#!/usr/bin/env python3
"""Проверка целей Harmony-патчей мода по декомпиляции игры (запускать после каждого обновления игры).

Для каждого [HarmonyPatch(typeof(X), nameof(X.M) | "M", ...)] проверяет:
  * класс X есть в декомпиляции;
  * метод/свойство M есть в X;
  * если типы аргументов не указаны — у M ровно одна перегрузка (иначе Harmony бросит AmbiguousMatchException,
    и PatchAll сорвётся целиком — так было с CardFactory.RollForUpgrade).

Декомпиляция — например, github.com/zhiyue/sts2-rl-agent (папка decompiled/):
    git clone --depth 1 --filter=blob:none --no-checkout https://github.com/zhiyue/sts2-rl-agent /tmp/rl
    git -C /tmp/rl sparse-checkout set --no-cone '/decompiled/**' && git -C /tmp/rl checkout
    python3 tools/ci/check_harmony_targets.py /tmp/rl/decompiled
"""
import glob
import os
import re
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
CODE = os.path.join(ROOT, "mod", "SlayTheRequiemCode")
PATCH = re.compile(r'\[HarmonyPatch\(typeof\((?P<cls>[\w.]+)\)\s*,\s*(?:nameof\([\w.]+\.(?P<n1>\w+)\)|"(?P<n2>\w+)")(?P<rest>[^\]]*)\]')


def main(decompiled: str) -> int:
    index: dict[str, list[str]] = {}
    for path in glob.glob(os.path.join(decompiled, "**", "*.cs"), recursive=True):
        index.setdefault(os.path.basename(path)[:-3], []).append(path)
    errors, checked = [], 0
    for path in sorted(glob.glob(os.path.join(CODE, "**", "*.cs"), recursive=True)):
        src = open(path, encoding="utf-8-sig").read()
        for m in PATCH.finditer(src):
            cls = m.group("cls").split(".")[-1]
            name = m.group("n1") or m.group("n2")
            rest = m.group("rest")
            where = f"{os.path.relpath(path, ROOT)}:{src[:m.start()].count(chr(10)) + 1}"
            checked += 1
            files = index.get(cls)
            if not files:
                errors.append(f"{where}: класса {cls} нет в декомпиляции")
                continue
            text = "\n".join(open(f, encoding="utf-8", errors="ignore").read() for f in files)
            if "MethodType.Getter" in rest or "MethodType.Setter" in rest:
                if not re.search(rf"\b{re.escape(name)}\s*(\{{|=>)", text):
                    errors.append(f"{where}: свойства {cls}.{name} нет")
                continue
            if "MethodType.Constructor" in rest:
                continue
            overloads = re.findall(rf"^\s*(?:public|private|protected|internal)[^=;(\n]*?\b{re.escape(name)}\s*(?:<[^>]*>)?\s*\(",
                                   text, re.M)
            if not overloads:
                errors.append(f"{where}: метода {cls}.{name} нет")
            elif len(overloads) > 1 and "typeof" not in rest:
                errors.append(f"{where}: у {cls}.{name} перегрузок: {len(overloads)} — укажите типы аргументов")
    for e in errors:
        print("✗", e)
    print(f"Патчей проверено: {checked}; ошибок: {len(errors)}")
    return 1 if errors else 0


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(2)
    sys.exit(main(sys.argv[1]))
