namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Груз: вес карт и снаряжения против вместимости (docs/design/02 §3).</summary>
public static class BurdenRules
{
    public readonly record struct Overload(int Excess, int StaminaRegenPenalty, int OpeningHandPenalty, bool CanPickUp);

    public static Overload Evaluate(int totalWeight, int capacity)
    {
        var excess = Math.Max(0, totalWeight - capacity);
        return excess switch
        {
            0 => new Overload(0, 0, 0, true),
            <= 5 => new Overload(excess, 1, 0, true),
            <= 10 => new Overload(excess, 1, 1, true),
            _ => new Overload(excess, 1, 1, false),
        };
    }

    /// <summary>Вес карты по умолчанию: базовые/обычные 1, необычные 1, редкие 2, проклятия 2, статусы 0.</summary>
    public static int DefaultCardWeight(string rarity, bool isCurse, bool isStatus)
    {
        if (isStatus) return 0;
        if (isCurse) return 2;
        return rarity switch
        {
            "Rare" or "Ancient" => 2,
            _ => 1,
        };
    }
}
