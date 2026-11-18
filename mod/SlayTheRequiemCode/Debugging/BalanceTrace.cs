using System.Runtime.CompilerServices;
using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Progression;
using SlayTheRequiem.SlayTheRequiemCode.Relics;
using SlayTheRequiem.SlayTheRequiemCode.Resources;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Shops;
using SlayTheRequiem.SlayTheRequiemCode.Spells;
using SlayTheRequiem.SlayTheRequiemCode.Transformations;

namespace SlayTheRequiem.SlayTheRequiemCode.Debugging;

/// <summary>Opt-in actual-engine observations for the FINAL playtest, enabled by str_trace on.
/// No gameplay writes, selectors, CanPlay re-query, game RNG draw, identity repair or periodic/frame sampling.
/// The logger writes rows; first artifact lookup also reads/hashes DLL/PCK/passport files before caching them.
/// Opt-in diagnostics allocate/serialize and are NOT performance-neutral. Boundary labels are explicit:
/// victory is PRE reward collection; room entry is not a second reward.
/// Logs contain run/owner ids just like str_passport; nothing is uploaded automatically.</summary>
public static class BalanceTrace
{
    private sealed class Sequence
    {
        internal readonly string Segment = Guid.NewGuid().ToString("N");
        internal long Value;
    }
    private static readonly ConditionalWeakTable<Player, Sequence> Sequences = new();

    private static object Enemy(Creature creature) => new
    {
        model = creature.Monster?.GetType().Name, role = EnemySetup.ObserveRole(creature).ToString(),
        hp = creature.CurrentHp, max_hp = creature.MaxHp,
        raw_min_hp = creature.Monster?.MinInitialHp, raw_max_hp = creature.Monster?.MaxInitialHp,
        plating = creature.GetPowerAmount<Powers.PlatingPower>(),
        stamina = creature.GetPowerAmount<Powers.EnemyStaminaPower>(), mana = creature.GetPowerAmount<Powers.EnemyManaPower>(),
    };

    public static void Snapshot(string phase, Player player, string detail = "")
    {
        if (!CardRewardTrace.Enabled || !RequiemMode.IsInSystem(player)) return;
        try
        {
            var run = player.RunState;
            var settings = RunSettings.Active;
            var sequence = Sequences.GetValue(player, _ => new Sequence());
            var hero = HeroRecord.Of(player);
            var pack = TravelPack.Of(player);
            var overload = Loadout.Overload(player);
            var stamina = RequiemResource.Of<StaminaResource>(player);
            var ledger = MerchantLedger.Of(player);
            var route = ShopEconomy.Context(run);
            var row = new
            {
                schema = 1, producer = "native-game", phase, detail,
                segment = sequence.Segment, sequence = Interlocked.Increment(ref sequence.Value),
                run_id = settings?.RunId ?? "", seed = run.Rng.StringSeed, launch = RunDiagnostics.LaunchKind,
                player = player.NetId.ToString(System.Globalization.CultureInfo.InvariantCulture), hero = player.Character.GetType().Name,
                act_index = run.CurrentActIndex, act_count = run.Acts.Count, route_slot = route.Slot,
                floor = run.TotalFloor, room = run.CurrentRoom?.RoomType.ToString(), round = player.Creature.CombatState?.RoundNumber ?? 0,
                economy_revision = RunSettings.EconomyRevision, requiem_level = Acts.RequiemLevel.Of(player), ascension = run.AscensionLevel,
                gold = player.Gold, hp = player.Creature.CurrentHp, max_hp = player.Creature.MaxHp,
                renown = hero?.Renown ?? 0, level = hero?.Level ?? 1,
                stat_points_spent = hero?.StatPointsSpent ?? 0, skill_points_spent = hero?.SkillPointsSpent ?? 0,
                weight = Loadout.TotalWeight(player), capacity = Loadout.Capacity(player), excess = overload.Excess,
                stamina_regen_penalty = overload.StaminaRegenPenalty, opening_hand_penalty = overload.OpeningHandPenalty,
                energy = player.PlayerCombatState?.Energy ?? 0,
                stamina = stamina == null ? null : new { amount = stamina.Amount, pool = stamina.Pool, regen = stamina.Regen },
                mana = new { amount = Mana.Amount(player), pool = Mana.Pool(player), regen = Mana.Regen(player) },
                mastery = Mastery.Schools.Select(s => new { school = s.ToString(), level = Mastery.Of(player, s) }).ToArray(),
                origin = player.Relics.OfType<OriginRelic>().FirstOrDefault()?.GetType().Name,
                background = player.Relics.OfType<BackgroundRelic>().FirstOrDefault()?.GetType().Name,
                forms = Forms.All(player).Select(f => f.Kind.ToString()).ToArray(), path = Paths.Of(player)?.Path.ToString(),
                camp_points = hero?.CampPoints ?? 0, stored_count = pack?.StoredCount ?? 0, pending_cards = pack?.PendingCards.Count ?? 0,
                operation_timeline = pack?.OperationRunId ?? "", operation_revision = pack?.OperationRevision ?? 0,
                operation_blocked = InventoryGate.ObserveBlocked(player),
                operation_pending = pack?.OperationPending ?? "", operation_mutating = InventoryGate.IsMutating(player),
                shop = ledger?.CurrentShop.ToString(), trust = ledger?.CurrentTrust ?? 0,
                reward_rng = player.PlayerRng.Rewards.Counter, shop_rng = player.PlayerRng.Shops.Counter,
                deck = player.Deck.Cards.Select(c => new { model = c.GetType().Name, rarity = c.Rarity.ToString(), weight = Loadout.CardWeight(c) }).ToArray(),
                hand = player.PlayerCombatState?.Hand.Cards.Select(c => new { model = c.GetType().Name, rarity = c.Rarity.ToString() }).ToArray(),
                equipment = Loadout.All(player).Select(e => new { model = e.GetType().Name, tier = e.Tier, upgrades = e.Upgrades, weight = e.Weight, equipped = e.IsEquipped }).ToArray(),
                enemies = player.Creature.CombatState?.Enemies.Select(Enemy).ToArray(),
                settings = settings?.Encode(), artifacts = RunDiagnostics.BinaryIdentity(),
            };
            MainFile.Logger.Info("[RequiemBalance] " + JsonSerializer.Serialize(row));
        }
        catch (Exception e) { MainFile.Logger.Warn($"[RequiemBalance] Observation failed (no gameplay retry): {e.GetType().Name}: {e.Message}"); }
    }

    internal static void EnemyReady(Creature creature, MegaCrit.Sts2.Core.Runs.IRunState? run, EnemyRole role,
        int actHpBonus, decimal hpMultiplier, int hpBefore, int hpAfterScale, bool hpScaleRequested)
    {
        if (!CardRewardTrace.Enabled) return;
        try
        {
            MainFile.Logger.Info("[RequiemBalanceEnemy] " + JsonSerializer.Serialize(new
            {
                schema = 1, producer = "native-game", phase = "enemy:after-setup", enemy = Enemy(creature),
                run_id = RunSettings.Active?.RunId ?? "", seed = run?.Rng.StringSeed,
                act_index = run?.CurrentActIndex, act_count = run?.Acts.Count, floor = run?.TotalFloor,
                role = role.ToString(), scales_hp = EnemyRoleRules.ScalesHp(role),
                hp_before = hpBefore, hp_after_scale = hpAfterScale, hp_scale_requested = hpScaleRequested,
                act_hp_bonus = actHpBonus, policy_hp_multiplier = hpMultiplier,
                economy_revision = RunSettings.EconomyRevision, configured_hp_percent = RunSettings.EnemyHpPercent,
                requiem_level = Acts.RequiemLevel.Of(run), artifacts = RunDiagnostics.BinaryIdentity(),
            }));
        }
        catch (Exception e) { MainFile.Logger.Warn($"[RequiemBalanceEnemy] Observation failed: {e.Message}"); }
    }
}
