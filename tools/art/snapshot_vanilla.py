#!/usr/bin/env python3
"""Снимок ванильных моделей игры для проверки таблицы заимствованного арта (tools/art/vanilla_art.csv).

Источник — декомпиляция игры (например, github.com/zhiyue/sts2-rl-agent, папка decompiled/).
Пишет tools/art/vanilla_models.txt: строки «kind pool Class» (pool только у карт, у прочих «-»),
tools/art/vanilla_card_types.txt: «Class Type» (Attack/Skill/Power/…) — для проверки tools/art/art_fit.py,
и tools/relics/vanilla_relics_snapshot.txt: «Class Pool Rarity признаки» — для таблицы переработки реликвий
(tools/relics/build_relics.py; признаки: energy — меняет макс. энергию, once — действует только при получении).
Перезапускать после обновлений игры:

    python3 tools/art/snapshot_vanilla.py /path/to/decompiled 0.107.1
"""
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "vanilla_models.txt")
TYPES_OUT = os.path.join(HERE, "vanilla_card_types.txt")
RELICS_OUT = os.path.join(HERE, "..", "relics", "vanilla_relics_snapshot.txt")
# Пулы реликвий, которые видит игрок (Deprecated и Fallback — служебные).
RELIC_POOLS = ["Shared", "Ironclad", "Silent", "Regent", "Necrobinder", "Defect", "Event"]


def snapshot_relics(src: str, version: str) -> None:
    pools_dir = os.path.join(src, "MegaCrit.Sts2.Core.Models.RelicPools")
    relics_dir = os.path.join(src, "MegaCrit.Sts2.Core.Models.Relics")
    pools_of: dict[str, list[str]] = {}
    for pool in RELIC_POOLS:
        text = open(os.path.join(pools_dir, f"{pool}RelicPool.cs"), encoding="utf-8").read()
        for name in sorted(set(re.findall(r"ModelDb\.Relic<(\w+)>", text))):
            pools_of.setdefault(name, []).append(pool)
    rows = []
    for name, pools in sorted(pools_of.items()):
        pool = "+".join(pools)
        body = open(os.path.join(relics_dir, f"{name}.cs"), encoding="utf-8").read()
        rarity = re.search(r"Rarity => RelicRarity\.(\w+)", body)
        hooks = set(re.findall(r"public override (?:async )?[\w<>?,\s]+? (\w+)\(", body))
        flags = []
        if "ModifyMaxEnergy" in hooks:
            flags.append("energy")
        if hooks and hooks <= {"AfterObtained"}:
            flags.append("once")
        rows.append(f"{name} {pool} {rarity.group(1) if rarity else 'None'} {','.join(flags) or '-'}")
    os.makedirs(os.path.dirname(RELICS_OUT), exist_ok=True)
    with open(RELICS_OUT, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(f"# Ванильные реликвии Slay the Spire 2 {version} (tools/art/snapshot_vanilla.py): класс, пул, редкость, признаки\n")
        fh.write("\n".join(rows) + "\n")
    print(f"{os.path.normpath(RELICS_OUT)}: {len(rows)} реликвий")
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
    types = []
    cards_dir = os.path.join(src, "MegaCrit.Sts2.Core.Models.Cards")
    for name in sorted({r.split()[2] for r in rows if r.startswith("card ")}):
        path = os.path.join(cards_dir, f"{name}.cs")
        m = re.search(r"base\(\s*-?\d+\s*,\s*CardType\.(\w+)", open(path, encoding="utf-8").read()) if os.path.exists(path) else None
        types.append(f"{name} {m.group(1) if m else '?'}")
    with open(TYPES_OUT, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(f"# Типы ванильных карт Slay the Spire 2 {version} (tools/art/snapshot_vanilla.py)\n")
        fh.write("\n".join(types) + "\n")
    print(f"{TYPES_OUT}: {len(types)} карт")
    snapshot_relics(src, version)
    return 0


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(2)
    sys.exit(main(sys.argv[1], sys.argv[2]))
