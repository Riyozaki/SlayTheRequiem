using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Monsters;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Spells;

namespace SlayTheRequiem.SlayTheRequiemCode.Debugging;

/// <summary>Native API/Harmony fixture source. Run by str_integrity in the final legal-game automation;
/// isolated models, never changes the live combat manager. Not a simulated full battle or a disk-save test.</summary>
public static partial class IntegrityRegression
{
    private static (MegaCrit.Sts2.Core.Entities.Players.Player Owner, CombatState Combat, Creature Enemy) CombatFixture<T>() where T : MonsterModel
    {
        var owner = Fixture(); owner.ResetCombatState();
        var combat = new CombatState(runState: owner.RunState); combat.AddPlayer(owner);
        var enemy = combat.CreateCreature((MonsterModel)ModelDb.Monster<T>().ToMutable(), CombatSide.Enemy, null);
        combat.AddCreature(enemy);
        return (owner, combat, enemy);
    }
    private static T FixturePower<T>(Creature target, int amount) where T : PowerModel
    {
        var power = (T)ModelDb.Power<T>().ToMutable();
        power.ApplyInternal(target, amount, silent: true);
        return power;
    }

    private static Task CombatNativeArmor()
    {
        var (owner, combat, enemy) = CombatFixture<BarrowDead>();
        var plating = FixturePower<PlatingPower>(enemy, 3);
        var card = combat.CreateCard<MegaCrit.Sts2.Core.Models.Cards.StrikeIronclad>(owner);
        decimal Query(DamageType type, decimal amount = 10m)
        {
            using var element = DamageTyping.As(type);
            return Hook.ModifyDamage(owner.RunState, combat, enemy, owner.Creature, amount, ValueProp.Move, card,
                ModifyDamageHookType.All, CardPreviewMode.None, out _);
        }
        Require(Query(DamageType.Physical) == 7m, "Shared native armor stage not installed");
        FixturePower<SunderPower>(enemy, 5);
        Require(Query(DamageType.Physical) == 12m, "Sunder not evaluated with armor");
        Require(Query(DamageType.True) == 10m, "True used armor/Sunder");
        var cap = FixturePower<IntangiblePower>(enemy, 1);
        Require(Query(DamageType.Physical) == 1m && Query(DamageType.True) == 1m, "Armor/Sunder ran after native cap or True skipped it");
        cap.RemoveInternal(); plating.RemoveInternal();
        Require(Query(DamageType.Physical, 0m) == 0m, "Sunder raised an immune/zero hit");
        return Task.CompletedTask;
    }

    private static async Task CombatHpPreview()
    {
        var (owner, combat, _) = CombatFixture<BarrowDead>();
        var shield = FixturePower<SpellShieldPower>(owner.Creature, 6);
        var before = shield.Amount;
        var dealer = combat.Enemies[0];
        decimal Query()
        {
            using var element = DamageTyping.As(DamageType.Fire);
            return Hook.ModifyHpLost(owner.RunState, combat, owner.Creature, 9m, ValueProp.Move, dealer, null, HpLossHookPhase.All, out _);
        }
        Require(Query() == 3m && Query() == 3m && shield.Amount == before, "HP preview spent/reserved spell ward");
        await HitDefensePipeline.Commit();
        Require(shield.Amount == before, "Pure preview queued an actual receipt");
        var frame = HitDefensePipeline.Enter();
        try { Require(Query() == 3m, "Actual HP defense plan differs from preview"); }
        finally { HitDefensePipeline.Restore(frame); HitDefensePipeline.Cancel(frame); }
        Require(Query() == 3m && shield.Amount == before, "Abandoned damage leaked shield reservation");
    }

    private static async Task CombatAbsorption()
    {
        var (owner, combat, enemy) = CombatFixture<BarrowDead>();
        var absorption = FixturePower<SpellAbsorptionPower>(enemy, 2);
        var fire = combat.CreateCard<Firebolt>(owner);
        SpellInterception.Begin(fire);
        Require(SpellInterception.Predict(enemy, ValueProp.Move | ValueProp.Unpowered, owner.Creature, fire), "Spell preview omitted absorption");
        Require(absorption.Amount == 2, "Preview spent absorption");
        var ctx = new ThrowingPlayerChoiceContext();
        Require(await SpellInterception.InterceptEffect(ctx, enemy, owner.Creature, fire), "Direct spell effect bypassed absorption");
        Require(await SpellInterception.InterceptEffect(ctx, enemy, owner.Creature, fire) && absorption.Amount == 1, "Multihit/status spent two charges");
        var light = combat.CreateCard<Sunbeam>(owner);
        SpellInterception.Begin(light);
        Require(!SpellInterception.Predict(enemy, ValueProp.Move | ValueProp.Unpowered, owner.Creature, light), "Light did not bypass absorption");
        Require(!await SpellInterception.InterceptEffect(ctx, enemy, owner.Creature, light) && absorption.Amount == 1, "Light spent absorption");
        SpellInterception.End(fire); SpellInterception.Begin(fire);
        Require(SpellInterception.Predict(enemy, ValueProp.Move | ValueProp.Unpowered, owner.Creature, fire), "Replay reused prior cast state");
        SpellInterception.End(fire); SpellInterception.End(light);
    }

    private static Task CombatSummonRole()
    {
        var (_, _, enemy) = CombatFixture<Dremora>();
        Require(EnemySetup.RoleOf(enemy) == EnemyRole.Normal, "Standalone rift auto-marked secondary");
        using (EnemySetup.SummonScope(typeof(Dremora)))
            Require(EnemySetup.RoleOf(enemy) == EnemyRole.Minion, "Summon setup inherited Normal before late native marker");
        Require(EnemySetup.RoleOf(enemy) == EnemyRole.Minion, "Declared summon marker did not survive scope");
        var (_, _, prop) = CombatFixture<SteamValve>();
        FixturePower<MinionPower>(prop, 1);
        Require(EnemySetup.RoleOf(prop) == EnemyRole.Prop && !EnemyRoleRules.UsesResources(EnemySetup.RoleOf(prop)), "Prop lost its object role");
        return Task.CompletedTask;
    }

    private static Task CombatActionPrice()
    {
        var (_, _, enemy) = CombatFixture<BarrowReaper>();
        var stamina = FixturePower<EnemyStaminaPower>(enemy, 5);
        var mana = FixturePower<EnemyManaPower>(enemy, 8);
        enemy.Monster!.SetUpForCombat(); enemy.Monster.RollMove([]);
        var price = EnemyActionBudget.CostOf(enemy);
        EnemyActionBudget.Begin(enemy.Monster);
        EnemyActionBudget.Pay(enemy); var first = mana.DisplayAmount;
        EnemyActionBudget.Pay(enemy);
        Require(first == 8 - price && mana.DisplayAmount == first && stamina.DisplayAmount == 5,
            "Enemy paid per command/target or paid both resources");
        Require(EnemyActionBudget.ManaDamageMultiplier(enemy, first) == 1m, "Price changed damage multiplier after payment");
        EnemyActionBudget.End(enemy);
        return Task.CompletedTask;
    }
}
