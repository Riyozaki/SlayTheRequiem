using System.Runtime.CompilerServices;
using BaseLib.Utils.NodeFactories;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using SlayTheRequiem.SlayTheRequiemCode.Art;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Extensions;
using SlayTheRequiem.SlayTheRequiemCode.Monsters;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Transformations;

/// <summary>
/// Облики форм (docs/design/06 §5, M6 п. 3). Модель героя в бою строится заново в каждом бою (<c>NCreature.Create</c> →
/// <see cref="Creature.CreateVisuals"/>), поэтому облик выбирается там же по формам героя (<see cref="HeroLookRules.LookFor"/>):
/// <list type="bullet">
/// <item>вампир и оборотень — свой PNG (images/monsters/hero_*.png) с код-анимациями <see cref="OwnArtAnimator"/>;</item>
/// <item>Лич — ванильная модель Некробиндер, Заводное сердце — модель Дефекта, с их Spine-аниматорами
///   (<see cref="HeroLookAnimatorPatches"/>).</item>
/// </list>
/// Нет файла / сцены — откат на модель героя с перекраской (MVP <see cref="FormVisuals"/>), бой не ломается.
/// </summary>
public static class HeroLooks
{
    /// <summary>Чья модель взята (Некробиндер/Дефект) — по корню узла визуала; читает патч аниматора.</summary>
    private static readonly ConditionalWeakTable<NCreatureVisuals, CharacterModel> Borrowed = new();

    public static HeroLook LookOf(Player? player) =>
        player == null || !RequiemMode.IsInSystem(player)
            ? HeroLook.Hero
            : HeroLookRules.LookFor(Forms.Worldview(player), Forms.HasClockwork(player));

    public static CharacterModel? BorrowedCharacter(HeroLook look) => look switch
    {
        HeroLook.Lich => ModelDb.Character<Necrobinder>(),
        HeroLook.Clockwork => ModelDb.Character<Defect>(),
        _ => null,
    };

    public static string SpritePath(string key, string? variant) =>
        $"monsters/{key}{(string.IsNullOrEmpty(variant) ? "" : "_" + variant)}.png".ImagePath();

    /// <summary>Картинка облика для текущего состояния героя (Звериная форма, Стойка лорда); нет варианта — основная.</summary>
    public static string? CurrentSprite(Player player, HeroLook look)
    {
        if (HeroLookRules.SpriteKey(look) is not { } key) return null;
        var creature = player.Creature;
        var beast = creature?.GetPower<BeastFormPower>() != null;
        var lord = creature?.GetPower<BloodLordStancePower>() != null;
        var path = SpritePath(key, HeroLookRules.SpriteVariant(look, beast, lord));
        return VanillaArt.Exists(path) ? path : SpritePath(key, null);
    }

    /// <summary>Узел облика или null (ванильная модель героя).</summary>
    public static NCreatureVisuals? Create(Player player)
    {
        var look = LookOf(player);
        if (CurrentSprite(player, look) is { } path)
        {
            if (!VanillaArt.Exists(path)) return null;
            var texture = ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Reuse);
            if (texture == null) return null;
            var visuals = NodeFactory<NCreatureVisuals>.CreateFromResource(texture);
            OwnArtAnimator.Attach(visuals);
            return visuals;
        }
        if (BorrowedCharacter(look) is { } character && character.GetType() != player.Character.GetType())
        {
            var visuals = character.CreateVisuals();
            Borrowed.AddOrUpdate(visuals, character);
            return visuals;
        }
        return null;
    }

    /// <summary>Узел — чужая ванильная модель (облик Лича или Заводного сердца).</summary>
    public static bool IsBorrowed(NCreatureVisuals? visuals) => visuals != null && Borrowed.TryGetValue(visuals, out _);

    /// <summary>Модель, чей аниматор нужен Spine-телу <paramref name="controller"/> (по предкам узла до корня визуала).</summary>
    public static CharacterModel? BorrowedFor(MegaSprite? controller)
    {
        var node = controller?.BoundObject as Node;
        for (var i = 0; node != null && i < 8; i++, node = node.GetParent())
            if (node is NCreatureVisuals visuals && Borrowed.TryGetValue(visuals, out var character))
                return character;
        return null;
    }

    /// <summary>Особые триггеры героев (тяжёлый удар Латоносца, Заточка, Клинок Суверена) — на общие анимации чужой модели.</summary>
    public static void AddFallbackTriggers(CreatureAnimator animator)
    {
        var idle = new AnimState("idle_loop", isLooping: true);
        var attack = new AnimState("attack") { NextState = idle };
        var cast = new AnimState("cast") { NextState = idle };
        foreach (var trigger in new[] { "heavyAttack", "Shiv" })
            if (!animator.HasTrigger(trigger)) animator.AddAnyState(trigger, attack);
        if (!animator.HasTrigger("sovereignBladeTrigger")) animator.AddAnyState("sovereignBladeTrigger", cast);
    }
}

/// <summary>Модель героя в бою (и на экране поражения) — облик формы.</summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.CreateVisuals))]
public static class HeroLookVisualsPatch
{
    [HarmonyPrefix]
    public static bool Prefix(Creature __instance, ref NCreatureVisuals? __result)
    {
        if (TestMode.IsOn || __instance.Player == null) return true;
        try
        {
            var visuals = HeroLooks.Create(__instance.Player);
            if (visuals == null) return true;
            __result = visuals;
            return false;
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"HeroLooks: {e.Message}");
            return true;
        }
    }
}

/// <summary>
/// Spine-аниматор облика: <c>NCreature._Ready</c> зовёт <c>GenerateAnimator</c> персонажа игрока (Латоносец, Безмолвная,
/// Регент), а тело — Некробиндер или Дефекта. Подменяем на аниматор хозяина модели (у них свои анимации: cast_mighty, process).
/// </summary>
public static class HeroLookAnimatorPatches
{
    [ThreadStatic] private static bool _redirecting;

    public static bool Redirect(CharacterModel instance, MegaSprite controller, ref CreatureAnimator result)
    {
        if (_redirecting) return true;
        try
        {
            var owner = HeroLooks.BorrowedFor(controller);
            if (owner == null || owner.GetType() == instance.GetType()) return true;
            _redirecting = true;
            result = owner.GenerateAnimator(controller);
            HeroLooks.AddFallbackTriggers(result);
            return false;
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"HeroLooks.GenerateAnimator: {e.Message}");
            return true;
        }
        finally
        {
            _redirecting = false;
        }
    }
}

[HarmonyPatch(typeof(CharacterModel), nameof(CharacterModel.GenerateAnimator))]
public static class HeroLookAnimatorBasePatch
{
    [HarmonyPrefix]
    public static bool Prefix(CharacterModel __instance, MegaSprite controller, ref CreatureAnimator __result) =>
        HeroLookAnimatorPatches.Redirect(__instance, controller, ref __result);
}

[HarmonyPatch(typeof(Ironclad), nameof(Ironclad.GenerateAnimator))]
public static class HeroLookAnimatorIroncladPatch
{
    [HarmonyPrefix]
    public static bool Prefix(CharacterModel __instance, MegaSprite controller, ref CreatureAnimator __result) =>
        HeroLookAnimatorPatches.Redirect(__instance, controller, ref __result);
}

[HarmonyPatch(typeof(Silent), nameof(Silent.GenerateAnimator))]
public static class HeroLookAnimatorSilentPatch
{
    [HarmonyPrefix]
    public static bool Prefix(CharacterModel __instance, MegaSprite controller, ref CreatureAnimator __result) =>
        HeroLookAnimatorPatches.Redirect(__instance, controller, ref __result);
}

[HarmonyPatch(typeof(Regent), nameof(Regent.GenerateAnimator))]
public static class HeroLookAnimatorRegentPatch
{
    [HarmonyPrefix]
    public static bool Prefix(CharacterModel __instance, MegaSprite controller, ref CreatureAnimator __result) =>
        HeroLookAnimatorPatches.Redirect(__instance, controller, ref __result);
}

/// <summary>
/// Накладки на модель героя в бою: знамя Знака Пути за спиной (06 §5 «Знак Пути — видимая часть экипировки») и
/// Сферы стихий Заводного сердца — спрайты ванильных сфер Дефекта на той же дуге над головой, что у NOrbManager
/// (сами сферы остаются силами <see cref="ElementOrbPower"/>: очередь сфер игры не трогаем).
/// </summary>
public static class HeroOverlays
{
    private const string SignNode = "RequiemPathSign";
    private const string OrbsNode = "RequiemElementOrbs";
    private static readonly StringName KeyMeta = "requiem_key";

    public static void Refresh(Player? player)
    {
        if (player?.Creature == null) return;
        var node = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
        if (node != null) Refresh(player, node);
    }

    public static void Refresh(Player player, NCreature node)
    {
        try
        {
            RefreshSign(player, node);
            RefreshOrbs(player, node);
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"HeroOverlays: {e.Message}");
        }
    }

    public static void RefreshOrbs(Creature? hero)
    {
        if (hero?.Player == null) return;
        try
        {
            var node = NCombatRoom.Instance?.GetCreatureNode(hero);
            if (node != null) RefreshOrbs(hero.Player, node);
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"HeroOverlays.Orbs: {e.Message}");
        }
    }

    // ───── Знак Пути ─────

    private static void RefreshSign(Player player, NCreature node)
    {
        var visuals = node.Visuals;
        if (visuals == null) return;
        var sign = RequiemMode.IsInSystem(player) ? player.Relics.OfType<PathSign>().FirstOrDefault(s => s.IsEquipped) : null;
        var key = sign == null ? null : HeroLookRules.PathSignKey(sign.Path);
        var path = key == null ? null : $"ui/path_signs/{key}.png".ImagePath();
        var existing = visuals.GetNodeOrNull<Sprite2D>(SignNode);
        if (path == null || !VanillaArt.Exists(path))
        {
            existing?.QueueFree();
            return;
        }
        if (existing != null && existing.GetMeta(KeyMeta, "").AsString() == key) return;
        existing?.QueueFree();
        var texture = ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Reuse);
        if (texture == null) return;
        var banner = new Sprite2D
        {
            Name = SignNode,
            Texture = texture,
            Centered = false,
            Offset = new Vector2(-14f, -8f), // точка вращения — верх древка
            Scale = Vector2.One * HeroLookRules.SignScale,
        };
        banner.SetMeta(KeyMeta, key!);
        var bounds = visuals.Bounds;
        banner.Position = bounds.Position + bounds.Size * new Vector2(HeroLookRules.SignAnchorX, HeroLookRules.SignAnchorY);
        visuals.AddChild(banner);
        visuals.MoveChild(banner, 0); // за телом героя
        banner.Modulate = new Color(1f, 1f, 1f, 0f);
        var appear = banner.CreateTween();
        appear.TweenProperty(banner, "modulate", Colors.White, 0.5);
        // лёгкое колыхание знамени
        var sway = banner.CreateTween().SetLoops();
        sway.TweenProperty(banner, "rotation", 0.05f, 1.8).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        sway.TweenProperty(banner, "rotation", -0.03f, 1.8).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    // ───── Сферы стихий ─────

    private static void RefreshOrbs(Player player, NCreature node)
    {
        var hero = player.Creature;
        var steam = hero.GetPower<SteamOrbPower>()?.Amount ?? 0;
        var spark = hero.GetPower<SparkOrbPower>()?.Amount ?? 0;
        var rime = hero.GetPower<RimeOrbPower>()?.Amount ?? 0;
        var orbs = HeroLookRules.OrbSequence(steam, spark, rime);
        var key = string.Join(",", orbs);
        var container = node.GetNodeOrNull<Node2D>(OrbsNode);
        if (orbs.Count == 0)
        {
            container?.QueueFree();
            return;
        }
        if (container != null && container.GetMeta(KeyMeta, "").AsString() == key) return;
        var isLocal = LocalContext.IsMe(player);
        if (container == null)
        {
            container = new Node2D { Name = OrbsNode };
            node.AddChild(container);
        }
        container.SetMeta(KeyMeta, key);
        var visuals = node.Visuals;
        if (visuals?.OrbPosition != null)
        {
            container.Position = visuals.OrbPosition.Position * Mathf.Min(visuals.Scale.X, 1.25f);
            if (!isLocal) container.Position += Vector2.Up * 50f;
        }
        foreach (var child in container.GetChildren()) child.QueueFree();
        for (var i = 0; i < orbs.Count; i++)
        {
            var (x, y) = HeroLookRules.OrbSlot(i, TransformationRules.MaxOrbs, isLocal);
            var holder = new Node2D { Position = new Vector2(x, y) };
            container.AddChild(holder);
            var sprite = CreateOrbSprite(orbs[i]);
            if (sprite == null) continue;
            holder.AddChild(sprite);
            if (orbs[i] == Element.Fire) holder.Modulate = new Color(1f, 0.78f, 0.55f); // пар: тёплая плазма
            if (sprite.GetNodeOrNull("SpineSkeleton") is { } skeleton)
                holder.RunWhenSpineReady(new MegaSprite(skeleton), state => state.SetAnimation("idle_loop"));
            sprite.Scale = Vector2.Zero;
            var pop = sprite.CreateTween();
            pop.TweenProperty(sprite, "scale", Vector2.One, 0.5).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }
    }

    private static Node2D? CreateOrbSprite(Element element)
    {
        try
        {
            OrbModel model = HeroLookRules.OrbArt(element) switch
            {
                "PlasmaOrb" => ModelDb.Orb<PlasmaOrb>(),
                "LightningOrb" => ModelDb.Orb<LightningOrb>(),
                _ => ModelDb.Orb<FrostOrb>(),
            };
            return model.CreateSprite();
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"HeroOverlays.OrbSprite: {e.Message}");
            return null;
        }
    }
}
