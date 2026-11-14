using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>Цена атак в ◆: ручная разметка ванильных атак + собственные карты мода.</summary>
public static class StaminaCosts
{
    public const string CsvPath = MainFile.ResPath + "/data/vanilla_attacks_stamina.csv";

    private static StaminaTable? _table;

    public static int Count => _table?.Count ?? 0;

    public static void Load()
    {
        try
        {
            using var file = Godot.FileAccess.Open(CsvPath, Godot.FileAccess.ModeFlags.Read);
            if (file == null)
            {
                MainFile.Logger.Error($"Cannot open {CsvPath}: {Godot.FileAccess.GetOpenError()}");
                return;
            }
            _table = StaminaTable.Parse(file.GetAsText());
        }
        catch (Exception e)
        {
            MainFile.Logger.Error($"Failed to load stamina table: {e}");
        }
    }

    /// <summary>Своя цена ◆ у карты мода (см. IStaminaCard).</summary>
    public interface IStaminaCard
    {
        int StaminaCost { get; }
    }

    /// <summary>
    /// Цена атаки в ◆. X-атаки платят по 1 ◆ за каждую единицу X (= потраченная энергия).
    /// Неразмеченные атаки (другие моды) — по классу удара «обычный» = 1 ◆.
    /// </summary>
    public static int CostFor(CardModel card, CardPlay? play = null)
    {
        if (card is IStaminaCard custom) return Math.Max(0, custom.StaminaCost);
        if (_table != null && _table.TryGet(card.GetType().Name, out var entry))
            return entry.IsX ? Math.Max(0, play?.Resources.EnergyValue ?? 0) : entry.Stamina;
        return 1;
    }

    public static DamageType DamageTypeFor(CardModel card) =>
        _table != null && _table.TryGet(card.GetType().Name, out var entry) ? entry.DamageType : DamageType.Physical;
}
