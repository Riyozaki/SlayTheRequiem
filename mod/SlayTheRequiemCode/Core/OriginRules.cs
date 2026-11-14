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
}
