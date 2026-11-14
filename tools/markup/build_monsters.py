#!/usr/bin/env python3
"""Ручная разметка ванильных врагов для Реквиема → mod/SlayTheRequiem/data/monsters_requiem.csv.

Каждый враг получает фракцию (docs/design/11 §3, 03 «Сопротивления по фракциям»), Латы, надбавку Стойкости
и тип урона атак. Ступени сопротивлений: -1 уязвимость, 1..3 сопротивление, 4 иммунитет.
Враги, которых нет в словаре, получают значения по умолчанию (фракция «—», без Лат, физический урон).

Источник списка ключей — классы Models.Monsters игры (0.107). Тестовые/служебные монстры не размечаются.
"""
import csv
import os
import sys

# Шаблоны сопротивлений по фракциям: fire, frost, shock, poison, magic
FACTIONS = {
    "beast":     (-1, 1, 0, 0, 0),   # звери: боятся огня
    "plant":     (-1, 0, 0, 1, 0),   # растения/грибы
    "construct": (1, -1, -1, 4, 0),  # конструкты: иммунитет к яду, уязвимы к холоду и разряду (закоротка)
    "undead":    (-1, 2, 0, 4, 0),   # нежить
    "spirit":    (0, 1, 0, 4, 1),    # призраки/туманы
    "aquatic":   (1, 0, -1, 0, 0),   # водные: разряд бьёт сильнее
    "insect":    (-1, -1, 0, 1, 0),  # насекомые/улей
    "cultist":   (0, 0, 0, 0, 1),    # культисты/маги
    "humanoid":  (0, 0, 0, 0, 0),
    "abyss":     (2, -1, 0, 4, 0),   # порождения Бездны
    "slime":     (-1, 1, 0, 2, 0),
}

# key: (faction, plating, poise_bonus, attack_type, note)
M = {
    # --- Акт 1: Заросли ---
    "LeafSlimeS": ("slime", 0, 0, "phys", ""), "LeafSlimeM": ("slime", 0, 0, "phys", ""),
    "TwigSlimeS": ("slime", 0, 0, "phys", ""), "TwigSlimeM": ("slime", 0, 0, "phys", ""),
    "VineShambler": ("plant", 0, 2, "phys", ""), "Flyconid": ("plant", 0, 0, "poison", "грибные споры"),
    "SnappingJaxfruit": ("plant", 0, 0, "phys", ""), "Fogmog": ("plant", 0, 0, "phys", ""),
    "PhantasmalGardener": ("spirit", 0, 0, "magic", ""),
    "ShrinkerBeetle": ("insect", 1, 0, "phys", "хитин"), "SlumberingBeetle": ("insect", 2, 4, "phys", "панцирь"),
    "Nibbit": ("beast", 0, 0, "phys", ""), "TwoTailedRat": ("beast", 0, 0, "phys", ""),
    "FuzzyWurmCrawler": ("insect", 0, 0, "phys", ""), "Inklet": ("cultist", 0, 0, "magic", ""),
    "KinFollower": ("cultist", 0, 0, "phys", ""), "KinPriest": ("cultist", 0, 0, "magic", "босс"),
    "Byrdonis": ("beast", 0, 0, "phys", ""), "Byrdpip": ("beast", 0, 0, "phys", ""),
    "Vantom": ("spirit", 0, 0, "magic", "босс"), "CeremonialBeast": ("beast", 1, 0, "phys", "босс"),
    "BygoneEffigy": ("construct", 2, 0, "phys", "идол"), "Mawler": ("beast", 0, 2, "phys", ""),
    "SpinyToad": ("aquatic", 0, 0, "phys", ""), "Toadpole": ("aquatic", 0, 0, "phys", ""),
    "FrogKnight": ("aquatic", 2, 3, "phys", "рыцарь"),
    # --- Акт 1: Подземные доки ---
    "SewerClam": ("aquatic", 3, 2, "phys", "раковина"), "CorpseSlug": ("undead", 0, 0, "poison", ""),
    "Seapunk": ("aquatic", 0, 0, "phys", ""), "TerrorEel": ("aquatic", 0, 0, "shock", "угорь"),
    "SludgeSpinner": ("insect", 0, 0, "poison", ""), "SkulkingColony": ("insect", 0, 0, "phys", ""),
    "HauntedShip": ("spirit", 2, 6, "phys", ""), "LagavulinMatriarch": ("aquatic", 3, 0, "phys", "босс, панцирь"),
    "SoulFysh": ("spirit", 0, 0, "magic", "босс"), "WaterfallGiant": ("aquatic", 1, 0, "phys", "босс"),
    "Wriggler": ("insect", 0, 0, "phys", ""), "PhrogParasite": ("insect", 0, 0, "poison", ""),
    "AssassinRubyRaider": ("humanoid", 0, 0, "phys", "Багряные Ножи"), "AxeRubyRaider": ("humanoid", 0, 0, "phys", "Багряные Ножи"),
    "BruteRubyRaider": ("humanoid", 1, 3, "phys", "Багряные Ножи"), "CrossbowRubyRaider": ("humanoid", 0, 0, "phys", "Багряные Ножи"),
    "TrackerRubyRaider": ("humanoid", 0, 0, "phys", "Багряные Ножи"), "GremlinMerc": ("humanoid", 0, 0, "phys", ""),
    "FatGremlin": ("humanoid", 0, 2, "phys", ""), "SneakyGremlin": ("humanoid", 0, 0, "phys", ""),
    "ThievingHopper": ("beast", 0, 0, "phys", ""), "LivingFog": ("spirit", 0, 0, "magic", ""),
    "GasBomb": ("construct", 0, 0, "fire", ""), "Chomper": ("beast", 0, 0, "phys", ""),
    "DampCultist": ("cultist", 0, 0, "magic", ""), "CalcifiedCultist": ("cultist", 2, 0, "phys", "окаменевший"),
    # --- Акт 2: Улей ---
    "BowlbugEgg": ("insect", 1, 0, "phys", ""), "BowlbugNectar": ("insect", 0, 0, "poison", ""),
    "BowlbugRock": ("insect", 3, 4, "phys", "каменный"), "BowlbugSilk": ("insect", 0, 0, "phys", ""),
    "Entomancer": ("cultist", 0, 0, "magic", ""), "Exoskeleton": ("insect", 3, 2, "phys", ""),
    "Ovicopter": ("insect", 0, 0, "phys", ""), "LouseProgenitor": ("insect", 1, 0, "phys", ""),
    "DecimillipedeSegment": ("insect", 2, 0, "phys", ""), "DecimillipedeSegmentFront": ("insect", 2, 0, "phys", ""),
    "DecimillipedeSegmentMiddle": ("insect", 2, 0, "phys", ""), "DecimillipedeSegmentBack": ("insect", 2, 0, "phys", ""),
    "Myte": ("insect", 0, 0, "phys", ""), "Tunneler": ("insect", 1, 2, "phys", ""),
    "SlitheringStrangler": ("beast", 0, 0, "phys", ""), "HunterKiller": ("construct", 2, 0, "phys", ""),
    "TheInsatiable": ("abyss", 0, 0, "phys", "босс"), "KnowledgeDemon": ("abyss", 0, 0, "magic", "босс"),
    "InfestedPrism": ("construct", 2, 0, "magic", ""), "ToughEgg": ("insect", 4, 0, "phys", ""),
    "SpectralKnight": ("spirit", 2, 4, "magic", "рыцарь"), "MagiKnight": ("cultist", 2, 4, "magic", "рыцарь"),
    "FlailKnight": ("humanoid", 3, 4, "phys", "рыцарь"), "MysteriousKnight": ("humanoid", 3, 4, "phys", "рыцарь"),
    "MechaKnight": ("construct", 4, 6, "phys", "рыцарь-механизм"),
    "EyeWithTeeth": ("abyss", 0, 0, "phys", ""), "Parafright": ("spirit", 0, 0, "magic", ""),
    # --- Акт 3: Слава ---
    "Axebot": ("construct", 2, 0, "phys", ""), "Guardbot": ("construct", 3, 2, "phys", ""),
    "Stabbot": ("construct", 1, 0, "phys", ""), "Zapbot": ("construct", 1, 0, "shock", ""),
    "Noisebot": ("construct", 1, 0, "phys", ""), "CubexConstruct": ("construct", 3, 2, "phys", ""),
    "PunchConstruct": ("construct", 2, 2, "phys", ""), "Fabricator": ("construct", 2, 0, "phys", ""),
    "Crusher": ("construct", 3, 4, "phys", ""), "Rocket": ("construct", 1, 0, "fire", ""),
    "TurretOperator": ("humanoid", 1, 0, "phys", ""), "GlobeHead": ("construct", 2, 0, "shock", ""),
    "OwlMagistrate": ("cultist", 0, 2, "magic", ""), "DevotedSculptor": ("cultist", 0, 0, "phys", ""),
    "FossilStalker": ("undead", 2, 2, "phys", ""), "LivingShield": ("construct", 4, 4, "phys", "щит"),
    "SoulNexus": ("spirit", 0, 0, "magic", ""), "ScrollOfBiting": ("cultist", 0, 0, "magic", ""),
    "SlimedBerserker": ("slime", 0, 2, "phys", ""), "PaelsLegion": ("humanoid", 2, 2, "phys", ""),
    "TheForgotten": ("undead", 0, 0, "magic", ""), "TheLost": ("undead", 0, 0, "phys", ""),
    "TheObscura": ("abyss", 0, 0, "magic", ""),
    "Queen": ("insect", 1, 0, "phys", "босс"), "TorchHeadAmalgam": ("construct", 2, 0, "fire", "босс"),
    "TestSubject": ("abyss", 0, 0, "phys", "босс"), "Aeonglass": ("construct", 2, 0, "magic", "босс"),
    "TheAdversaryMkOne": ("construct", 2, 0, "phys", ""), "TheAdversaryMkTwo": ("construct", 3, 0, "phys", ""),
    "TheAdversaryMkThree": ("construct", 4, 0, "phys", ""),
    "Architect": ("abyss", 0, 0, "magic", "финал"),
    "BattleFriendV1": ("construct", 1, 0, "phys", ""), "BattleFriendV2": ("construct", 2, 0, "phys", ""),
    "BattleFriendV3": ("construct", 3, 0, "phys", ""),
}

SKIP = {"BigDummy", "DeprecatedMonster", "MultiAttackMoveMonster", "SingleAttackMoveMonster", "OneHpMonster",
        "TenHpMonster", "FakeMerchantMonster", "Osty"}

FIELDS = ["key", "faction", "plating", "poise_bonus", "res_fire", "res_frost", "res_shock", "res_poison", "res_magic",
          "attack_type", "note"]


def main() -> int:
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
    out = os.path.join(root, "mod", "SlayTheRequiem", "data", "monsters_requiem.csv")
    known = set(sys.argv[1].split(",")) if len(sys.argv) > 1 else None
    if known is not None:
        missing = sorted(known - set(M) - SKIP)
        unknown = sorted(set(M) - known)
        if unknown:
            print("Лишние ключи:", unknown)
            return 1
        if missing:
            print("Не размечены (получат значения по умолчанию):", missing)
    with open(out, "w", newline="", encoding="utf-8") as f:
        f.write("# Генерируется tools/markup/build_monsters.py — не править руками.\n")
        w = csv.DictWriter(f, fieldnames=FIELDS, lineterminator="\n")
        w.writeheader()
        for key in sorted(M):
            fac, plating, poise, atk, note = M[key]
            r = FACTIONS[fac]
            w.writerow(dict(key=key, faction=fac, plating=plating, poise_bonus=poise, res_fire=r[0], res_frost=r[1],
                            res_shock=r[2], res_poison=r[3], res_magic=r[4], attack_type=atk, note=note))
    print(f"OK: {len(M)} врагов → {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
