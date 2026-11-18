using System.Security.Cryptography;
using System.Text;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Shops;
using SlayTheRequiem.SlayTheRequiemCode.Spells;
using SlayTheRequiem.SlayTheRequiemCode.Transformations;

namespace SlayTheRequiem.SlayTheRequiemCode.Debugging;

/// <summary>
/// Invokes the REAL patched game factory, never a Python approximation. Fixtures have independent players/RNG,
/// no RunManager setup, no save/write/obtain commands. Run in a disposable diagnostic run with only BaseLib + mod:
/// third-party hooks can have global side effects even when passed an isolated RunState.
/// This is not a reward-screen, Add, draw or two-client acceptance test.
/// </summary>
public static class CardFactoryRegression
{
    private static bool _running;
    private sealed record Sample(string Hero, bool Unlocked, int Profile, HeroPath Path, int Act, int Party, string Seed);

    private static Player Fixture(Sample sample)
    {
        CharacterModel character = sample.Hero switch
        {
            "Silent" => ModelDb.Character<Silent>(), "Regent" => ModelDb.Character<Regent>(),
            _ => ModelDb.Character<Ironclad>(),
        };
        var unlocks = sample.Unlocked ? UnlockState.all : UnlockState.none;
        var player = Player.CreateForNewRun(character, unlocks, ulong.MaxValue);
        var players = new List<Player> { player };
        if (sample.Party == 2) players.Add(Player.CreateForNewRun(ModelDb.Character<Ironclad>(), unlocks, ulong.MaxValue - 1));
        if (players.Any(MegaCrit.Sts2.Core.Context.LocalContext.IsMe))
            throw new InvalidOperationException("Factory fixtures must not match the real local player");
        var run = RunState.CreateForTest(players, seed: sample.Seed);
        run.CurrentActIndex = sample.Act;
        var grimoire = Mastery.GrimoireOf(player) ?? throw new InvalidOperationException("Missing fixture Grimoire; systems must be enabled");
        foreach (var school in Mastery.Schools) grimoire.SetLevel(school, 0);
        if (sample.Profile == 16)
            foreach (var school in Mastery.Schools) grimoire.SetLevel(school, 5);
        else if (sample.Profile > 0)
            grimoire.SetLevel((SpellSchool)((sample.Profile - 1) / 3), new[] { 1, 3, 5 }[(sample.Profile - 1) % 3]);
        if (sample.Path != HeroPath.None)
            player.AddRelicInternal(ModelDb.GetById<RelicModel>(ModelDb.GetId(Paths.EssenceType(sample.Path))).ToMutable(), silent: true);
        if (players.Count > 1)
        {
            var other = players[1];
            if (ReferenceEquals(TravelPack.Of(player)!.StoredCards, TravelPack.Of(other)!.StoredCards)
                || ReferenceEquals(TravelPack.Of(player)!.PendingCards, TravelPack.Of(other)!.PendingCards)
                || ReferenceEquals(HerbalistPouch.Of(player)!.Reagents, HerbalistPouch.Of(other)!.Reagents)
                || ReferenceEquals(MerchantLedger.Of(player)!.Spent, MerchantLedger.Of(other)!.Spent))
                throw new InvalidOperationException("Player inventories share mutable clone fields");
        }
        return player;
    }

    private static string Roll(Sample sample, out int upgraded)
    {
        var player = Fixture(sample);
        var options = CardCreationOptions.ForRoom(player, RoomType.Monster).WithFlags(CardCreationFlags.IsCardReward);
        var cards = CardFactory.CreateForReward(player, 3, options).Select(r => r.Card).ToList();
        if (cards.Count != 3 || cards.Distinct().Count() != 3 || cards.Select(c => c.Id).Distinct().Count() != 3)
            throw new InvalidOperationException($"Invalid reward count/duplicates: {cards.Count}");
        foreach (var card in cards)
        {
            if (!card.IsMutable || card.Owner != player || card.HasBeenRemovedFromState || !player.RunState.ContainsCard(card))
                throw new InvalidOperationException($"Invalid model binding: {CardRewardTrace.Describe(card)}");
            if (!ResourceLoader.Exists(card.PortraitPath) || ResourceLoader.Load<Texture2D>(card.PortraitPath) is not { } texture
                || texture.GetWidth() <= 0 || texture.GetHeight() <= 0)
                throw new InvalidOperationException($"Portrait cannot be loaded: {card.Id} {card.PortraitPath}");
        }
        upgraded = cards.Count(c => c.IsUpgraded);
        return string.Join("|", cards.Select(c => $"{c.Id}:{c.CurrentUpgradeLevel}")) + $"|rng={player.PlayerRng.Rewards.Counter}";
    }

    public static async Task Run(bool full, int seeds)
    {
        if (_running) throw new InvalidOperationException("Factory suite already running");
        if (!Net.RequiemNet.IsSingleplayer || MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsInProgress
            || !RequiemMode.IsInSystem(RunManager.Instance.DebugOnlyGetState()?.Players.FirstOrDefault()) || Config.RunSettings.Active == null)
            throw new InvalidOperationException("Use a disposable singleplayer diagnostic run outside combat with enabled systems and a settings snapshot");
        if (MegaCrit.Sts2.Core.Modding.ModManager.GetLoadedMods().Any(m => m.manifest?.id is { } id && id != MainFile.ModId && id != "BaseLib"))
            throw new InvalidOperationException("Run the factory suite with only BaseLib + SlayTheRequiem");
        if (seeds is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(seeds));
        _running = true;
        var live = RunManager.Instance.DebugOnlyGetState();
        var trace = CardRewardTrace.Enabled;
        CardRewardTrace.Enabled = false; // per-stage traces are for reproductions, not tens of thousands of fixture models
        try
        {
            RunDiagnostics.Log(live, "factory-suite");
            var cases = 0; var failed = 0; var upgraded = 0;
            var fingerprints = new StringBuilder();
            var profiles = full ? Enumerable.Range(0, 17).ToArray() : new[] { 0, 1, 16 };
            var paths = full ? Enum.GetValues<HeroPath>() : new[] { HeroPath.None, HeroPath.Paladin, HeroPath.ElementLord };
            foreach (var hero in new[] { "Ironclad", "Silent", "Regent" })
            foreach (var unlocked in new[] { false, true })
            foreach (var profile in profiles)
            foreach (var path in paths)
            foreach (var act in new[] { 0, 2 })
            foreach (var party in new[] { 1, 2 })
            for (var seed = 0; seed < seeds; seed++)
            {
                if (!ReferenceEquals(live, RunManager.Instance.DebugOnlyGetState())) throw new InvalidOperationException("Run changed; suite aborted");
                var sample = new Sample(hero, unlocked, profile, path, act, party, $"STR-G1-{seed:D3}");
                cases++;
                try
                {
                    var first = Roll(sample, out var n);
                    var replay = Roll(sample, out _);
                    if (first != replay) throw new InvalidOperationException($"Non-deterministic replay: {first} != {replay}");
                    upgraded += n;
                    fingerprints.Append(sample).Append('=').Append(first).Append('\n');
                }
                catch (Exception e)
                {
                    failed++;
                    MainFile.Logger.Error($"[CardFactorySuite] FAIL case={sample}: {e}");
                }
                if (cases % 16 == 0) await Task.Yield();
            }
            if (upgraded == 0) { failed++; MainFile.Logger.Error("[CardFactorySuite] No upgraded variants exercised"); }
            var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprints.ToString())));
            MainFile.Logger.Info($"[CardFactorySuite] mode={(full ? "full" : "smoke")} seeds={seeds} cases={cases} failures={failed} upgraded={upgraded} fingerprint={digest} " +
                "real_factory=true isolated=true reward_ui=false add=false draw=false two_clients=false");
            if (failed > 0) throw new InvalidOperationException($"Factory suite failed {failed} checks; see [CardFactorySuite]");
        }
        finally { CardRewardTrace.Enabled = trace; _running = false; }
    }
}
