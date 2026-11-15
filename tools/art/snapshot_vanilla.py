#!/usr/bin/env python3
"""Снимок ванильных моделей игры для проверки таблицы заимствованного арта (tools/art/vanilla_art.csv).

Источник — декомпиляция игры (например, github.com/zhiyue/sts2-rl-agent, папка decompiled/).
Пишет tools/art/vanilla_models.txt: строки «kind pool Class» (pool только у карт, у прочих «-»).
Перезапускать после обновлений игры:

    python3 tools/art/snapshot_vanilla.py /path/to/decompiled 0.107.1
"""
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "vanilla_models.txt")
POOLS = ["Ironclad", "Silent", "Regent", "Necrobinder", "Defect", "Colorless", "Event", "Token", "Curse", "Status", "Quest"]


def main(src: str, version: str) -> int:
    rows = []
    pools_dir = os.path.join(src, "MegaCrit.Sts2.Core.Models.CardPools")
    for pool in POOLS:
        path = os.path.join(pools_dir, f"{pool}CardPool.cs")
        if not os.path.exists(path):
            continue
        text = open(path, encoding="utf-8").read()
        title = re.search(r'Title => "(\w+)"', text)
        title = title.group(1) if title else pool.lower()
        for name in sorted(set(re.findall(r"ModelDb\.Card<(\w+)>", text))):
            rows.append(f"card {title} {name}")
    for kind, folder in [("power", "Powers"), ("relic", "Relics"), ("event", "Events"), ("enchantment", "Enchantments")]:
        d = os.path.join(src, f"MegaCrit.Sts2.Core.Models.{folder}")
        for f in sorted(os.listdir(d)):
            if f.endswith(".cs") and not f.startswith(("Deprecated", "Fake", "Mock")):
                rows.append(f"{kind} - {f[:-3]}")
    with open(OUT, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(f"# Ванильные модели Slay the Spire 2 {version} (tools/art/snapshot_vanilla.py)\n")
        fh.write("\n".join(rows) + "\n")
    print(f"{OUT}: {len(rows)} моделей")
    return 0


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(2)
    sys.exit(main(sys.argv[1], sys.argv[2]))
