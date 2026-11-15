#!/usr/bin/env python3
"""Пулы карт Реквиема (docs/design/19-card-pools.md): какие ванильные карты не выпадают героям мода и какие карты
скрытых героев открываются трансформациями.

Решения — в таблицах CUTS и GATED ниже, у каждой строки критерий и причина. Скрипт:
  * проверяет, что каждая карта существует в игре и лежит в указанном пуле (снимок tools/art/vanilla_models.txt);
  * пишет mod/SlayTheRequiem/data/requiem_pools.csv — его читает мод (Core/PoolTable.cs, Rules/RequiemPools.cs);
  * обновляет таблицы в docs/design/19-card-pools.md между маркерами <!-- pools:… -->.

    python3 tools/pools/build_pools.py          # записать CSV и таблицы документа
    python3 tools/pools/build_pools.py --check  # CI: код 1, если CSV или документ устарели
"""
from __future__ import annotations

import argparse
import collections
import os
import re
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "tools", "cards"))
import card_catalog as cc  # noqa: E402

VANILLA = os.path.join(ROOT, "tools", "art", "vanilla_models.txt")
OUT = os.path.join(ROOT, "mod", "SlayTheRequiem", "data", "requiem_pools.csv")
DOC = os.path.join(ROOT, "docs", "design", "19-card-pools.md")

# Критерии изъятия (19 §2).
CRITERIA = {
    "Д": "дубль механики мода: то же имя или роль, другие правила",
    "Т": "тёзка карты мода в том же пуле",
    "Ф": "форма: в Реквиеме формы дают только трансформации (06)",
    "Ч": "чужая фантазия: тема скрытого героя или другой роли",
}

# pool → {класс: (критерий, причина)}. Базовые и древние карты не трогаем — они не выпадают в наградах.
CUTS = {
    "Ironclad": {
        "StoneArmor": ("Д", "ванильные Латы (Блок в конце хода) ≠ Латы мода (−N к каждому удару, 03): два разных статуса с одним именем"),
        "DemonForm": ("Ф", "демоническая форма; рост Силы остаётся у Воспламенения и Ритуала крови"),
        "HowlFromBeyond": ("Ч", "магия Бездны (урон магией) у латника без школ; магия Латоносца — через заклинания и Кличи"),
        "PactsEnd": ("Ч", "магия «конца договора» — тема Тёмного договора Регента (06 §1.4)"),
        "Hemokinesis": ("Ч", "кровавая магия (урон магией); ветка «боль → сила» остаётся у Разрыва, Кровопускания, Подношения"),
    },
    "Silent": {
        "Backstab": ("Т", "у Безмолвной свой «Удар в спину» с правилом Подлого удара (17)"),
        "ShadowStep": ("Т", "у Безмолвной свой «Шаг тени» с Невидимостью (17)"),
    },
    "Regent": {
        "Begone": ("Ч", "королевские прислужники: у мага призывы — школа Колдовства (08)"),
        "Guards": ("Ч", "королевская стража из карт руки; призывы — Колдовство"),
        "Charge": ("Ч", "прислужники-пикировщики; призывы — Колдовство"),
        "BundleOfJoy": ("Ч", "щедрость короля: случайные бесцветные карты размывают колоду мага, у которого есть свиток заклинаний"),
        "SpectrumShift": ("Ч", "поток бесцветных карт каждый ход — то же, сильнее"),
        "Quasar": ("Ч", "выбор бесцветной карты; у Регента вместо этого «Свиток из рукава» — заклинание (18)"),
        "ManifestAuthority": ("Ч", "Блок + бесцветная карта: тема королевской власти"),
        "HeirloomHammer": ("Ч", "копирует бесцветные карты — без генераторов бесцветных карт пустая"),
        "Largesse": ("Ч", "дар бесцветной карты союзнику — та же тема (кооп)"),
        "KinglyKick": ("Ч", "королевский пинок: тяжёлая рукопашная у мага с ◆ 5/2"),
        "KinglyPunch": ("Ч", "королевский удар кулаком — рукопашная у мага"),
        "NeutronAegis": ("Д", "ванильные Латы ≠ Латы мода"),
        "Royalties": ("Ч", "королевский доход: золото за каждый бой в обход экономики лавок и Доверия (14)"),
        "VoidForm": ("Ф", "форма Пустоты; формы Регента — Тёмный договор → Вестник → Лич"),
    },
    "Colorless": {
        "EternalArmor": ("Д", "ванильные Латы ≠ Латы мода"),
    },
}

# Карты скрытых героев, которые открывает трансформация: gate → (пул-источник, описание, [классы]).
GATED = {
    "Pact": ("Necrobinder", "Тёмный договор, Вестник, Лич (06 §1.4): обречённость Некробиндера — облик Лича. "
                            "Без Формы Жнеца — формы только из трансформаций", [
        "BlightStrike", "NegativePulse", "Scourge",
        "Countdown", "Deathbringer", "DeathsDoor", "NoEscape", "Shroud",
        "TimesUp", "EndOfDays", "Oblivion", "Neurosurge",
    ]),
}

POOL_RU = {"Ironclad": "Латоносец (Ironclad)", "Silent": "Безмолвная (Silent)", "Regent": "Регент (Regent)",
           "Colorless": "Бесцветные", "Necrobinder": "Некробиндер"}
HERO_GROUP = {"Ironclad": "Латоносец", "Silent": "Безмолвная", "Regent": "Регент"}
POOL_RULES = os.path.join(ROOT, "mod", "SlayTheRequiemCode", "Core", "PoolTable.cs")


def own_share() -> int:
    """Доля своих карт в награде — из кода (PoolRules.OwnCardSharePercent), чтобы сводка не расходилась с правилом."""
    m = re.search(r"OwnCardSharePercent\s*=\s*(\d+)", open(POOL_RULES, encoding="utf-8").read())
    return int(m.group(1)) if m else 0


def vanilla_cards() -> dict:
    """{класс: пул} по снимку ванильных моделей."""
    out = {}
    for line in open(VANILLA, encoding="utf-8"):
        parts = line.split()
        if len(parts) == 3 and parts[0] == "card":
            out[parts[2]] = parts[1].capitalize()
    return out


def validate(vanilla: dict) -> list:
    errors = []
    for pool, cuts in CUTS.items():
        for key, (crit, reason) in cuts.items():
            if vanilla.get(key) != pool:
                errors.append(f"{key}: в игре пул {vanilla.get(key) or 'нет'} вместо {pool}")
            if crit not in CRITERIA:
                errors.append(f"{key}: неизвестный критерий «{crit}»")
            if not reason.strip():
                errors.append(f"{key}: нет причины")
    for gate, (pool, _, keys) in GATED.items():
        for key in keys:
            if vanilla.get(key) != pool:
                errors.append(f"{key} ({gate}): в игре пул {vanilla.get(key) or 'нет'} вместо {pool}")
    seen = collections.Counter(k for c in CUTS.values() for k in c)
    seen.update(k for _, _, keys in GATED.values() for k in keys)
    errors += [f"{k}: встречается {n} раза" for k, n in seen.items() if n > 1]
    return errors


def render_csv() -> str:
    lines = ["# Slay the Requiem: card pools in Requiem mode (docs/design/19). Generated by tools/pools/build_pools.py — do not edit.",
             "pool,key,decision,gate,criterion"]
    for pool, cuts in CUTS.items():
        for key, (crit, _) in sorted(cuts.items()):
            lines.append(f"{pool},{key},cut,,{crit}")
    for gate, (pool, _, keys) in GATED.items():
        for key in keys:
            lines.append(f"{pool},{key},gated,{gate},")
    return "\n".join(lines) + "\n"


def render_cuts() -> str:
    out = ["| Пул | Карта | Критерий | Почему |", "|---|---|---|---|"]
    for pool, cuts in CUTS.items():
        for key, (crit, reason) in sorted(cuts.items()):
            out.append(f"| {POOL_RU[pool]} | `{key}` | {crit} | {reason} |")
    return "\n".join(out) + "\n"


def render_gated() -> str:
    out = ["| Открывает | Пул-источник | Карты |", "|---|---|---|"]
    for gate, (pool, desc, keys) in GATED.items():
        out.append(f"| {desc} | {POOL_RU[pool]} | {', '.join(f'`{k}`' for k in keys)} |")
    return "\n".join(out) + "\n"


def render_summary(vanilla: dict) -> str:
    rows = cc.collect()
    out = ["| Герой | Ванильных в пуле | Изъято | Ванильных в наградах | Своих карт героя | Доля своих без правила | Доля с правилом §3 |",
           "|---|---|---|---|---|---|---|"]
    for pool, group in HERO_GROUP.items():
        total = sum(1 for p in vanilla.values() if p == pool)
        cut = len(CUTS.get(pool, {}))
        # базовые (Strike/Defend/стартовая) и древние в награды не попадают: 3–4 базовых + 2 древних
        reward = total - cut - BASIC_ANCIENT[pool]
        # Кличи лежат в пуле Ironclad и тоже выпадают Латоносцу (18 §2)
        groups = [group] + (["Кличи"] if pool == "Ironclad" else [])
        own = sum(1 for g in groups for r in rows.get(g, []) if r["rarity"] in ("Common", "Uncommon", "Rare"))
        share = 100 * own // max(1, own + reward)
        out.append(f"| {group} | {total} | {cut} | {reward} | {own} | {share} % | {max(share, own_share())} % |")
    return "\n".join(out) + "\n"


# Базовые + древние карты в пулах героев (не выпадают в наградах): Ironclad 3+2, Silent 4+2, Regent 4+2.
BASIC_ANCIENT = {"Ironclad": 5, "Silent": 6, "Regent": 6}

BLOCK = re.compile(r"(<!-- pools:(?P<name>\w+) -->\n)(?P<body>.*?)(<!-- /pools -->)", re.S)


def render_doc(text: str, vanilla: dict) -> str:
    parts = {"cuts": render_cuts(), "gated": render_gated(), "summary": render_summary(vanilla)}
    return BLOCK.sub(lambda m: m.group(1) + parts.get(m.group("name"), m.group("body")) + m.group(4), text)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    vanilla = vanilla_cards()
    errors = validate(vanilla)
    for e in errors:
        print("✗", e)
    if errors:
        return 1
    csv_text = render_csv()
    doc_text = open(DOC, encoding="utf-8").read() if os.path.exists(DOC) else ""
    new_doc = render_doc(doc_text, vanilla) if doc_text else ""
    stale = []
    if not os.path.exists(OUT) or open(OUT, encoding="utf-8").read() != csv_text:
        stale.append(os.path.relpath(OUT, ROOT))
        if not args.check:
            open(OUT, "w", encoding="utf-8").write(csv_text)
    if doc_text and new_doc != doc_text:
        stale.append(os.path.relpath(DOC, ROOT))
        if not args.check:
            open(DOC, "w", encoding="utf-8").write(new_doc)
    cut = sum(len(c) for c in CUTS.values())
    gated = sum(len(k) for _, _, k in GATED.values())
    if args.check and stale:
        for s in stale:
            print(f"✗ {s} устарел: python3 tools/pools/build_pools.py")
        return 1
    print(f"Пулы: изъято {cut}, открывается трансформациями {gated}" + (f"; обновлено: {', '.join(stale)}" if stale else ""))
    return 0


if __name__ == "__main__":
    sys.exit(main())
