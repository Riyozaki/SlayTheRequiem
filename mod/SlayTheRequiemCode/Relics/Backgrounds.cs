using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Cards.Platebearer;
using SlayTheRequiem.SlayTheRequiemCode.Cards.Silent;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Potions;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Spells;

namespace SlayTheRequiem.SlayTheRequiemCode.Relics;

/// <summary>
/// Предыстория (docs/design/04) — вторая стартовая реликвия-«паспорт»: меняет 1 стартовую карту, даёт мелочь
/// (зелье, владение школой) и маленький пассив. Выдаётся Гримуаром сразу после происхождения.
/// </summary>
public abstract class BackgroundRelic : RequiemRelic, IResourceSource
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override bool IsAllowedInShops => false;
    public override bool IsAllowed(IRunState runState) => false;

    protected virtual int MaxHpDelta => 0;
    internal int IdentityMaxHp => MaxHpDelta;
    private bool _identityReplacement, _startupSetupDone;
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool IdentityReplacement { get => _identityReplacement; set { AssertMutable(); _identityReplacement = value; } }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool StartupSetupDone { get => _startupSetupDone; set { AssertMutable(); _startupSetupDone = value; } }
    public virtual int StaminaPoolBonus => 0;
    public virtual int StaminaRegenBonus => 0;
    public virtual int ManaPoolBonus => 0;
    public virtual int ManaRegenBonus => 0;
    public virtual int ManaStartBonus => 0;
    public virtual int PoiseBonus => 0;
    public virtual int BurdenBonus => 0;

    /// <summary>One-shot starter gifts only. Permanent mastery belongs to the identity source journal.</summary>
    protected virtual Task SetUpStart() => Task.CompletedTask;

    public override async Task AfterObtained()
    {
        if (IdentityReplacement || StartupSetupDone) return;
        StartupSetupDone = true; // an exception is uncertain, never permission to issue these gifts again
        Mastery.GrimoireOf(Owner)?.RefreshIdentitySources();
        if (MaxHpDelta > 0) await CreatureCmd.GainMaxHp(Owner.Creature, MaxHpDelta);
        else if (MaxHpDelta < 0) await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), Owner.Creature, -MaxHpDelta, false);
        await SetUpStart();
    }

    // ───── помощники старта ─────

    /// <summary>Заменить одну базовую карту с тегом (Удар/Защита) на карту T.</summary>
    protected async Task ReplaceStarter<T>(CardTag tag) where T : CardModel
    {
        var old = Owner.Deck.Cards.FirstOrDefault(c => c.Rarity == CardRarity.Basic && c.Tags.Contains(tag));
        await CardAcquisition.GrantOnceOrDefer(this, Owner.RunState.CreateCard<T>(Owner), "background:" + typeof(T).Name, old);
    }

    protected async Task AddToDeck<T>() where T : CardModel
    {
        var card = Owner.RunState.CreateCard<T>(Owner);
        await CardAcquisition.GrantOnceOrDefer(this, card, "background:" + typeof(T).Name);
    }

    protected Task GivePotion<T>() where T : PotionModel => PotionCmd.TryToProcure<T>(Owner);

    /// <summary>Стартовый предмет: снаряжение надевается само (<see cref="Equipment.EquipmentRelic.AfterObtained"/>).</summary>
    protected Task GiveRelic<T>() where T : RelicModel => RelicCmd.Obtain(ModelDb.Relic<T>().ToMutable(), Owner);

    /// <summary>Аура на врагов в первый ход героя (враги уже подготовлены правилами боя).</summary>
    protected static bool IsFirstTurnOf(Player player, Player owner) =>
        player == owner && player.Creature.CombatState is { RoundNumber: 1 };

    protected async Task ApplyToAllEnemies<T>(int amount) where T : PowerModel
    {
        var combat = Owner.Creature.CombatState;
        if (combat == null) return;
        foreach (var enemy in combat.Enemies.Where(e => e.IsAlive).ToList())
            await PowerCmd.Apply<T>(new ThrowingPlayerChoiceContext(), enemy, amount, Owner.Creature, null, silent: true);
    }

    protected bool IsOwnAttack(Creature? dealer, ValueProp props, CardModel? card) =>
        dealer == Owner.Creature && card != null && card.Owner == Owner && card.Type == CardType.Attack && props.IsPoweredAttack();

    // ───── выдача по настройке ─────

    /// <summary>Предыстории происхождения по порядку (1-я, 2-я, 3-я); null — ещё не сделана (этап 4).</summary>
    public static Type?[] Options(OriginRelic? origin) => origin switch
    {
        FrostbornOrigin => [typeof(BeastSpeakerBackground), typeof(SkaldBackground), typeof(FrostbinderBackground)],
        ForgebloodOrigin => [typeof(BerserkerBackground), typeof(PoacherBackground), typeof(ThunderShamanBackground)],
        DunebladeOrigin => [typeof(DawnBrotherBackground), typeof(ConspiratorBackground), typeof(AnseiBackground)],
        CitadelOrigin => [typeof(LegionaryBackground), typeof(EnvoyBackground), typeof(CitadelMageBackground)],
        MoonbornOrigin => [typeof(FugitiveBackground), typeof(RogueBackground), typeof(DuskSingerBackground)],
        ThicketbornOrigin => [typeof(WinddancerBackground), typeof(HunterBackground), typeof(HermitBackground)],
        MarshbornOrigin => [typeof(GladiatorBackground), typeof(ShadowscaleBackground), typeof(VenomweaverBackground)],
        HighbornOrigin => [typeof(JusticiarBackground), typeof(ShadowSorcererBackground), typeof(WizardBackground)],
        MistbornOrigin => [typeof(KnightBackground), typeof(EnchanterBackground), typeof(SummonerBackground)],
        AshbornOrigin => [typeof(OrdinatorBackground), typeof(AshNomadBackground), typeof(MushroomTowerBackground)],
        _ => [],
    };

    /// <summary>Предыстория: экран выбора при настройке «На выбор» (готовые предыстории происхождения + Бродяга).</summary>
    public static async Task<BackgroundRelic?> Pick(Player player, OriginRelic? origin)
    {
        var options = Options(origin).Where(t => t != null).Append(typeof(VagrantBackground)).Distinct()
            .Select(t => ModelDb.GetById<RelicModel>(ModelDb.GetId(t!)).ToMutable()).ToList();
        return await RelicSelectCmd.FromChooseARelicScreen(player, options) as BackgroundRelic;
    }

    public static BackgroundRelic? For(Player player, OriginRelic? origin)
    {
        var choice = RunSettings.Background(player);
        if (choice == BackgroundChoice.None) return null;
        var options = Options(origin);
        Type? type = choice switch
        {
            BackgroundChoice.Vagrant => typeof(VagrantBackground),
            BackgroundChoice.First or BackgroundChoice.Second or BackgroundChoice.Third
                when options.Length > (int)choice - 1 && options[(int)choice - 1] != null => options[(int)choice - 1],
            _ => null,
        };
        if (type == null)
        {
            // Случайно (или выбранная ещё не готова): среди готовых предысторий происхождения; без происхождения — Бродяга.
            var ready = options.Where(t => t != null).ToList();
            type = ready.Count == 0 ? typeof(VagrantBackground) : ready[OriginRelic.OriginRng(player, "background").NextInt(ready.Count)];
        }
        return (BackgroundRelic)ModelDb.GetById<RelicModel>(ModelDb.GetId(type!)).ToMutable();
    }
}

// ═════════════ Бродяга (для всех) ═════════════

/// <summary>Бродяга: +5 ♥, +1 ◆, +1 ✦, +3 груза и одно бесплатное удаление карты (сразу, на старте).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class VagrantBackground : BackgroundRelic
{
    protected override int MaxHpDelta => 5;
    public override int StaminaPoolBonus => 1;
    public override int ManaPoolBonus => 1;
    public override int BurdenBonus => 3;

    protected override Task SetUpStart() => Progression.HeroCreation.ApplyVagrantRemoval(Owner);
}

// ═════════════ Латоносец · Инеерождённые ═════════════

/// <summary>Зверобой-духовидец: в начале боя бесплатный дух зверя (волк 6 ♥/3 → саблезуб 12/5 → медведь 20/7 по элитам).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class BeastSpeakerBackground : BackgroundRelic
{
    private int _elitesDefeated;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ElitesDefeated { get => _elitesDefeated; set { AssertMutable(); _elitesDefeated = value; } }

    public override bool ShowCounter => true;
    public override int DisplayAmount => ElitesDefeated;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (!IsFirstTurnOf(player, Owner)) return;
        var (hp, damage) = OriginRules.BeastSpirit(ElitesDefeated);
        var elites = ElitesDefeated;
        await SummonPower.Summon<BeastSpiritPower>(choiceContext, Owner.Creature, hp, null, p => p.SetStage(elites, damage));
        Flash();
    }

    public override Task AfterCombatVictory(CombatRoom room)
    {
        if (room.RoomType == RoomType.Elite) ElitesDefeated++;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }
}

/// <summary>Скальд: «Боевой клич» вместо одной Защиты; на полке Кличей — 2 места вместо 1 (<see cref="Cards.Shouts"/>).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class SkaldBackground : BackgroundRelic
{
    protected override Task SetUpStart() => ReplaceStarter<BattleCry>(CardTag.Defend);
}

/// <summary>Хладовяз: «Ледяной ветер» вместо Удара; аура — все враги уязвимы к холоду (I).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class FrostbinderBackground : BackgroundRelic
{
    protected override Task SetUpStart() => ReplaceStarter<IceWind>(CardTag.Strike);

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (IsFirstTurnOf(player, Owner)) await ApplyToAllEnemies<FrostResistancePower>(-1);
    }
}

// ═════════════ Латоносец · Горнокровные ═════════════

/// <summary>Берсерк: тяжёлые атаки (◆ 2+) на 1 ◆ дешевле; стартовый двуручный топор.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class BerserkerBackground : BackgroundRelic, IAttackStaminaModifier
{
    protected override Task SetUpStart() => GiveRelic<Equipment.IronBattleaxe>();

    public int ModifyAttackStamina(CardModel card, int cost) =>
        card.Owner == Owner && Pantheon.BoonRelic.IsHeavy(card) ? cost - 1 : cost;
}

/// <summary>Браконьер: тяжёлый лук; «Прицельный выстрел» вместо Удара; луки и арбалеты не весят (выстрелы без штрафа к весу).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class PoacherBackground : BackgroundRelic, Equipment.IEquipmentWeightWaiver
{
    protected override async Task SetUpStart()
    {
        await GiveRelic<Equipment.HeavyBow>();
        await ReplaceStarter<AimedShot>(CardTag.Strike);
    }

    public bool Waives(Equipment.EquipmentRelic item) => Progression.HeroRecord.IsRanged(item.Weapon);
}

/// <summary>Громовой шаман: +1 владения Разрушением; «Искры» вместо Удара; аура — враги уязвимы к разряду (I).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class ThunderShamanBackground : BackgroundRelic
{
    protected override Task SetUpStart()
    {
        return ReplaceStarter<Sparks>(CardTag.Strike);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (IsFirstTurnOf(player, Owner)) await ApplyToAllEnemies<ShockResistancePower>(-1);
    }
}

// ═════════════ Латоносец · Дюнные клинки ═════════════

/// <summary>Брат Рассвета: +1 владения Восстановлением; бесплатно поддерживаемый «Дух света» (2 урона светом в начале хода).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class DawnBrotherBackground : BackgroundRelic
{
    protected override Task SetUpStart()
    {
        return Task.CompletedTask;
    }

    public override async Task BeforeCombatStart() =>
        await PowerCmd.Apply<LightSpiritPower>(new ThrowingPlayerChoiceContext(), Owner.Creature, OriginRules.LightSpiritDamage, Owner.Creature, null, silent: true);
}

/// <summary>Заговорщик: яды на оружии +1 заряд и +25 % силы (<see cref="WeaponPoisonPower"/>); стартовый Комбинированный яд I.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class ConspiratorBackground : BackgroundRelic, IPoisonChargeSource
{
    public int ExtraPoisonCharges => 1;

    protected override Task SetUpStart() => GivePotion<CombinedPoisonI>();
}

/// <summary>Клинок-ансей: в колоде «Дух клинка» — переключаемый дух (атаки + урон магией ⌊✦/2⌋, но +1 ✦).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class AnseiBackground : BackgroundRelic
{
    protected override Task SetUpStart() => AddToDeck<BladeSpirit>();
}

// ═════════════ Латоносец · Цитадельцы ═════════════

/// <summary>Легионер: стартовый щит; «Удар щитом» вместо Удара; в первый ход боя Блок от карт +2, если в руке щит.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class LegionaryBackground : BackgroundRelic
{
    protected override async Task SetUpStart()
    {
        await GiveRelic<Equipment.OakShield>();
        await ReplaceStarter<ShieldBash>(CardTag.Strike);
    }

    public override decimal ModifyBlockAdditive(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay) =>
        target == Owner.Creature && cardSource != null && Owner.Creature.CombatState is { RoundNumber: 1 }
        && Equipment.Loadout.InSlot(Owner, EquipSlot.OffHand) is { Weapon: WeaponKind.Shield }
            ? OriginRules.LegionaryFirstTurnBlock : 0m;
}

/// <summary>Посланник: в каждом бою рядом страж «Верный-до-Конца» — принимает удары первым, растёт с уровнем героя.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class EnvoyBackground : BackgroundRelic
{
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (!IsFirstTurnOf(player, Owner)) return;
        var (hp, damage) = OriginRules.LoyalGuard(Progression.HeroRecord.Of(Owner)?.Level ?? 1);
        await SummonPower.Summon<LoyalGuardPower>(choiceContext, Owner.Creature, hp, null, p => p.SetDamage(damage));
        Flash();
    }
}

/// <summary>Маг цитадели: +1 владения Изменением; «Импульс» (телекинез) вместо Удара.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class CitadelMageBackground : BackgroundRelic
{
    protected override Task SetUpStart()
    {
        return ReplaceStarter<Telekinesis>(CardTag.Strike);
    }
}

// ═════════════ Безмолвная · Лунная порода ═════════════

/// <summary>Беглец: +1 урона атакам за каждую сыгранную в этом ходу 0-энергетическую карту (до +3).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class FugitiveBackground : BackgroundRelic
{
    private int _zeroCostThisTurn;

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner && cardPlay.IsLastInSeries && cardPlay.Resources.EnergyValue == 0) _zeroCostThisTurn++;
        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner) _zeroCostThisTurn = 0;
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        IsOwnAttack(dealer, props, cardSource) ? OriginRules.FugitiveBonus(_zeroCostThisTurn) : 0m;
}

/// <summary>
/// Плут (04, 14 §1): в колоде «Обчистить» (кража золота); открывает Кражу в лавках (<see cref="ITheftEnabler"/>);
/// в сундуке комнаты сокровищ — +1 реликвия на выбор (<see cref="RogueChestPatch"/>). Ревью F-09.
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class RogueBackground : BackgroundRelic, Shops.ITheftEnabler
{
    protected override Task SetUpStart() => AddToDeck<PickPocket>();
}

/// <summary>Сумеречный певец: +1 владения Восстановлением (свет) и стартовый «Эликсир лунного сахара».</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class DuskSingerBackground : BackgroundRelic
{
    protected override async Task SetUpStart()
    {
        await GivePotion<MoonSugarElixir>();
    }
}

// ═════════════ Безмолвная · Сыны Чащи ═════════════

/// <summary>Ветроплясун: атаки +1 урона за каждую 3-ю карту, сыгранную за ход.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class WinddancerBackground : BackgroundRelic
{
    private int _cardsThisTurn;

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner && cardPlay.IsLastInSeries) _cardsThisTurn++;
        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner) _cardsThisTurn = 0;
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        IsOwnAttack(dealer, props, cardSource) ? OriginRules.WinddancerBonus(_cardsThisTurn) : 0m;
}

/// <summary>Охотник: «выстрелы» Безмолвной — Ножи (тег Shiv) — наносят +2 урона.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class HunterBackground : BackgroundRelic
{
    public const int ShotBonus = 2;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        IsOwnAttack(dealer, props, cardSource) && cardSource!.Tags.Contains(CardTag.Shiv) ? ShotBonus : 0m;
}

/// <summary>
/// Отшельник: после каждого боя +1 реагент (Кровяной мох или Ночная поганка). Готовит Грибную похлёбку в лагере и без
/// «Походного котла» (<see cref="Alchemy.CookingRestSiteOption"/>).
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class HermitBackground : BackgroundRelic
{
    public override Task AfterCombatVictory(CombatRoom room)
    {
        var pouch = HerbalistPouch.Of(Owner);
        if (pouch == null) return Task.CompletedTask;
        var reagent = Owner.PlayerRng.Rewards.NextInt(2) == 0 ? Reagent.Bloodmoss : Reagent.Nightcap;
        pouch.Add(reagent, 1);
        Flash();
        return Task.CompletedTask;
    }
}

// ═════════════ Безмолвная · Тинники ═════════════

/// <summary>Гладиатор: в начале хода лечит 1 (постоянная Регенерация 1); стартовое зелье Омоложения I.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class GladiatorBackground : BackgroundRelic
{
    protected override Task SetUpStart() => GivePotion<RejuvenationPotionI>();

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner && Owner.Creature.CurrentHp < Owner.Creature.MaxHp) await CreatureCmd.Heal(Owner.Creature, 1);
    }
}

/// <summary>Тёмный ящер: «Из тени» — первая атака боя ×2 и не тратит ◆.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class ShadowscaleBackground : BackgroundRelic, IAttackStaminaModifier
{
    private bool _attackedThisCombat;

    public override Task BeforeCombatStart()
    {
        _attackedThisCombat = false;
        return Task.CompletedTask;
    }

    public int ModifyAttackStamina(CardModel card, int cost) => _attackedThisCombat || card.Owner != Owner ? cost : 0;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        !_attackedThisCombat && IsOwnAttack(dealer, props, cardSource) ? 2m : 1m;

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!_attackedThisCombat && cardPlay.Card.Owner == Owner && cardPlay.Card.Type == CardType.Attack && cardPlay.IsLastInSeries)
        {
            _attackedThisCombat = true;
            Flash();
        }
        return Task.CompletedTask;
    }
}

/// <summary>Ядовяз: «Ядовитый спрей» вместо Удара.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class VenomweaverBackground : BackgroundRelic
{
    protected override Task SetUpStart() => ReplaceStarter<PoisonSpray>(CardTag.Strike);
}

// ═════════════ Регент · Златокровные ═════════════

/// <summary>Юстициар: в начале боя бесплатный «Дух молнии» (грозовой атронах, 10 ♥).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class JusticiarBackground : BackgroundRelic
{
    public const int SpiritHp = 10;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (!IsFirstTurnOf(player, Owner)) return;
        await SummonPower.Summon<StormAtronachPower>(choiceContext, Owner.Creature, SpiritHp, null);
        Flash();
    }
}

/// <summary>Чародей теней: +1 владения Иллюзией; «Смятение» вместо Удара.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class ShadowSorcererBackground : BackgroundRelic
{
    protected override Task SetUpStart()
    {
        return ReplaceStarter<Confusion>(CardTag.Strike);
    }
}

/// <summary>Волшебник: +1 урона заклинаниям за каждые потерянные 25 % ✦ пула.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class WizardBackground : BackgroundRelic
{
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        dealer == Owner.Creature && cardSource is SpellCard && cardSource.Owner == Owner
            ? OriginRules.WizardSpellBonus(Mana.Amount(Owner), Mana.Pool(Owner)) : 0m;
}

// ═════════════ Регент · Туманные ═════════════

/// <summary>Рыцарь: Латы +1/+2/+3 при ♥ ниже 90/70/50 % (пересчёт в начале хода).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class KnightBackground : BackgroundRelic
{
    private int _applied;

    public override Task BeforeCombatStart()
    {
        _applied = 0;
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner) return;
        var hero = Owner.Creature;
        var want = OriginRules.KnightPlating(hero.CurrentHp, hero.MaxHp);
        var delta = want - _applied;
        if (delta == 0) return;
        if (delta > 0) await PowerCmd.Apply<PlatingPower>(choiceContext, hero, delta, hero, null, silent: true);
        else if (hero.GetPower<PlatingPower>() is { } plating)
            await PowerCmd.ModifyAmount(choiceContext, plating, Math.Max(delta, -plating.Amount), null, null, silent: true);
        _applied = want;
        Flash();
    }
}

/// <summary>
/// Зачарователь: каждая не-заклинательная атака возвращает 1 ✦. Зачаровальный стол — в любом лагере
/// (<see cref="Alchemy.Camp.HasEnchantingTable"/>).
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class EnchanterBackground : BackgroundRelic
{
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner || cardPlay.Card.Type != CardType.Attack || cardPlay.Card is SpellCard || !cardPlay.IsLastInSeries) return;
        await Mana.Gain(Owner, 1);
    }
}

/// <summary>Призыватель: призывы +5 ♥ и +1 урона; «Призвать волка-духа» вместо Удара.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class SummonerBackground : BackgroundRelic
{
    public const int BonusHp = 5;
    public const int BonusDamage = 1;

    protected override Task SetUpStart() => ReplaceStarter<ConjureSpiritWolf>(CardTag.Strike);

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (target != Owner.Creature || canonicalPower is not SummonPower || amount <= 0) return false;
        modifiedAmount = amount + BonusHp;
        return true;
    }
}

// ═════════════ Регент · Пеплорождённые ═════════════

/// <summary>Ученик Грибной Башни (пиромант): «Огненная стрела» вместо Удара; аура — враги уязвимы к огню (I).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class MushroomTowerBackground : BackgroundRelic
{
    protected override Task SetUpStart() => ReplaceStarter<Firebolt>(CardTag.Strike);

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (IsFirstTurnOf(player, Owner)) await ApplyToAllEnemies<FireResistancePower>(-1);
    }
}

/// <summary>
/// Ординатор: все три дара Тройственных сразу (Измена, Паутина, Сумеречная Звезда — Цена ослаблена, праведность не страдает).
/// Клятва Трибуналу: запрещены Праведник, вампиризм, ликантропия и лич (06 §3; из Путей — только Паладин из «святых»),
/// благословения Небесных недоступны, чужих Князей Ординатор не принимает.
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class OrdinatorBackground : BackgroundRelic,
    Pantheon.ITribunalHeir, Pantheon.ITribunalOrdinator, Pantheon.IPietyForbidder
{
    protected override async Task SetUpStart()
    {
        // Напрямую, а не через Accept: дары Тройственных не стоят Ординатору праведности и не вытесняют друг друга.
        foreach (var prince in PantheonRules.Tribunal)
            if (Pantheon.AbyssGift.AllOf(Owner).All(g => g.Prince != prince))
                await RelicCmd.Obtain(Pantheon.AbyssGift.Create(prince), Owner);
        Pantheon.Faith.RefreshAll(Owner);
    }
}

/// <summary>Пепельный кочевник: дары Тройственных на ступень сильнее (<see cref="PantheonRules.TribunalTierUp"/>).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class AshNomadBackground : BackgroundRelic, Pantheon.ITribunalHeir, Pantheon.ITribunalAdept
{
}
