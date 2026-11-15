namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>
/// Кличи (docs/design/04 «Глас»): после розыгрыша Клич уходит на полку и через N ходов возвращается в руку.
/// На полке одновременно — 1 Клич (Скальд — 2); если полка занята, Клич уходит в сброс как обычная карта.
/// </summary>
public static class ShoutRules
{
    public const int MinCooldown = 1;

    /// <summary>Перезарядка с учётом сокращений (Знамение Гласа, Крылатый Час, ...), не меньше 1 хода.</summary>
    public static int Cooldown(int baseCooldown, int reduction) => Math.Max(MinCooldown, baseCooldown - Math.Max(0, reduction));

    /// <summary>Сколько Кличей может перезаряжаться одновременно.</summary>
    public static int ShelfCapacity(bool skald) => skald ? 2 : 1;

    /// <summary>Разрыв души: добивает не-босса с ♥ ниже 25 %.</summary>
    public static bool SoulTearExecutes(int hp, int maxHp, bool boss) => !boss && hp > 0 && hp * 4 < maxHp;
}
