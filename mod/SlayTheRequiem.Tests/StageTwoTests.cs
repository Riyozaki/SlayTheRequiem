using System.Text.Json;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using Xunit;

namespace SlayTheRequiem.Tests;

public class MasterySourceTests
{
    private static readonly int[] NoIdentity = [0, 0, 0, 0, 0];

    [Theory]
    [InlineData("Ironclad", 0, 0, 0, 0, 0)]
    [InlineData("Silent", 0, 1, 1, 0, 1)]
    [InlineData("Regent", 2, 1, 1, 1, 1)]
    public void New_hero_base_is_an_explicit_nonrefundable_source(string hero, int d, int r, int a, int c, int i)
    {
        var sources = MasterySources.ForNewHero(hero);
        sources.RequireCounters([d, r, a, c, i]);
        Assert.True(sources.IdentityKnown);
        Assert.Equal(0, sources.StatPoints);
        Assert.Equal(sources.Encode(), MasterySources.Read(sources.Encode()).Encode());
    }

    [Fact]
    public void Respec_removes_only_paid_levels_and_refunds_the_historical_price()
    {
        var sources = MasterySources.ForNewHero("Ironclad");
        Assert.True(sources.TryBuyStat(SpellSchool.Destruction, 2, out sources));
        Assert.True(sources.TryBuyStat(SpellSchool.Restoration, 1, out sources));
        Assert.True(sources.TryRaiseAcquired(SpellSchool.Destruction, out sources));
        sources = sources.WithSkill(Skill.Destruction1);
        var saved = MasterySources.Read(sources.Encode());
        Assert.Equal(3, saved.StatPoints);
        Assert.Equal(3, saved.Level(SpellSchool.Destruction));
        var reborn = saved.WithoutStatPurchases();
        Assert.Equal(0, reborn.StatPoints);
        Assert.Equal(2, reborn.Level(SpellSchool.Destruction)); // treatise + retained learned skill
        Assert.Equal(0, reborn.Level(SpellSchool.Restoration));
        Assert.Equal(reborn.Encode(), reborn.WithoutStatPurchases().Encode());
        Assert.Equal(3, saved.StatPoints); // immutable source; no aliasing into the checkpoint
    }

    [Fact]
    public void Identity_change_cannot_make_a_refund_use_the_new_cheaper_price()
    {
        var sources = MasterySources.ForNewHero("Ironclad");
        Assert.True(sources.TryBuyStat(SpellSchool.Restoration, 2, out sources));
        sources = sources.WithIdentity(NoIdentity, [0, 1, 0, 0, 0]);
        Assert.True(sources.IsIdentitySchool(SpellSchool.Restoration));
        Assert.Equal(2, sources.StatPoints);
        var reborn = sources.WithoutStatPurchases().WithIdentity(NoIdentity, NoIdentity);
        Assert.Equal(0, reborn.Level(SpellSchool.Restoration));
        Assert.False(reborn.IsIdentitySchool(SpellSchool.Restoration));
    }

    [Fact]
    public void Acquired_mastery_does_not_turn_a_foreign_school_into_a_native_one()
    {
        var sources = MasterySources.ForNewHero("Ironclad");
        Assert.True(sources.TryRaiseAcquired(SpellSchool.Illusion, out sources));
        Assert.False(sources.IsIdentitySchool(SpellSchool.Illusion));
        Assert.True(sources.WithIdentity(NoIdentity, [0, 0, 0, 0, 1]).IsIdentitySchool(SpellSchool.Illusion));
    }

    [Fact]
    public void Skill_source_is_idempotent_even_at_the_cap_and_survives_stat_refund()
    {
        var sources = MasterySources.ForNewHero("Regent");
        Assert.True(sources.TryBuyStat(SpellSchool.Destruction, 1, out sources));
        sources = sources.WithSkill(Skill.Destruction1).WithSkill(Skill.Destruction2);
        Assert.Equal(5, sources.Level(SpellSchool.Destruction));
        sources = sources.WithSkill(Skill.Destruction3);
        var encoded = sources.Encode();
        Assert.Equal(encoded, sources.WithSkill(Skill.Destruction3).Encode());
        Assert.False(sources.TryBuyStat(SpellSchool.Destruction, 1, out _));
        Assert.False(sources.TryRaiseAcquired(SpellSchool.Destruction, out _));
        Assert.Equal(5, sources.WithoutStatPurchases().Level(SpellSchool.Destruction)); // base 2 + three retained nodes
        Assert.Equal(1, sources.StatPoints);
    }

    [Fact]
    public void Base_and_identity_contributions_remain_distinct_from_stat_and_treatise()
    {
        var sources = MasterySources.ForNewHero("Regent").WithIdentity([1, 0, 0, 0, 0], [0, 0, 0, 1, 0]);
        Assert.True(sources.TryBuyStat(SpellSchool.Destruction, 1, out sources));
        Assert.True(sources.TryRaiseAcquired(SpellSchool.Conjuration, out sources));
        var next = sources.WithoutStatPurchases().WithIdentity([0, 1, 0, 0, 0], NoIdentity);
        next.RequireCounters([2, 2, 1, 2, 1]); // new origin + base + permanent treatise, old background removed
        Assert.Equal(4, sources.Level(SpellSchool.Destruction));
    }

    [Fact]
    public void Legacy_counters_are_preserved_without_guessing_paid_or_identity_sources()
    {
        var legacy = MasterySources.PreserveLegacy([4, 1, 0, 3, 0]);
        var restored = MasterySources.Read(legacy.Encode());
        restored.RequireCounters([4, 1, 0, 3, 0]);
        Assert.False(restored.IdentityKnown);
        Assert.Equal(0, restored.StatPoints);
        Assert.Throws<InvalidOperationException>(() => restored.WithIdentity(NoIdentity, NoIdentity));
        Assert.True(restored.TryBuyStat(SpellSchool.Illusion, 2, out var next));
        next.WithoutStatPurchases().RequireCounters([4, 1, 0, 3, 0]);
    }

    [Fact]
    public void Journal_and_plain_saved_counters_must_agree_before_a_refund()
    {
        var sources = MasterySources.ForNewHero("Silent");
        Assert.Throws<FormatException>(() => sources.RequireCounters([5, 5, 5, 5, 5]));
        Assert.Throws<FormatException>(() => sources.RequireCounters([0, 1]));
        Assert.Throws<FormatException>(() => MasterySources.PreserveLegacy([-1, 0, 0, 0, 0]));
        Assert.Throws<FormatException>(() => MasterySources.PreserveLegacy([6, 0, 0, 0, 0]));
        Assert.Throws<ArgumentException>(() => MasterySources.ForNewHero("Necrobinder"));
        Assert.Throws<ArgumentOutOfRangeException>(() => sources.TryBuyStat(SpellSchool.Destruction, 0, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"Version\":99,\"IdentityKnown\":true,\"Grants\":[]}")]
    [InlineData("{\"Version\":1,\"IdentityKnown\":true,\"Grants\":[],\"Future\":1}")]
    [InlineData("{\"Version\":1,\"Version\":1,\"IdentityKnown\":true,\"Grants\":[]}")]
    [InlineData("{\"Version\":1,\"Grants\":[]}")]
    [InlineData("{\"Version\":1,\"IdentityKnown\":true,\"Grants\":[null]}")]
    [InlineData("{\"Version\":1,\"IdentityKnown\":true,\"Grants\":[{\"Source\":\"future\",\"School\":0,\"Levels\":1}]}")]
    [InlineData("{\"Version\":1,\"IdentityKnown\":true,\"Grants\":[{\"Source\":\"legacy\",\"School\":0,\"Levels\":1}]}")]
    [InlineData("{\"Version\":1,\"IdentityKnown\":false,\"Grants\":[{\"Source\":\"origin\",\"School\":0,\"Levels\":1}]}")]
    [InlineData("{\"Version\":1,\"IdentityKnown\":true,\"Grants\":[{\"Source\":\"stat\",\"School\":0,\"Levels\":2,\"PaidPoints\":1}]}")]
    [InlineData("{\"Version\":1,\"IdentityKnown\":true,\"Grants\":[{\"Source\":\"acquired\",\"School\":0,\"Levels\":1,\"PaidPoints\":1}]}")]
    [InlineData("{\"Version\":1,\"IdentityKnown\":true,\"Grants\":[{\"Source\":\"skill:Destruction1\",\"School\":1,\"Levels\":1}]}")]
    public void Corrupt_future_or_ambiguous_sources_are_refused_without_repair(string encoded) =>
        Assert.Throws<FormatException>(() => MasterySources.Read(encoded));

    [Fact]
    public void Duplicate_source_and_school_cannot_authorize_two_refunds()
    {
        var grant = new MasteryGrant("stat", SpellSchool.Destruction, 1, 2);
        var encoded = JsonSerializer.Serialize(new { Version = 1, IdentityKnown = true, Grants = new[] { grant, grant } });
        Assert.Throws<FormatException>(() => MasterySources.Read(encoded));
    }
}

public class HeroCreationPlanTests
{
    internal static CreationBaseline Before() => new(70, 10, 4, 20, 99, [0, 0, 0, 0, 0], ["STRIKE", "DEFEND"]);
    internal static HeroCreationPlan Review(StartingBonus bonus = StartingBonus.Gold) => HeroCreationPlan.Empty(CreationMode.Start)
        .ChooseOrigin("FrostbornOrigin").ChooseBackground("SkaldBackground").ChooseBonus(bonus);
    internal static HeroCreationPlan Receipt() => Review().Confirm(Before()).IdentityComplete().EffectsComplete();
    private static HeroCreationPlan Reload(HeroCreationPlan plan) => HeroCreationPlan.Read(plan.Encode());

    [Theory]
    [InlineData(StartingBonus.Gold)]
    [InlineData(StartingBonus.Potion)]
    [InlineData(StartingBonus.RemoveBasic)]
    public void Every_required_step_survives_json_without_claiming_effects_early(StartingBonus bonus)
    {
        var plan = Reload(HeroCreationPlan.Empty(CreationMode.Start));
        Assert.Equal(CreationStage.Origin, plan.Stage);
        Assert.Throws<InvalidOperationException>(() => plan.ChooseBackground("VagrantBackground"));
        plan = Reload(plan.ChooseOrigin("FrostbornOrigin"));
        Assert.Equal(CreationStage.Background, plan.Stage);
        plan = Reload(plan.ChooseBackground("SkaldBackground"));
        Assert.Equal(CreationStage.Bonus, plan.Stage);
        plan = Reload(plan.ChooseBonus(bonus));
        Assert.Equal(CreationStage.Review, plan.Stage);
        Assert.False(plan.IdentityApplied); Assert.False(plan.BonusApplied);
        Assert.Throws<InvalidOperationException>(() => plan.Acknowledge());
        plan = Reload(plan.Confirm(Before()));
        Assert.Equal(CreationStage.Applying, plan.Stage);
        Assert.Throws<InvalidOperationException>(() => plan.EffectsComplete());
        plan = Reload(plan.IdentityComplete());
        if (bonus != StartingBonus.Gold) plan = Reload(plan.PrepareBonusSource("{\"id\":\"native_source\"}"));
        plan = Reload(plan.EffectsComplete());
        Assert.True(plan.IdentityApplied); Assert.True(plan.BonusApplied);
        Assert.Equal(CreationStage.Receipt, plan.Stage); // still must show the ACTUAL changes before the first battle
        Assert.NotSame(Before().Deck, plan.Baseline!.Deck);
        plan = Reload(plan.Acknowledge());
        Assert.Equal(CreationStage.Complete, plan.Stage);
        Assert.Throws<InvalidOperationException>(() => plan.ChooseBonus(StartingBonus.Gold));
        Assert.Throws<InvalidOperationException>(() => plan.Back());
        Assert.Equal(60, HeroCreationPlan.GoldGift);
    }

    [Fact]
    public void Back_is_pure_and_changing_origin_invalidates_dependent_choices()
    {
        var original = Review(); var encoded = original.Encode();
        var back = original.Back().Back().Back();
        Assert.Equal(CreationStage.Origin, back.Stage);
        back = back.ChooseOrigin("MistbornOrigin");
        Assert.Equal("", back.Background);
        Assert.Equal(StartingBonus.None, back.Bonus);
        Assert.False(back.IdentityApplied);
        Assert.Equal(encoded, original.Encode());
        Assert.Equal(CreationStage.Bonus, back.ChooseBackground("SummonerBackground").Stage);
    }

    [Fact]
    public void A_full_potion_belt_or_cancelled_removal_returns_to_bonus_without_reissuing_identity()
    {
        var initial = Review(StartingBonus.Potion).Confirm(Before()).IdentityComplete();
        var retry = Reload(initial.RetryBonusChoice());
        Assert.Equal(CreationStage.Bonus, retry.Stage);
        Assert.True(retry.IdentityApplied); Assert.False(retry.BonusApplied);
        Assert.Equal(initial.Baseline!.MaxHp, retry.Baseline!.MaxHp);
        Assert.Equal(initial.Baseline.Deck, retry.Baseline.Deck);
        Assert.Throws<InvalidOperationException>(() => retry.Back()); // cannot ungrant a one-shot starter gift
        var changed = retry.ChooseBonus(StartingBonus.Gold).Confirm(new(999, 99, 99, 99, 999, [5, 5, 5, 5, 5], []));
        Assert.True(changed.IdentityApplied);
        Assert.Equal(70, changed.Baseline!.MaxHp); // preserve original before/after baseline across the retry
        var complete = changed.EffectsComplete().Acknowledge();
        Assert.Equal(StartingBonus.Gold, complete.Bonus);
        Assert.Throws<InvalidOperationException>(() => complete.RetryBonusChoice());
    }

    [Fact]
    public void Exact_non_gold_source_is_required_for_a_receipt_and_cannot_be_replaced_twice()
    {
        var plan = Review(StartingBonus.RemoveBasic).Confirm(Before()).IdentityComplete();
        Assert.Throws<FormatException>(() => plan.EffectsComplete());
        plan = plan.PrepareBonusSource("{\"id\":\"STRIKE\",\"props\":{\"ints\":[{\"name\":\"instance\",\"value\":17}]}}");
        var roundTrip = Reload(plan);
        Assert.Equal(plan.BonusSource, roundTrip.BonusSource);
        Assert.Throws<InvalidOperationException>(() => roundTrip.PrepareBonusSource("{}"));
        Assert.Equal(CreationStage.Receipt, roundTrip.EffectsComplete().Stage);
    }

    [Fact]
    public void Background_removal_or_explicit_skip_is_saved_before_any_identity_effect_and_survives_bonus_retry()
    {
        foreach (var payload in new[] { "none", "{\"id\":\"BASH\",\"instance\":17}" })
        {
            var plan = Review(StartingBonus.Potion) with { Background = "VagrantBackground" };
            Assert.Throws<InvalidOperationException>(() => plan.PrepareBackgroundSource(payload));
            plan = Reload(plan.Confirm(Before()).PrepareBackgroundSource(payload));
            Assert.False(plan.IdentityApplied);
            Assert.Equal(payload, plan.BackgroundSource);
            Assert.Throws<InvalidOperationException>(() => plan.PrepareBackgroundSource(payload));
            plan = Reload(plan.IdentityComplete().RetryBonusChoice());
            Assert.Equal(CreationStage.Bonus, plan.Stage);
            Assert.Equal(payload, plan.BackgroundSource); // identity and its one-shot removal cannot be replayed
            plan = Reload(plan.ChooseBonus(StartingBonus.Gold).Confirm(Before()).EffectsComplete().Acknowledge());
            Assert.Equal(payload, plan.BackgroundSource);
        }
        var rebirth = HeroCreationPlan.Empty(CreationMode.Rebirth).ChooseOrigin("none").ChooseBackground("VagrantBackground")
            .Confirm(Before(), new string('A', 64));
        Assert.Throws<InvalidOperationException>(() => rebirth.PrepareBackgroundSource("none"));
        Assert.Throws<FormatException>(() => (rebirth with { BackgroundSource = "none" }).Encode());
        Assert.Throws<FormatException>(() => Review().Confirm(Before()).PrepareBackgroundSource("[]"));
        Assert.Throws<FormatException>(() => Review().Confirm(Before()).PrepareBackgroundSource("{\"id\":1,\"id\":2}"));
    }

    [Fact]
    public void Rebirth_receipt_retains_the_actual_original_stat_investment_and_free_pool()
    {
        var before = Before() with { StatPointsSpent = 9, UnspentStatPoints = 3 };
        var plan = HeroCreationPlan.Empty(CreationMode.Rebirth).ChooseOrigin("none").ChooseBackground("none")
            .Confirm(before, new string('B', 64)).IdentityComplete().EffectsComplete();
        var saved = Reload(plan);
        Assert.Equal(9, saved.Baseline!.StatPointsSpent);
        Assert.Equal(3, saved.Baseline.UnspentStatPoints);
        Assert.Throws<FormatException>(() => (plan with { Baseline = before with { StatPointsSpent = -1 } }).Encode());
    }

    [Fact]
    public void Rebirth_changes_both_identity_decisions_but_cannot_issue_any_starter_bonus()
    {
        var plan = Reload(HeroCreationPlan.Empty(CreationMode.Rebirth).ChooseOrigin("CitadelOrigin").ChooseBackground("EnvoyBackground"));
        Assert.Equal(CreationStage.Review, plan.Stage);
        Assert.Throws<InvalidOperationException>(() => plan.ChooseBonus(StartingBonus.Gold));
        Assert.Throws<FormatException>(() => plan.Confirm(Before()));
        plan = Reload(plan.Confirm(Before(), new string('A', 64))).IdentityComplete().EffectsComplete();
        Assert.Equal(CreationMode.Rebirth, plan.Mode);
        Assert.Equal(StartingBonus.None, plan.Bonus); Assert.False(plan.BonusApplied);
        Assert.Equal("", plan.BonusSource);
        Assert.Equal(CreationStage.Complete, Reload(plan).Acknowledge().Stage);
    }

    [Fact]
    public void Explicit_none_is_a_real_decision_not_a_missing_step()
    {
        var plan = HeroCreationPlan.Empty(CreationMode.Start).ChooseOrigin("none").ChooseBackground("none");
        Assert.Equal(CreationStage.Bonus, Reload(plan).Stage);
        Assert.Throws<InvalidOperationException>(() => plan.ChooseBonus(StartingBonus.None));
        Assert.Equal("none", Reload(plan.ChooseBonus(StartingBonus.Gold)).Origin);
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"Version\":1,\"Version\":1}")]
    public void Empty_future_or_duplicate_plans_are_readonly_refusals(string encoded) =>
        Assert.Throws<FormatException>(() => HeroCreationPlan.Read(encoded));

    [Fact]
    public void Malformed_cross_fields_and_opaque_future_fields_are_not_repaired()
    {
        var review = Review();
        HeroCreationPlan[] corrupt =
        [
            review with { Version = 2 }, review with { Stage = (CreationStage)99 }, review with { Mode = (CreationMode)99 },
            review with { Bonus = (StartingBonus)99 }, review with { Bonus = StartingBonus.None },
            review with { Origin = "" }, review with { Background = "" }, review with { Origin = "../origin" },
            review with { BonusApplied = true }, review with { IdentityApplied = true },
            review with { Stage = CreationStage.Applying }, review with { Stage = CreationStage.Receipt, BonusApplied = true },
            review with { BonusSource = "{}" }, review with { SourceFingerprint = new string('A', 64) },
            review.Confirm(Before()) with { Baseline = Before() with { Mastery = [0] } },
            review.Confirm(Before()) with { Baseline = Before() with { Deck = [""] } },
            review.Confirm(Before()) with { Baseline = Before() with { MaxHp = 0 } },
            review.Confirm(Before()).IdentityComplete() with { Bonus = StartingBonus.Potion, BonusSource = "[]" },
            review.Confirm(Before()).IdentityComplete() with { Bonus = StartingBonus.Potion, BonusSource = "{\"id\":1,\"id\":2}" },
        ];
        foreach (var bad in corrupt)
            Assert.Throws<FormatException>(() => HeroCreationPlan.Read(JsonSerializer.Serialize(bad)));
        var encoded = review.Encode();
        Assert.Throws<FormatException>(() => HeroCreationPlan.Read(encoded[..^1] + ",\"Future\":true}"));
        Assert.Throws<FormatException>(() => HeroCreationPlan.Read(new string('x', HeroCreationPlan.MaxLength + 1)));
    }

    [Theory]
    [InlineData("G", 64)]
    [InlineData("A", 63)]
    [InlineData("a", 65)]
    public void Rebirth_cannot_confirm_without_an_exact_source_fingerprint(string c, int length)
    {
        var plan = HeroCreationPlan.Empty(CreationMode.Rebirth).ChooseOrigin("none").ChooseBackground("none");
        Assert.Throws<FormatException>(() => plan.Confirm(Before(), string.Concat(Enumerable.Repeat(c, length))));
    }
}

public class HeroCreationSnapshotTests
{
    private static HeroCreationSnapshot Current(HeroCreationPlan plan) => new(StartupState.Protocol,
        plan.Stage == CreationStage.Complete ? StartupState.Complete : 0, plan.Stage == CreationStage.Complete, false,
        MasterySources.ForNewHero("Ironclad").Encode(), plan.Encode(), "", HeroPath.None);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 4)]
    [InlineData(1, 4)]
    public void A_completed_old_start_keeps_its_declaration_without_a_second_gift(int protocol, int phase)
    {
        var old = new HeroCreationSnapshot(protocol, phase, true, false, "", "", "", HeroPath.Reborn);
        old.Validate(3);
        var migrated = old with { Protocol = StartupState.Protocol, Phase = 4, Legacy = true,
            Mastery = MasterySources.PreserveLegacy([4, 3, 2, 1, 0]).Encode() };
        migrated.Validate(4);
        Assert.False(MasterySources.Read(migrated.Mastery).IdentityKnown);
        Assert.Throws<FormatException>(() => (migrated with { Start = HeroCreationPlanTests.Receipt().Acknowledge().Encode() }).Validate(4));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(1, 3)]
    public void Ambiguous_legacy_one_shot_progress_is_refused_before_any_migration(int protocol, int phase)
    {
        var old = new HeroCreationSnapshot(protocol, phase, false, false, "", "", "", HeroPath.None);
        Assert.Throws<FormatException>(() => old.Validate(3));
        Assert.Equal("", old.Mastery);
    }

    [Fact]
    public void Draft_applying_receipt_and_completion_have_distinct_compatible_saved_flags()
    {
        HeroCreationPlan[] plans = [HeroCreationPlan.Empty(CreationMode.Start), HeroCreationPlanTests.Review(),
            HeroCreationPlanTests.Review().Confirm(HeroCreationPlanTests.Before()), HeroCreationPlanTests.Receipt(), HeroCreationPlanTests.Receipt().Acknowledge()];
        foreach (var plan in plans)
        {
            var current = Current(plan);
            current.Validate(4);
            Assert.Throws<FormatException>(() => (current with { DeclaredComplete = !current.DeclaredComplete }).Validate(4));
        }
        Assert.Throws<FormatException>(() => Current(plans[0]).Validate(3));
        Assert.Throws<FormatException>(() => (Current(plans[0]) with { Mastery = "" }).Validate(4));
        Assert.Throws<FormatException>(() => (Current(plans[0]) with { Legacy = true }).Validate(4));
        Assert.Throws<FormatException>(() => (Current(plans[0]) with { Mastery = MasterySources.PreserveLegacy([0, 0, 0, 0, 0]).Encode() }).Validate(4));
    }

    [Fact]
    public void Rebirth_entitlement_is_not_path_ownership_until_its_effect_receipt()
    {
        var current = Current(HeroCreationPlanTests.Receipt().Acknowledge());
        var draft = HeroCreationPlan.Empty(CreationMode.Rebirth).ChooseOrigin("none").ChooseBackground("none");
        (current with { Rebirth = draft.Encode() }).Validate(4);
        Assert.Throws<FormatException>(() => (current with { Rebirth = draft.Encode(), Path = HeroPath.Reborn }).Validate(4));
        var applying = draft.Confirm(HeroCreationPlanTests.Before(), new string('1', 64));
        (current with { Rebirth = applying.Encode(), Path = HeroPath.Reborn, Interrupted = true }).Validate(4); // blocked, not a replay license
        Assert.Throws<FormatException>(() => (current with { Rebirth = applying.Encode(), Path = HeroPath.Reborn }).Validate(4));
        var receipt = applying.IdentityComplete().EffectsComplete();
        (current with { Rebirth = receipt.Encode(), Path = HeroPath.Reborn }).Validate(4);
        Assert.Throws<FormatException>(() => (current with { Rebirth = receipt.Encode() }).Validate(4));
        Assert.Throws<FormatException>(() => (current with { Path = HeroPath.Reborn }).Validate(4));
    }
}

public class IdentityAndProgressionRulesTests
{
    [Theory]
    [InlineData("HighbornOrigin", SpellSchool.Destruction)]
    [InlineData("MistbornOrigin", SpellSchool.Restoration)]
    public void Origin_school_is_an_identity_contribution(string origin, SpellSchool school) =>
        Assert.Equal(1, HeroIdentityRules.OriginMastery(origin)[(int)school]);

    [Theory]
    [InlineData("FrostbinderBackground", SpellSchool.Destruction)]
    [InlineData("ThunderShamanBackground", SpellSchool.Destruction)]
    [InlineData("MushroomTowerBackground", SpellSchool.Destruction)]
    [InlineData("DawnBrotherBackground", SpellSchool.Restoration)]
    [InlineData("DuskSingerBackground", SpellSchool.Restoration)]
    [InlineData("VenomweaverBackground", SpellSchool.Restoration)]
    [InlineData("CitadelMageBackground", SpellSchool.Alteration)]
    [InlineData("SummonerBackground", SpellSchool.Conjuration)]
    [InlineData("ShadowSorcererBackground", SpellSchool.Illusion)]
    public void Background_grants_are_not_one_shot_stat_purchases(string background, SpellSchool school)
    {
        var bonus = HeroIdentityRules.BackgroundMastery(background);
        Assert.Equal(1, bonus[(int)school]); Assert.Equal(1, bonus.Sum());
        var sources = MasterySources.ForNewHero("Ironclad").WithIdentity([0, 0, 0, 0, 0], bonus);
        Assert.True(sources.IsIdentitySchool(school));
        Assert.Equal(0, sources.StatPoints);
        Assert.Equal(1, sources.WithoutStatPurchases().Level(school));
    }

    [Fact]
    public void Legacy_import_does_not_invent_previously_missing_school_hooks()
    {
        Assert.Null(HeroIdentityRules.LegacyBackgroundMastery("FrostbinderBackground"));
        Assert.Null(HeroIdentityRules.LegacyBackgroundMastery("VenomweaverBackground"));
        Assert.Null(HeroIdentityRules.LegacyBackgroundMastery("SummonerBackground"));
        Assert.Equal(SpellSchool.Destruction, HeroIdentityRules.LegacyBackgroundMastery("ThunderShamanBackground"));
        Assert.Equal(4, HeroIdentityRules.OriginTypes("Ironclad").Count);
        Assert.Equal(3, HeroIdentityRules.OriginTypes("Silent").Count);
        Assert.Equal(3, HeroIdentityRules.OriginTypes("Regent").Count);
        Assert.Empty(HeroIdentityRules.OriginTypes("Defect"));
    }

    [Theory]
    [InlineData(5, 12, 0, 10, -2)]
    [InlineData(5, 18, 9, 5, -4)] // stat loss + retained gift gain + identity; one floor, not free HP
    [InlineData(5, 12, 0, 0, -4)]
    [InlineData(70, 6, 0, -5, -11)]
    [InlineData(70, 0, 0, 10, 10)]
    [InlineData(1, 0, 0, -5, 0)]
    [InlineData(10, -5, 0, -5, 0)]
    public void Max_hp_reconciles_sources_before_clamping_once(int hp, int oldBoon, int nextBoon, int identity, int expected) =>
        Assert.Equal(expected, HeroIdentityRules.ReconciledHpDelta(hp, oldBoon, nextBoon, identity));

    [Fact]
    public void Source_journal_must_agree_with_hero_costs_base_and_retained_skills()
    {
        var sources = MasterySources.ForNewHero("Ironclad").WithSkill(Skill.Destruction1);
        Assert.True(sources.TryBuyStat(SpellSchool.Destruction, 2, out sources));
        sources.RequireHero("Ironclad", new HashSet<Skill> { Skill.Destruction1 }, 2);
        Assert.Throws<FormatException>(() => sources.RequireHero("Ironclad", new HashSet<Skill>(), 2));
        Assert.Throws<FormatException>(() => sources.RequireHero("Ironclad", new HashSet<Skill> { Skill.Destruction1 }, 1));
        Assert.Throws<FormatException>(() => sources.RequireHero("Regent", new HashSet<Skill> { Skill.Destruction1 }, 2));
        Assert.Throws<FormatException>(() => MasterySources.ForNewHero("Ironclad").RequireHero("Ironclad", new HashSet<Skill> { Skill.Destruction1 }, 0));
        MasterySources.PreserveLegacy([3, 0, 0, 0, 0]).RequireHero("Ironclad", new HashSet<Skill> { Skill.Destruction1 }, 7);
    }

    [Theory]
    [InlineData(8, 2, 5, false, false, true)]
    [InlineData(7, 2, 5, false, false, false)]
    [InlineData(20, 1, 5, false, false, false)]
    [InlineData(8, 3, 5, true, false, false)]
    [InlineData(1, 0, 3, false, true, true)]
    [InlineData(7, 1, 3, false, true, true)]
    [InlineData(8, 0, 3, true, true, false)]
    [InlineData(8, -1, 3, false, true, false)]
    [InlineData(7, 1, 3, false, false, false)]
    [InlineData(8, 1, 3, false, false, true)]
    public void Full_short_and_vanilla_compatibility_have_explicit_path_timing(int level, int boss, int acts, bool owned, bool shortRun, bool expected) =>
        Assert.Equal(expected, TransformationRules.PathOfferDue(level, boss, acts, owned, shortRun));

    [Theory]
    [InlineData("")]
    [InlineData("2|Champion,Barbarian,Reborn")]
    [InlineData("1|None")]
    [InlineData("1|999")]
    [InlineData("1|Champion,Champion")]
    [InlineData("1|Champion,Barbarian,Reborn,Medium")]
    [InlineData("1|Champion,")]
    public void Invalid_saved_path_offers_are_not_rerolled(string encoded) =>
        Assert.Throws<FormatException>(() => PathOfferState.Read(encoded));

    [Fact]
    public void Original_path_rng_result_survives_cancel_continue_and_read_order()
    {
        HeroPath[] paths = [HeroPath.Champion, HeroPath.Barbarian, HeroPath.Reborn];
        var encoded = PathOfferState.Write(paths);
        Assert.Equal(paths, PathOfferState.Read(encoded));
        Assert.Equal(encoded, PathOfferState.Write(PathOfferState.Read(encoded)));
        Assert.Equal(0, TransformationRules.PathOfferActIndex(3, true));
        Assert.Equal(1, TransformationRules.PathOfferActIndex(3));
        Assert.Equal(2, TransformationRules.PathOfferActIndex(5));
    }
}

public class HeroCreationPeerProtocolTests
{
    [Fact]
    public void Lobby_controls_reuse_a_known_string_envelope_not_a_new_packet_type_or_saved_rules()
    {
        var controls = new[] { StartupState.LobbyQuery, StartupState.LobbyOffer, StartupState.LobbyAgreement };
        Assert.Equal(3, controls.Distinct().Count());
        Assert.All(controls, value =>
        {
            Assert.StartsWith(StartupState.LobbyPrefix, value);
            Assert.Null(RunSettingsData.Decode(value));
            Assert.DoesNotContain("v=", value);
        });
        Assert.EndsWith("/" + StartupState.Protocol, StartupState.LobbyOffer);
        Assert.EndsWith("/" + StartupState.Protocol, StartupState.LobbyAgreement);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Client_cannot_ready_for_an_old_unknown_or_future_host(int protocol)
    {
        Assert.Throws<InvalidOperationException>(() => StartupState.RequireHost(protocol));
        StartupState.RequireHost(StartupState.Protocol);
    }

    [Fact]
    public void New_lobbies_require_explicit_current_rules_but_old_saved_rules_stay_readable()
    {
        var legacy = RunSettingsData.Decode("v=1;hp=95")!;
        Assert.Throws<FormatException>(() => StartupState.RequireNewRules(legacy));
        var current = legacy.Clone();
        StartupState.StampNewRules(current);
        StartupState.RequireNewRules(RunSettingsData.Decode(current.Encode())!);
        Assert.Equal(95, current.EnemyHpPercent);
        Assert.False(legacy.Extensions.ContainsKey(StartupState.RulesKey));
        foreach (var bad in new[] { "1", "3", "02", "future" })
        {
            current.Extensions[StartupState.RulesKey] = bad;
            Assert.Throws<FormatException>(() => StartupState.RequireNewRules(current));
        }
    }

    [Fact]
    public void Missing_old_future_or_disconnected_peer_agreement_cannot_authorize_a_new_run()
    {
        var peers = new Dictionary<ulong, int> { [11] = StartupState.Protocol, [12] = StartupState.Protocol };
        StartupState.RequirePeers([11, 12], peers);
        StartupState.RequirePeers([], peers); // singleplayer
        Assert.Throws<InvalidOperationException>(() => StartupState.RequirePeers([11, 12, 13], peers));
        peers.Remove(12); // disconnected or a fresh lobby; agreement is not a permanent NetId property
        Assert.Throws<InvalidOperationException>(() => StartupState.RequirePeers([11, 12], peers));
        peers[12] = 1;
        Assert.Throws<InvalidOperationException>(() => StartupState.RequirePeers([11, 12], peers));
        peers[12] = StartupState.Protocol + 1;
        Assert.Throws<InvalidOperationException>(() => StartupState.RequirePeers([11, 12], peers));
    }
}
