namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Чистые числа пассивов происхождений и предысторий (docs/design/04), зеркало — tools/sim.</summary>
public static class OriginRules
{
    /// <summary>«Кора топи» (Тинники): лечение в начале хода при ♥ &lt; 50 % — 2, при &lt; 25 % — 4.</summary>
    public static int MarshbarkHeal(int hp, int maxHp)
    {
        if (maxHp <= 0 || hp <= 0) return 0;
        if (hp * 4 < maxHp) return 4;
        if (hp * 2 < maxHp) return 2;
        return 0;
    }

    /// <summary>«Рыцарь» (Туманные): Латы +1/+2/+3 при ♥ ниже 90/70/50 %.</summary>
    public static int KnightPlating(int hp, int maxHp)
    {
        if (maxHp <= 0) return 0;
        if (hp * 2 < maxHp) return 3;
        if (hp * 10 < maxHp * 7) return 2;
        if (hp * 10 < maxHp * 9) return 1;
        return 0;
    }

    /// <summary>«Волшебник» (Златокровные): +1 урона заклинаниям за каждые потерянные 25 % ✦ пула.</summary>
    public static int WizardSpellBonus(int mana, int pool)
    {
        if (pool <= 0) return 0;
        var missing = Math.Max(0, pool - mana);
        return Math.Min(4, missing * 4 / pool);
    }

    /// <summary>«Беглец» (Лунная порода): +1 урона атакам за каждую 0-энергетическую карту этого хода, до +3.</summary>
    public static int FugitiveBonus(int zeroCostCardsThisTurn) => Math.Clamp(zeroCostCardsThisTurn, 0, 3);

    /// <summary>«Ветроплясун» (Сыны Чащи): +1 урона атакам за каждую 3-ю карту хода.</summary>
    public static int WinddancerBonus(int cardsThisTurn) => Math.Max(0, cardsThisTurn) / 3;

    /// <summary>«Зверобой-духовидец»: дух волка → саблезуба → медведя по числу побеждённых элит.</summary>
    public static (int hp, int damage) BeastSpirit(int elitesDefeated) => elitesDefeated switch
    {
        <= 0 => (6, 3),
        1 => (12, 5),
        _ => (20, 7),
    };

    // ───── Латоносец: Горнокровные, Дюнные клинки, Цитадельцы (M4 п. 1) ─────

    /// <summary>«Ярость горна» (Горнокровные): после 3+ Атак за ход урон по герою в ход врага ×0.75.</summary>
    public const int ForgeFuryAttacks = 3;
    public const decimal ForgeFuryMultiplier = 0.75m;
    public static bool ForgeFuryActive(int attacksThisTurn) => attacksThisTurn >= ForgeFuryAttacks;

    /// <summary>«Ритм дюн» (Дюнные клинки): прирост ◆ +1 при ◆ ниже 75 % пула, +2 — ниже 50 %.</summary>
    public static int DuneRhythmRegen(int stamina, int pool)
    {
        if (pool <= 0) return 0;
        if (stamina * 2 < pool) return 2;
        if (stamina * 4 < pool * 3) return 1;
        return 0;
    }

    /// <summary>«Выучка» (Цитадельцы): +1 очко характеристик за каждые 3 уровня.</summary>
    public static int TrainingBonusPoints(int level) => Math.Clamp(level, 1, ProgressionRules.MaxLevel) / 3;

    /// <summary>«Заговорщик»: яды на оружии +25 % силы (округление вверх).</summary>
    public static int ConspiratorPotency(int potency) => potency <= 0 ? potency : (potency * 5 + 3) / 4;

    /// <summary>«Клинок-ансей»: Дух клинка добавляет атаке урон магией ⌊✦/2⌋ (считается до траты 1 ✦).</summary>
    public static int BladeSpiritBonus(int mana) => Math.Max(0, mana) / 2;

    /// <summary>«Дух света» (Брат Рассвета): урон светом случайному врагу в начале хода.</summary>
    public const int LightSpiritDamage = 2;

    /// <summary>«Верный-до-Конца» (Посланник): страж растёт с уровнем героя — ♥ 8 + 2 за уровень, удар 2 + 1 за каждые 4 уровня.</summary>
    public static (int hp, int damage) LoyalGuard(int level)
    {
        var l = Math.Clamp(level, 1, ProgressionRules.MaxLevel);
        return (8 + 2 * l, 2 + l / 4);
    }

    /// <summary>«Легионер»: Блок от карт +2 в первый ход боя, если в левой руке щит.</summary>
    public const int LegionaryFirstTurnBlock = 2;
}
