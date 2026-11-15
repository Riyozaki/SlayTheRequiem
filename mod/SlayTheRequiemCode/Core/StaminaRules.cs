namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>
/// Правила выносливости (docs/design/02 §1). Чистая логика без зависимостей от игры —
/// зеркало в tools/sim/requiem_rules.py покрыто тестами.
/// </summary>
public static class StaminaRules
{
    public const decimal TiredMultiplier = 0.75m;
    public const decimal ExhaustedMultiplier = 0.5m;

    /// <summary>Потеря ♥ за каждую недостающую единицу ◆ («надрыв»).</summary>
    public const int OverstrainHpPerPoint = 2;

    /// <summary>Состояние до оплаты атаки стоимостью <paramref name="cost"/>.</summary>
    public static StaminaState StateFor(int current, int pool, int cost)
    {
        if (cost > current) return StaminaState.Exhausted;
        if (pool > 0 && current * 2 < pool) return StaminaState.Tired;
        return StaminaState.Rested;
    }

    public static decimal DamageMultiplier(StaminaState state) => state switch
    {
        StaminaState.Rested => 1m,
        StaminaState.Tired => TiredMultiplier,
        _ => ExhaustedMultiplier,
    };

    /// <summary>Результат оплаты атаки.</summary>
    public readonly record struct Payment(int Spent, int Shortfall, int HpLoss, StaminaState State, decimal DamageMultiplier);

    /// <summary>
    /// Оплатить атаку. Атаку всегда можно сыграть: нехватка ◆ превращается в надрыв,
    /// поэтому выносливость — ресурс силы удара, а не второй блокиратор энергии.
    /// </summary>
    public static Payment Pay(int current, int pool, int cost)
    {
        if (cost < 0) cost = 0;
        var state = StateFor(current, pool, cost);
        var spent = Math.Min(current, cost);
        var shortfall = cost - spent;
        return new Payment(spent, shortfall, shortfall * OverstrainHpPerPoint, state, DamageMultiplier(state));
    }

    /// <summary>Прирост в начале хода, не выше пула.</summary>
    public static int Regenerate(int current, int pool, int regen) => Math.Clamp(current + regen, 0, Math.Max(pool, current));
}
