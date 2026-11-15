using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;

namespace SlayTheRequiem.SlayTheRequiemCode.Monsters;

/// <summary>
/// Тело призыва на поле (M6 п. 2, R-17): питомец героя (<c>PetOwner</c>, как Ости у Некробиндер) со своим спрайтом
/// <c>images/monsters/summon_*.png</c> и код-анимациями <see cref="OwnArtMonster"/>. Правила и ♥ — у силы-призыва
/// (<see cref="Bond"/>), существо показывает их: полоску ♥, намерение на ход («Удар N», «Дремлет»), удары и смерть.
/// <para>
/// Своих ходов у питомца нет (игра водит только врагов): намерение ставит сила (<see cref="Show"/>), а бьёт она же в конце
/// хода героя. Основа — <see cref="OwnArtMonster"/>, а не <c>BaseLib.CustomPetModel</c>: тот даёт лишь «ничего не делать» и
/// флаг взаимодействия, а нам нужны свой спрайт, анимации и намерения; питомцем существо делает <c>PlayerCmd.AddPet</c>.
/// </para>
/// </summary>
public abstract class SummonCreature : OwnArtMonster
{
    public const string StrikeMove = "SUMMON_STRIKE";
    public const string SlumberMove = "SUMMON_SLUMBER";

    /// <summary>♥ ставит сила сразу после появления (= её стаки).</summary>
    protected override int Hp => 1;

    /// <summary>Запасной облик, если PNG нет.</summary>
    protected override MonsterModel LookOf => ModelDb.Monster<MegaCrit.Sts2.Core.Models.Monsters.Osty>();

    protected override string? CastAnim => null;

    public override string? CustomDeathSfx => "event:/sfx/characters/osty/osty_die";

    /// <summary>Призыв — не враг: в Бестиарии его нет.</summary>
    public override bool ShouldShowInCompendium => false;

    public abstract SummonKind Kind { get; }

    /// <summary>Сила-призыв, чьё это тело (только в бою, не сохраняется).</summary>
    public SummonPower? Bond { get; set; }

    /// <summary>Значки намерения удара (урон — из силы, с надбавками).</summary>
    protected virtual AbstractIntent[] StrikeIntents() => [new SummonAttackIntent(() => Bond?.StrikeDamage ?? 0)];

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var strike = new MoveState(StrikeMove, _ => Task.CompletedTask, StrikeIntents());
        var slumber = new MoveState(SlumberMove, _ => Task.CompletedTask, new SleepIntent());
        strike.FollowUpState = strike;
        slumber.FollowUpState = strike;
        return new MonsterMoveStateMachine([strike, slumber], strike);
    }

    /// <summary>Показать намерение на этот ход (и снова проявить значок после прошлого удара).</summary>
    public void Show(SummonMove move)
    {
        var id = move == SummonMove.Slumber ? SlumberMove : StrikeMove;
        if (MoveStateMachine?.States.TryGetValue(id, out var state) == true && state is MoveState next)
            SetMoveImmediate(next, forceTransition: true);
    }
}

/// <summary>Намерение удара призыва: урон силы без модификаторов героя (ванильный расчёт брал бы герой целью).</summary>
public sealed class SummonAttackIntent : AttackIntent
{
    public SummonAttackIntent(Func<decimal> damage) => DamageCalc = damage;

    public override int Repeats => 1;

    protected override LocString IntentLabelFormat => new("intents", "FORMAT_DAMAGE_SINGLE");

    public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner) => Math.Max(0, (int)(DamageCalc?.Invoke() ?? 0m));

    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
    {
        var label = IntentLabelFormat;
        label.Add("Damage", GetTotalDamage(targets, owner));
        return label;
    }
}

// ───────────── существа ─────────────

/// <summary>Волк-дух (Призыв волка-духа, Зов духов Медиума, Колокольчик духов, Альфа оборотня).</summary>
public sealed class SpiritWolfSummon : SummonCreature
{
    public override SummonKind Kind => SummonKind.SpiritWolf;
    public override string ArtKey => "summon_spirit_wolf";
}

/// <summary>Дух зверя Зверобоя-духовидца: волк (0 элит).</summary>
public sealed class BeastWolfSummon : SummonCreature
{
    public override SummonKind Kind => SummonKind.BeastWolf;
    public override string ArtKey => "summon_beast_wolf";
}

/// <summary>Дух зверя: саблезуб (1 элита).</summary>
public sealed class BeastSabertoothSummon : SummonCreature
{
    public override SummonKind Kind => SummonKind.BeastSabertooth;
    public override string ArtKey => "summon_beast_sabertooth";
}

/// <summary>Дух зверя: медведь (2+ элиты).</summary>
public sealed class BeastBearSummon : SummonCreature
{
    public override SummonKind Kind => SummonKind.BeastBear;
    public override string ArtKey => "summon_beast_bear";
}

/// <summary>Огненный атронах: удар огнём + Горение.</summary>
public sealed class FlameAtronachSummon : SummonCreature
{
    public override SummonKind Kind => SummonKind.FlameAtronach;
    public override string ArtKey => "summon_flame_atronach";
    protected override AbstractIntent[] StrikeIntents() => [.. base.StrikeIntents(), new DebuffIntent()];
}

/// <summary>Ледяной атронах: принимает удары, атакующим — Озноб.</summary>
public sealed class FrostAtronachSummon : SummonCreature
{
    public override SummonKind Kind => SummonKind.FrostAtronach;
    public override string ArtKey => "summon_frost_atronach";
}

/// <summary>Грозовой атронах: разряд всем врагам (Дух молнии Юстициара).</summary>
public sealed class StormAtronachSummon : SummonCreature
{
    public override SummonKind Kind => SummonKind.StormAtronach;
    public override string ArtKey => "summon_storm_atronach";
}

/// <summary>Поднятый мертвец («Поднять мертвеца»).</summary>
public sealed class RaisedDeadSummon : SummonCreature
{
    public override SummonKind Kind => SummonKind.RaisedDead;
    public override string ArtKey => "summon_raised_dead";
}

/// <summary>Дремлющий страж («Страж Бездны»): первый ход — «Дремлет».</summary>
public sealed class AbyssGuardianSummon : SummonCreature
{
    public override SummonKind Kind => SummonKind.AbyssGuardian;
    public override string ArtKey => "summon_abyss_guardian";
}

/// <summary>Призрачный рыцарь («Зов Рыцаря-Духа»): пока стоит — атаки героя +2.</summary>
public sealed class SpectralKnightSummon : SummonCreature
{
    public override SummonKind Kind => SummonKind.SpectralKnight;
    public override string ArtKey => "summon_spectral_knight";
    protected override AbstractIntent[] StrikeIntents() => [.. base.StrikeIntents(), new BuffIntent()];
}

/// <summary>«Верный-до-Конца» (Посланник Цитадельцев): страж со щитом.</summary>
public sealed class LoyalGuardSummon : SummonCreature
{
    public override SummonKind Kind => SummonKind.LoyalGuard;
    public override string ArtKey => "summon_loyal_guard";
    protected override AbstractIntent[] StrikeIntents() => [.. base.StrikeIntents(), new DefendIntent()];
}

/// <summary>Костяной прислужник Лича.</summary>
public sealed class BoneThrallSummon : SummonCreature
{
    public override SummonKind Kind => SummonKind.BoneThrall;
    public override string ArtKey => "summon_bone_thrall";
}

// ───────────── раскладка на поле ─────────────

/// <summary>
/// Где стоят призывы: ванильная раскладка сажает питомцев на героя и выключает им наведение (так сделаны мелкие
/// питомцы-реликвии). Призывы встают перед героем, к врагам, в ряд (<see cref="SummonRules.LayoutX"/>), и на них можно навести.
/// </summary>
public static class SummonLayout
{
    public static void Arrange(Player player)
    {
        try
        {
            var room = NCombatRoom.Instance;
            var hero = room?.GetCreatureNode(player.Creature);
            if (room == null || hero == null) return;
            var summons = room.CreatureNodes
                .Where(n => n.Entity.PetOwner == player && n.Entity.Monster is SummonCreature && n.Entity.IsAlive)
                .ToList();
            var half = hero.Hitbox.Size.X * 0.5f;
            for (var i = 0; i < summons.Count; i++)
            {
                var node = summons[i];
                var target = new Vector2(hero.Position.X + SummonRules.LayoutX(half, i, player.IsOstyAlive), hero.Position.Y);
                if (node.Position.DistanceTo(target) > 1f && node.IsInsideTree())
                    node.CreateTween().TweenProperty(node, "position", target, 0.25)
                        .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                else node.Position = target;
                node.Visuals.Modulate = hero.Visuals.Modulate;
                room.SetCreatureIsInteractable(node.Entity, true);
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"SummonLayout: {e.Message}");
        }
    }
}

[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.AddCreature))]
public static class SummonAddCreaturePatch
{
    [HarmonyPostfix]
    public static void Postfix(Creature creature)
    {
        if (creature.Monster is SummonCreature && creature.PetOwner is { } owner) SummonLayout.Arrange(owner);
    }
}

[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.PositionPlayersAndPets))]
public static class SummonPositionPatch
{
    [HarmonyPostfix]
    public static void Postfix(List<NCreature> creatureNodes)
    {
        foreach (var node in creatureNodes.Where(n => n.Entity.IsPlayer).ToList())
            if (node.Entity.Player is { } player) SummonLayout.Arrange(player);
    }
}
