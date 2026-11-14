namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Тики стихийных статусов (docs/design/03).</summary>
public static class StatusMath
{
    /// <summary>Горение: урон = стаки, затем стаки делятся пополам (вниз).</summary>
    public static (int damage, int next) BurningTick(int stacks) => (Math.Max(0, stacks), Math.Max(0, stacks) / 2);

    /// <summary>
    /// Горение «плавит» Блок: каждая единица Горения снимает 2 Блока.
    /// Возвращает, сколько Блока сгорает и сколько урона проходит в ♥ (без участия обычного поглощения Блоком).
    /// </summary>
    public static (int blockLost, int hpDamage) BurningVsBlock(int damage, int block)
    {
        if (damage <= 0) return (0, 0);
        block = Math.Max(0, block);
        var absorbed = Math.Min(damage, block / 2);
        var blockLost = Math.Min(block, damage * 2);
        return (blockLost, damage - absorbed);
    }

    /// <summary>Суммарный урон Горения до затухания (для подсказки/прогноза).</summary>
    public static int BurningTotal(int stacks)
    {
        var total = 0;
        while (stacks > 0) { total += stacks; stacks /= 2; }
        return total;
    }

    /// <summary>Озноб: отнимает ◆ = стаки, затем стаки делятся пополам.</summary>
    public static (int staminaLoss, int next) ChillTick(int stacks) => (Math.Max(0, stacks), Math.Max(0, stacks) / 2);

    /// <summary>
    /// Статика: выжигает ✦ = стаки; на недостающую часть — урон разрядом («перегрузка»). Затем −1.
    /// </summary>
    public static (int manaLoss, int shockDamage, int next) StaticTick(int stacks, int mana)
    {
        stacks = Math.Max(0, stacks);
        var manaLoss = Math.Min(stacks, Math.Max(0, mana));
        return (manaLoss, stacks - manaLoss, Math.Max(0, stacks - 1));
    }

    /// <summary>Кровотечение: при атаке носителя — потеря ♥ = стаки, затем −1.</summary>
    public static (int hpLoss, int next) BleedOnAttack(int stacks) => (Math.Max(0, stacks), Math.Max(0, stacks - 1));

    /// <summary>Ванильный Яд (для сравнения в симуляторе): урон = стаки, затем −1.</summary>
    public static int PoisonTotal(int stacks) => stacks <= 0 ? 0 : stacks * (stacks + 1) / 2;

    /// <summary>Хворь: каждый тик Яда по носителю наносит +N урона.</summary>
    public static int PoisonTickWithBlight(int poisonStacks, int blight) => poisonStacks <= 0 ? 0 : poisonStacks + Math.Max(0, blight);
}
