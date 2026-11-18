using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Monsters;
using SlayTheRequiem.SlayTheRequiemCode.Pantheon;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Transformations;

/// <summary>
/// Сущность формы (docs/design/06): реликвия-статус, которую нельзя продать. Числа — общим путём <see cref="BoonRelic"/>
/// (♥, пулы, Латы, сопротивления, урон атак, отдых), лечение — <see cref="IHealModifier"/>, иммунитеты —
/// <see cref="IStatusImmunity"/>. При получении кладёт в колоду особые карты формы, при снятии — забирает.
/// Визуал — облик формы (<see cref="HeroLooks"/>) и перекраска/накладки (<see cref="FormVisuals"/>).
/// </summary>
public abstract class FormRelic : BoonRelic, IHealModifier, IStatusImmunity
{
    public abstract FormKind Kind { get; }

    /// <summary>Особые карты формы, которые кладутся в колоду при получении.</summary>
    protected virtual IEnumerable<Type> FormCards => [];

    /// <summary>Перекраска модели: оттенок (hsv-шейдер, доля круга) и множитель цвета. null — без изменений.</summary>
    public virtual float? Hue => null;
    public virtual Color? Tint => null;
    /// <summary>Цвет вспышки при обретении формы.</summary>
    public virtual Color FlashColor => new(1f, 1f, 1f);

    public virtual int HealPercent(HealSource source) => 0;
    public virtual bool BlocksHealing(HealSource source) => false;
    public virtual bool ImmuneTo(PowerModel canonicalPower) => false;

    protected override async Task OnObtained()
    {
        foreach (var type in FormCards)
        {
            var canonical = ModelDb.GetById<CardModel>(ModelDb.GetId(type));
            await CardAcquisition.AddOrDefer(Owner.RunState.CreateCard(canonical, Owner));
        }
        Faith.RefreshAll(Owner);
        FormVisuals.Refresh(Owner, flash: FlashColor);
        await OnFormGained();
    }

    protected virtual Task OnFormGained() => Task.CompletedTask;

    /// <summary>Снять форму: забрать особые карты и выданные ♥, убрать реликвию.</summary>
    public async Task Shed()
    {
        var types = FormCards.ToHashSet();
        foreach (var card in Owner.Deck.Cards.Where(c => types.Contains(c.GetType())).ToList())
            await CardPileCmd.RemoveFromDeck(card, showPreview: false);
        await OnFormLost();
        var owner = Owner;
        await RemoveSelf();
        Faith.RefreshAll(owner);
        FormVisuals.Refresh(owner);
    }

    protected virtual Task OnFormLost() => Task.CompletedTask;

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        await base.AfterRoomEntered(room);
        if (room is CombatRoom) FormVisuals.Refresh(Owner);
    }

    protected override Task OnCombatStarted(ICombatState combatState)
    {
        FormVisuals.Refresh(Owner);
        return Task.CompletedTask;
    }

    /// <summary>Доля ♥ героя в процентах.</summary>
    protected int HpPercent => Owner.Creature.MaxHp <= 0 ? 100 : Owner.Creature.CurrentHp * 100 / Owner.Creature.MaxHp;
}

/// <summary>Формы героя: чтение, совместимость (06 §3) и выдача.</summary>
public static class Forms
{
    public static IEnumerable<FormRelic> All(Player? player) =>
        player?.Relics.OfType<FormRelic>() ?? Enumerable.Empty<FormRelic>();

    public static T? Of<T>(Player? player) where T : FormRelic => All(player).OfType<T>().FirstOrDefault();

    /// <summary>Мировоззрение (Праведник, вампир, оборотень, Договор/Вестник/Лич) или None.</summary>
    public static FormKind Worldview(Player? player) =>
        All(player).Select(f => f.Kind).FirstOrDefault(TransformationRules.IsWorldview);

    public static bool HasClockwork(Player? player) => All(player).Any(f => f.Kind == FormKind.Clockwork);

    /// <summary>Предыстория Ординатора: Праведник, вампир, оборотень и лич закрыты (06 §3), Путь Паладина — открыт.</summary>
    public static bool IsOrdinator(Player player) => AbyssGift.IsOrdinator(player);

    public static bool CanTake(Player player, FormKind kind)
    {
        if (!RequiemMode.IsInSystem(player)) return false;
        if (kind == FormKind.Clockwork) return TransformationRules.CanTakeClockwork(Worldview(player), HasClockwork(player));
        return TransformationRules.CanTakeWorldview(Worldview(player), kind, Faith.Effective(player) == Piety.Righteous,
            IsOrdinator(player), HasClockwork(player));
    }

    public static Type TypeFor(FormKind kind) => kind switch
    {
        FormKind.Righteous => typeof(RighteousForm),
        FormKind.Vampire => typeof(VampireForm),
        FormKind.Werewolf => typeof(WerewolfForm),
        FormKind.DarkPact => typeof(DarkPactForm),
        FormKind.Herald => typeof(HeraldForm),
        FormKind.Lich => typeof(LichForm),
        _ => typeof(ClockworkHeart),
    };

    public static RelicModel Create(Type type) => ModelDb.GetById<RelicModel>(ModelDb.GetId(type)).ToMutable();

    /// <summary>Принять форму. Ступень Договора заменяет предыдущую. false — несовместимо.</summary>
    public static async Task<bool> Grant(Player player, FormKind kind)
    {
        if (!CanTake(player, kind)) return false;
        if (kind is FormKind.Herald or FormKind.Lich)
            foreach (var previous in All(player).Where(f => f.Kind is FormKind.DarkPact or FormKind.Herald).ToList())
                await previous.Shed();
        await RelicCmd.Obtain(Create(TypeFor(kind)), player);
        if (Progression.HeroRecord.Of(player) is { } hero) await hero.SyncMaxHp();
        return true;
    }
}

/// <summary>
/// Визуал форм (06 §5). Облик (свой спрайт вампира/оборотня, модель Некробиндер/Дефекта) строит <see cref="HeroLooks"/>
/// при создании модели в бою; здесь — то, что меняется по ходу боя: картинка-вариант спрайта (Звериная форма, Стойка лорда),
/// вспышка при превращении, накладки (<see cref="HeroOverlays"/>: знамя Знака Пути, Сферы стихий).
/// На родной модели героя (Праведник, Договор, Вестник; откат без файла облика) — MVP-перекраска: hsv-оттенок, множитель
/// цвета, масштаб оборотня в Звериной форме и «жидкая» вспышка.
/// </summary>
public static class FormVisuals
{
    private static readonly ConditionalWeakTable<NCreatureVisuals, StrongBox<float>> BaseScale = new();

    public static void Refresh(Player? player, Color? flash = null, float scale = 1f)
    {
        if (player?.Creature == null) return;
        try
        {
            var node = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
            if (node == null) return;
            var visuals = node.Visuals;
            var look = HeroLooks.LookOf(player);
            if (OwnArtAnimator.IsOwnArt(visuals))
            {
                visuals.Modulate = new Color(1f, 1f, 1f);
                var swapped = HeroLooks.CurrentSprite(player, look) is { } path && OwnArtAnimator.Swap(visuals, path);
                if (!swapped && flash is { } spriteFlash) OwnArtAnimator.Flash(visuals, spriteFlash);
            }
            else
            {
                var forms = Forms.All(player).ToList();
                // Чужая модель (Некробиндер, Дефект) — без перекраски; родная модель героя (в т. ч. откат) — MVP-оттенок.
                var keepTint = !HeroLooks.IsBorrowed(visuals);
                var hue = keepTint ? forms.Select(f => f.Hue).LastOrDefault(h => h != null) ?? 0f : 0f;
                var tint = keepTint ? forms.Select(f => f.Tint).LastOrDefault(t => t != null) ?? new Color(1f, 1f, 1f) : new Color(1f, 1f, 1f);
                if (player.Creature.GetPower<BeastFormPower>() != null)
                {
                    // Звериная форма без своего спрайта: крупнее и бурее.
                    scale *= HeroLookRules.BeastScale;
                    tint = new Color(0.8f, 0.7f, 0.6f);
                }
                var baseScale = BaseScale.GetValue(visuals, v => new StrongBox<float>(v.DefaultScale)).Value;
                visuals.SetScaleAndHue(baseScale * scale, hue);
                visuals.Modulate = tint;
                if (flash is { } color) visuals.TryApplyLiquidOverlay(color);
            }
            HeroOverlays.Refresh(player, node);
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"FormVisuals: {e.Message}");
        }
    }
}
