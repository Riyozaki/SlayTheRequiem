"""Тексты M6 п. 2: настоящие призывы (docs/design/08 §5, R-17) — три новых заклинания, силы-призывы и тела призывов на поле."""

from loc_stage7 import _m

# ───────────── заклинания Колдовства (08 §5) ─────────────
M6_SUMMON_CARDS = [
    ("RaiseDead", "Raise Dead",
     "Raise the last enemy that fell this combat (not a boss) as an undead summon with {Percent:diff()}% of its max HP. "
     "It deals 4 damage at the end of your turn.",
     "Поднять мертвеца",
     "Поднять последнего павшего в этом бою врага (не босса) нежитью-призывом с {Percent:diff()} % его макс. ♥. "
     "Бьёт на 4 в конце вашего хода."),
    ("ConjureAbyssGuardian", "Abyss Guardian",
     "Summon a [gold]Slumbering Guardian[/gold] with {SummonHp:diff()} HP. It slumbers on its first turn, "
     "then deals 10 [purple]magic[/purple] damage each turn.",
     "Страж Бездны",
     "Призвать [gold]Дремлющего стража[/gold] ({SummonHp:diff()} ♥). Первый ход дремлет, "
     "затем каждый ход бьёт на 10 [purple]магией[/purple]."),
    ("CallSpiritKnight", "Call the Spirit Knight",
     "Summon a [gold]Spectral Knight[/gold] with {SummonHp:diff()} HP. It deals 5 damage each turn; "
     "while it stands, your attacks deal 2 more damage.",
     "Зов Рыцаря-Духа",
     "Призвать [gold]Призрачного рыцаря[/gold] ({SummonHp:diff()} ♥). Каждый ход бьёт на 5; "
     "пока он стоит, ваши атаки наносят на 2 урона больше."),
]

# ───────────── силы-призывы на герое ─────────────
M6_SUMMON_POWERS = [
    ("RaisedDeadPower", "Raised Dead",
     "Undead summon with {Amount} HP: takes enemy attacks first, deals 4 damage at the end of your turn.",
     "Поднятый мертвец",
     "Призыв-нежить ({Amount} ♥): первым принимает атаки врагов, бьёт на 4 в конце вашего хода."),
    ("AbyssGuardianPower", "Slumbering Guardian",
     "Summon with {Amount} HP: takes attacks first. Slumbers on its first turn, then deals 10 [purple]magic[/purple] damage at the end of your turn.",
     "Дремлющий страж",
     "Призыв ({Amount} ♥): первым принимает атаки. Первый ход дремлет, затем в конце вашего хода бьёт на 10 [purple]магией[/purple]."),
    ("SpectralKnightPower", "Spectral Knight",
     "Summon with {Amount} HP: takes attacks first, deals 5 damage at the end of your turn. While it stands, your attacks deal 2 more damage.",
     "Призрачный рыцарь",
     "Призыв ({Amount} ♥): первым принимает атаки, бьёт на 5 в конце вашего хода. Пока он стоит, ваши атаки наносят на 2 урона больше."),
    ("BoneThrallPower", "Bone Thrall",
     "The Lich's undead with {Amount} HP: takes attacks first, deals 3 [purple]magic[/purple] damage at the end of your turn. Its fall triggers Death Blast.",
     "Костяной прислужник",
     "Нежить Лича ({Amount} ♥): первой принимает атаки, в конце вашего хода бьёт на 3 [purple]магией[/purple]. Её гибель — Взрыв смерти."),
]

# ───────────── тела призывов на поле (питомцы героя) ─────────────
_STRIKE = ("Strike", "Удар")
_SLUMBER = ("Slumber", "Дремлет")


def _summon(eng, rus):
    return _m(eng, rus, {"SUMMON_STRIKE": _STRIKE, "SUMMON_SLUMBER": _SLUMBER})


M6_SUMMON_MONSTERS = {
    "SPIRIT_WOLF_SUMMON": _summon("Spirit Wolf", "Волк-дух"),
    "BEAST_WOLF_SUMMON": _summon("Beast Spirit: Wolf", "Дух зверя: волк"),
    "BEAST_SABERTOOTH_SUMMON": _summon("Beast Spirit: Sabertooth", "Дух зверя: саблезуб"),
    "BEAST_BEAR_SUMMON": _summon("Beast Spirit: Bear", "Дух зверя: медведь"),
    "FLAME_ATRONACH_SUMMON": _summon("Flame Atronach", "Огненный атронах"),
    "FROST_ATRONACH_SUMMON": _summon("Frost Atronach", "Ледяной атронах"),
    "STORM_ATRONACH_SUMMON": _summon("Storm Atronach", "Грозовой атронах"),
    "RAISED_DEAD_SUMMON": _summon("Raised Dead", "Поднятый мертвец"),
    "ABYSS_GUARDIAN_SUMMON": _summon("Slumbering Guardian", "Дремлющий страж"),
    "SPECTRAL_KNIGHT_SUMMON": _summon("Spectral Knight", "Призрачный рыцарь"),
    "LOYAL_GUARD_SUMMON": _summon("Loyal-to-the-End", "Верный-до-Конца"),
    "BONE_THRALL_SUMMON": _summon("Bone Thrall", "Костяной прислужник"),
}
