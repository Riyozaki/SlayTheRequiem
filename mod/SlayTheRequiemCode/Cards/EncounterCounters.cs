using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.CardPools;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Monsters;
using SlayTheRequiem.SlayTheRequiemCode.Resources;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Cards;

/// <summary>Encounter-issued combat cards, never a permanent deck/reward/shop grant. No school or mana gate.
/// Retain keeps the answer available; normal tokens Exhaust, counter-shouts use the ordinary shout shelf.</summary>
public abstract class EncounterCounterCard(int energy) : RequiemCard(energy, CardType.Skill, CardRarity.Token, TargetType.Self)
{
    public override int MaxUpgradeLevel => 0;

    protected void InitCounter()
    {
        WithKeyword(CardKeyword.Retain);
        // Ordinary shout overflow follows Shouts: Discard when its normal shelf is full, not Exhaust.
        if (this is not IShoutCard) WithKeyword(CardKeyword.Exhaust);
    }

    protected T? Boss<T>() where T : SuperbossMonster => !IsMutable ? null : Owner?.Creature.CombatState?.Enemies
        .Where(c => c.IsAlive).Select(c => c.Monster).OfType<T>().FirstOrDefault();
}

/// <summary>One encounter-provided sidestep per owner turn, 0 energy / 1 stamina. Ordinary Skills remain a fallback.</summary>
[Pool(typeof(TokenCardPool))]
public sealed class EnvoySidestep : EncounterCounterCard, IRequiredStaminaCard
{
    public int RequiredStaminaCost => 1;
    private bool _paidThisSeries;
    public EnvoySidestep() : base(0) { InitCounter(); WithStamina(1); }
    protected override bool IsPlayable => Boss<GatebreakerEnvoy>() != null
        && (RequiemResource.Of<StaminaResource>(Owner)?.Amount ?? 0) >= RequiredStaminaCost;

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (play.IsFirstInSeries)
        {
            _paidThisSeries = false;
            if (RequiemResource.Of<StaminaResource>(Owner) is { } stamina && CombatState is { } combat)
                // BaseLib optional:true means fail without spending when short; false clamps to the remaining
                // amount and returns true even at zero. A failed first play must not become a free replay.
                _paidThisSeries = await stamina.Spend<StaminaResource>(combat, this, RequiredStaminaCost, optional: true);
        }
        if (_paidThisSeries) Boss<GatebreakerEnvoy>()?.Sidestep(Owner);
    }
}

/// <summary>The standard is a visible field object; carrying it closes gates and protects only this hero for the next enemy turn.</summary>
[Pool(typeof(TokenCardPool))]
public sealed class CarrySolarStandard : EncounterCounterCard
{
    public CarrySolarStandard() : base(2) => InitCounter();
    protected override bool IsPlayable => Boss<GatebreakerEnvoy>() is { HasStandard: true };
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (Boss<GatebreakerEnvoy>() is { HasStandard: true } boss) await boss.CarryStandard(ctx, Owner.Creature, this);
    }
}

/// <summary>A dedicated phase answer, not the Restoration spell: any hero can use it without a treatise or mana.</summary>
[Pool(typeof(TokenCardPool))]
public sealed class CocoonSunbeam : EncounterCounterCard
{
    public CocoonSunbeam() : base(1) => InitCounter();
    protected override bool IsPlayable => Boss<BloodCourtLord>() is { Cocooned: true };
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (Boss<BloodCourtLord>() is { } boss) await boss.BreakCocoon();
    }
}

[Pool(typeof(TokenCardPool))]
public sealed class TearTheWing : EncounterCounterCard, IShoutCard
{
    public int ShoutCooldown => BossCounterplayRules.CounterShoutCooldown;
    public TearTheWing() : base(1) => InitCounter();
    protected override bool IsPlayable => Boss<WorldEater>() is { Flying: true };
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (Boss<WorldEater>() is { } boss) await boss.Ground(ctx);
    }
}

[Pool(typeof(TokenCardPool))]
public sealed class ClearTheSkies : EncounterCounterCard, IShoutCard
{
    public int ShoutCooldown => BossCounterplayRules.CounterShoutCooldown;
    public ClearTheSkies() : base(1) => InitCounter();
    protected override bool IsPlayable => Boss<WorldEater>() is { MeteorThreat: true };
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (Boss<WorldEater>() is { } boss) await boss.ClearMeteors(ctx);
    }
}
