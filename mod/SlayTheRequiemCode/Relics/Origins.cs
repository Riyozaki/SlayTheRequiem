using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Spells;

namespace SlayTheRequiem.SlayTheRequiemCode.Relics;

/// <summary>
/// Происхождение (docs/design/04) — стартовая реликвия-«паспорт»: правка статов и пассив. Не продаётся и не выпадает.
/// ♥ правится при получении; пулы ◆/✦ — через <see cref="IResourceSource"/>; сопротивления накладываются в начале боя.
/// </summary>
public abstract class OriginRelic : RequiemRelic, IResourceSource
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override bool IsAllowedInShops => false;
    public override bool IsAllowed(IRunState runState) => false;

    protected virtual int MaxHpDelta => 0;
    public virtual int StaminaPoolBonus => 0;
    public virtual int ManaPoolBonus => 0;
    public virtual int ManaRegenBonus => 0;
    public virtual int ManaStartBonus => 0;

    /// <summary>Сопротивления происхождения (тип силы, ступень; отрицательная = уязвимость).</summary>
    protected virtual IEnumerable<(Type power, int tier)> Resistances => [];

    public override async Task AfterObtained()
    {
        if (MaxHpDelta > 0) await CreatureCmd.GainMaxHp(Owner.Creature, MaxHpDelta);
        else if (MaxHpDelta < 0) await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), Owner.Creature, -MaxHpDelta, false);
    }

    public override async Task BeforeCombatStart()
    {
        foreach (var (power, tier) in Resistances)
        {
            var model = ModelDb.GetById<PowerModel>(ModelDb.GetId(power)).ToMutable();
            await PowerCmd.Apply(new ThrowingPlayerChoiceContext(), model, Owner.Creature, tier, Owner.Creature, null, silent: true);
        }
    }

    // ───── выдача по настройке ─────

    public static OriginRelic? ForRegent(Player player)
    {
        var choice = RequiemConfig.RegentOrigin;
        if (choice == RegentOriginChoice.Random)
            choice = (RegentOriginChoice)(1 + player.PlayerRng.Rewards.NextInt(3));
        RelicModel? canonical = choice switch
        {
            RegentOriginChoice.Highborn => ModelDb.Relic<HighbornOrigin>(),
            RegentOriginChoice.Mistborn => ModelDb.Relic<MistbornOrigin>(),
            RegentOriginChoice.Ashborn => ModelDb.Relic<AshbornOrigin>(),
            _ => null,
        };
        return (OriginRelic?)canonical?.ToMutable();
    }
}

/// <summary>
/// Златокровные (альтмеры): ✦ пул +3, ♥ −5. «Высокая кровь» — первое заклинание каждого хода на 1 ✦ дешевле.
/// Минус: уязвимость к магии I.
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class HighbornOrigin : OriginRelic
{
    protected override int MaxHpDelta => -5;
    public override int ManaPoolBonus => 3;
    protected override IEnumerable<(Type power, int tier)> Resistances => [(typeof(MagicResistancePower), -1)];

    private bool _spellCastThisTurn;

    public override bool ShowCounter => false;

    public override bool TryModifyStarCost(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (_spellCastThisTurn || card.Owner != Owner || card is not SpellCard || originalCost <= 0) return false;
        modifiedCost = originalCost - 1;
        return true;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner && cardPlay.Card is SpellCard) _spellCastThisTurn = true;
        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner) _spellCastThisTurn = false;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Туманные (бретонцы): ◆ +1. «Туманная кровь» — сопротивление магии I; первое вражеское заклинание в бою
/// поглощается и даёт ✦.
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class MistbornOrigin : OriginRelic
{
    public override int StaminaPoolBonus => 1;
    protected override IEnumerable<(Type power, int tier)> Resistances => [(typeof(MagicResistancePower), 1)];

    public override async Task BeforeCombatStart()
    {
        await base.BeforeCombatStart();
        await PowerCmd.Apply<MistbloodPower>(new ThrowingPlayerChoiceContext(), Owner.Creature, 1, Owner.Creature, null, silent: true);
    }
}

/// <summary>
/// Пеплорождённые (данмеры): ♥ +5, сопротивление огню I. Дары Тройственных — с системой Князей Бездны (этап 5).
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class AshbornOrigin : OriginRelic
{
    protected override int MaxHpDelta => 5;
    protected override IEnumerable<(Type power, int tier)> Resistances => [(typeof(FireResistancePower), 1)];
}
