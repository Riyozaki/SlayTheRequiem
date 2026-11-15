#!/usr/bin/env python3
"""Проверка событий актов (M5 п. 3, docs/design/13 §5): каждое событие Реквиема

* sealed, наследует RequiemEvent, задаёт HomeAct и зарегистрировано в ActEvents.For своего акта;
* у каждого нового акта (2а, 2б, 4а, 4б) не меньше 6 своих событий, у оверлеев Улья и Славы — не меньше 3;
* все варианты (Choice("KEY")) и страницы (Finish("PAGE")) есть в events.json на обоих языках;
* все {Числа} из текстов объявлены в Numbers (DynamicVars), картинка images/events/<id>.jpg на месте.

    python3 tools/ci/check_events.py
"""
from __future__ import annotations

import glob
import json
import os
import re
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
CODE = os.path.join(ROOT, "mod", "SlayTheRequiemCode", "Events")
RES = os.path.join(ROOT, "mod", "SlayTheRequiem")
PREFIX = "SLAYTHEREQUIEM-"
MIN_EVENTS = {"FrozenBarrows": 6, "ClockworkDepths": 6, "BloodCourt": 6, "StarTower": 6, "Hive": 3, "Glory": 3}
SCHOOLS = ["DESTRUCTION", "RESTORATION", "ALTERATION", "CONJURATION", "ILLUSION"]


def snake(name: str) -> str:
    return re.sub(r"(?<=[a-z0-9])(?=[A-Z])", "_", name).upper()


def main() -> int:
    src = {p: open(p, encoding="utf-8").read() for p in glob.glob(os.path.join(CODE, "*.cs"))}
    base = src.get(os.path.join(CODE, "RequiemEvent.cs"), "")
    loc = {lang: json.load(open(os.path.join(RES, "localization", lang, "events.json"), encoding="utf-8")) for lang in ("eng", "rus")}
    errors: list[str] = []

    registered: dict[str, str] = {}
    m = re.search(r"For\(ActKind kind\) => kind switch\s*\{(.*?)\n    \};", base, re.S)
    if not m:
        errors.append("RequiemEvent.cs: не найден ActEvents.For")
    else:
        for arm in re.finditer(r"ActKind\.(\w+) =>(.*?)(?=ActKind\.\w+ =>|_ =>)", m.group(1), re.S):
            for cls in re.findall(r"ModelDb\.Event<(\w+)>", arm.group(2)):
                registered[cls] = arm.group(1)

    events: dict[str, str] = {}
    for path, text in src.items():
        for cm in re.finditer(r"public (sealed )?class (\w+) : RequiemEvent\b(.*?)(?=\npublic |\Z)", text, re.S):
            sealed, name, body = cm.group(1), cm.group(2), cm.group(3)
            where = f"{os.path.basename(path)}:{name}"
            if not sealed:
                errors.append(f"{where}: класс события должен быть sealed (манифест картинок)")
            act = re.search(r"HomeAct => ActKind\.(\w+);", body)
            if not act:
                errors.append(f"{where}: нет HomeAct")
                continue
            events[name] = act.group(1)
            if registered.get(name) != act.group(1):
                errors.append(f"{where}: HomeAct = {act.group(1)}, а в ActEvents.For — {registered.get(name)}")
            eid = PREFIX + snake(name)
            keys = set(re.findall(r'Choice\("(\w+)"', body))
            if "PASS_{school" in body:
                keys |= {f"PASS_{s}" for s in SCHOOLS}
            pages = set(re.findall(r'Finish\("(\w+)"\)', body))
            if "LeaveChoice()" in body:
                keys.add("LEAVE")
                pages.add("LEAVE")
            numbers = set(re.findall(r'\("(\w+)", -?\d+\)', body))
            need = [f"{eid}.title", f"{eid}.pages.INITIAL.description"]
            need += [f"{eid}.pages.INITIAL.options.{k}.{f}" for k in keys for f in ("title", "description")]
            need += [f"{eid}.pages.{p}.description" for p in pages]
            for lang, table in loc.items():
                for key in need:
                    if key not in table:
                        errors.append(f"{where}: нет {lang}/events.json «{key}»")
                for key, text_ in table.items():
                    if key.startswith(eid + "."):
                        for var in re.findall(r"\{(\w+)(?::[^}]*)?\}", text_):
                            if var not in numbers:
                                errors.append(f"{where}: {lang} «{key}» использует {{{var}}}, а в Numbers его нет")
            jpg = os.path.join(RES, "images", "events", f"{snake(name).lower()}.jpg")
            if not os.path.exists(jpg):
                errors.append(f"{where}: нет картинки {os.path.relpath(jpg, ROOT)}")

    for cls in registered:
        if cls not in events:
            errors.append(f"ActEvents.For: {cls} не найден среди событий RequiemEvent")
    for act, need in MIN_EVENTS.items():
        have = sum(1 for a in events.values() if a == act)
        if have < need:
            errors.append(f"{act}: событий {have}, нужно не меньше {need} (13 §5)")

    for e in errors:
        print("✗", e)
    by_act = ", ".join(f"{a} {sum(1 for x in events.values() if x == a)}" for a in MIN_EVENTS)
    print(f"событий актов: {len(events)} ({by_act}); ошибок: {len(errors)}")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
