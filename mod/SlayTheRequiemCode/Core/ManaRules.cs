namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>
/// ✦ Мана (docs/design/02 §2). Технически ✦ — это звёзды игры у всех героев: цена ✦ на картах, проверка
/// «не хватает» и списание делает сама игра. Здесь — только числа пула и прироста.
/// </summary>
public static class ManaRules
{
    /// <summary>Бой начинается с половиной пула (вниз); прирост первого хода добавляется сверху.</summary>
    public static int StartAmount(int pool) => Math.Max(0, pool) / 2;

    /// <summary>
    /// Сколько ✦ дать приростом: до пула, но не отнимая сверх-пул (звёзды от ванильных карт Регента могут
    /// поднять ✦ выше пула — это не срезается).
    /// </summary>
    public static int RegenGain(int current, int pool, int regen) =>
        Math.Max(0, StaminaRules.Regenerate(current, pool, regen) - current);
}
