namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Вид призванного существа (docs/design/08 §5, M6 п. 2): у каждого — своё существо на поле и свой спрайт.</summary>
public enum SummonKind
{
    SpiritWolf,
    BeastWolf,
    BeastSabertooth,
    BeastBear,
    FlameAtronach,
    FrostAtronach,
    StormAtronach,
    RaisedDead,
    AbyssGuardian,
    SpectralKnight,
    LoyalGuard,
    BoneThrall,
}

/// <summary>Что призыв сделает в конце хода героя (намерение над существом).</summary>
public enum SummonMove
{
    Strike,
    Slumber,
}

/// <summary>
/// Чистые правила призывов (без игры; зеркало — tools/sim/requiem_rules.py). Призыв — существо на стороне героя с ♥ = стакам
/// силы-призыва: урон атак врагов по ♥ героя сначала уходит в ♥ призыва, в конце хода героя призыв бьёт. Слот один
/// (+Кольцо Призывателя и др.), лишние — самые старые — уходят. Спутник («Верный-до-Конца») слот не занимает.
/// </summary>
public static class SummonRules
{
    public const int BaseSlots = 1;

    /// <summary>«Поднять мертвеца»: 50 % макс. ♥ последнего павшего врага (не босса), с улучшением — 65 %.</summary>
    public const int RaiseDeadPercent = 50;
    public const int RaiseDeadPercentUpgrade = 15;
    public const int RaisedDeadDamage = 4;

    /// <summary>«Страж Бездны»: «Дремлющий страж» 30 ♥ (+8), первый ход дремлет, затем бьёт 10 магией.</summary>
    public const int AbyssGuardianHp = 30;
    public const int AbyssGuardianHpUpgrade = 8;
    public const int AbyssGuardianDamage = 10;
    public const int AbyssGuardianSlumberTurns = 1;

    /// <summary>«Зов Рыцаря-Духа»: Призрачный рыцарь 25 ♥ (+7), бьёт на 5; пока жив — ваши атаки +2.</summary>
    public const int SpectralKnightHp = 25;
    public const int SpectralKnightHpUpgrade = 7;
    public const int SpectralKnightDamage = 5;
    public const int SpectralKnightAttackBonus = 2;

    /// <summary>Нежить Лича: в начале каждого боя — Костяной прислужник 10 ♥, бьёт 3 магией.</summary>
    public const int BoneThrallHp = 10;
    public const int BoneThrallDamage = 3;

    /// <summary>Сколько призывов держит герой: 1 + бонусы (Кольцо Призывателя, Знамение Обряда, дары).</summary>
    public static int Slots(int bonus) => BaseSlots + Math.Max(0, bonus);

    /// <summary>Сколько старых призывов уйдёт при новом (спутники не считаются).</summary>
    public static int Dismissals(int otherSummons, int slots) => Math.Max(0, otherSummons - (Math.Max(1, slots) - 1));

    /// <summary>Сколько урона по герою забирает призыв с <paramref name="summonHp"/> ♥ (часть уже «занята» этим ударом).</summary>
    public static int Absorb(int incoming, int summonHp, int pending) => Math.Max(0, Math.Min(incoming, summonHp - pending));

    /// <summary>Сколько ♥ снимает удар с призыва: +<paramref name="percent"/> % (Цена Князя Измены), вверх.</summary>
    public static int ScaledLoss(int taken, int percent) =>
        taken <= 0 ? 0 : percent <= 0 ? taken : (taken * (100 + percent) + 99) / 100;

    /// <summary>♥ поднятого мертвеца (не меньше 1, если был кто поднимать).</summary>
    public static int RaisedDeadHp(int maxHp, int percent) => maxHp <= 0 || percent <= 0 ? 0 : Math.Max(1, maxHp * percent / 100);

    /// <summary>Кого можно поднять: павшего врага, не босса (прислужники боссов — можно) и не своего союзника.</summary>
    public static bool CanRaise(bool isEnemy, bool isBoss) => isEnemy && !isBoss;

    /// <summary>Облик «Духа зверя» по числу побеждённых элит (06, предыстория Зверобоя-духовидца).</summary>
    public static SummonKind BeastKind(int elitesDefeated) => elitesDefeated switch
    {
        <= 0 => SummonKind.BeastWolf,
        1 => SummonKind.BeastSabertooth,
        _ => SummonKind.BeastBear,
    };

    /// <summary>Ход призыва: Дремлющий страж первые ходы спит, остальные бьют.</summary>
    public static SummonMove MoveFor(SummonKind kind, int turnsOnField) =>
        kind == SummonKind.AbyssGuardian && turnsOnField < AbyssGuardianSlumberTurns ? SummonMove.Slumber : SummonMove.Strike;

    public static bool IsUndead(SummonKind kind) => kind is SummonKind.RaisedDead or SummonKind.BoneThrall;

    public static bool IsCompanion(SummonKind kind) => kind == SummonKind.LoyalGuard;

    /// <summary>Спрайт <c>images/monsters/{key}.png</c> (tools/art/monsters/draw_monsters.py).</summary>
    public static string ArtKey(SummonKind kind) => kind switch
    {
        SummonKind.SpiritWolf => "summon_spirit_wolf",
        SummonKind.BeastWolf => "summon_beast_wolf",
        SummonKind.BeastSabertooth => "summon_beast_sabertooth",
        SummonKind.BeastBear => "summon_beast_bear",
        SummonKind.FlameAtronach => "summon_flame_atronach",
        SummonKind.FrostAtronach => "summon_frost_atronach",
        SummonKind.StormAtronach => "summon_storm_atronach",
        SummonKind.RaisedDead => "summon_raised_dead",
        SummonKind.AbyssGuardian => "summon_abyss_guardian",
        SummonKind.SpectralKnight => "summon_spectral_knight",
        SummonKind.LoyalGuard => "summon_loyal_guard",
        _ => "summon_bone_thrall",
    };

    // ───── раскладка на поле ─────

    public const float FirstGap = 60f;
    public const float Spacing = 150f;
    /// <summary>Сдвиг, если перед героем уже стоит Ости (Некробиндер).</summary>
    public const float OstyGap = 220f;

    /// <summary>Сдвиг <paramref name="index"/>-го призыва вправо от центра героя (к врагам).</summary>
    public static float LayoutX(float heroHalfWidth, int index, bool hasOsty) =>
        heroHalfWidth + FirstGap + Math.Max(0, index) * Spacing + (hasOsty ? OstyGap : 0f);
}
