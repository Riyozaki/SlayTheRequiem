using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using SlayTheRequiem.SlayTheRequiemCode.Art;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Extensions;
using SlayTheRequiem.SlayTheRequiemCode.Monsters;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Transformations;
using SlayTheRequiem.SlayTheRequiemCode.UI;

namespace SlayTheRequiem.SlayTheRequiemCode.Equipment;

/// <summary>
/// Кукла героя в бою (docs/design/07 §8, M6 п. 4), MVP: оружие правой руки — впереди, в руке; щит или посох левой руки — за
/// телом у дальней руки; оба цвета материала (<see cref="PaperDollRules.MaterialColor"/>, спрайты images/ui/doll/: основа +
/// светлый металл под множителем цвета). Аура брони — мягкое сияние за героем: цвет — материал лучшей части, стиль — класс
/// (латы ровно, лёгкая мерцает, одеяния переливаются), сила — число частей; Бездонное дымит, серебро бликует.
/// Узлы — дети визуала героя (двигаются и масштабируются с моделью); строятся заново, только если предмет сменился.
/// Удар героя взмахивает оружием, получение урона встряхивает щит (<see cref="PaperDollTriggerPatch"/>).
/// Свои спрайты форм (вампир, оборотень) держат своё оружие — на них только аура. Настройка «Снаряжение на модели героя».
/// </summary>
public static class PaperDoll
{
    private const string MainNode = "RequiemDollMainHand";
    private const string OffNode = "RequiemDollOffHand";
    private const string AuraNode = "RequiemDollAura";
    private static readonly StringName KeyMeta = "requiem_doll";
    private const string ActionPose = "ActionPose";
    private static readonly ConditionalWeakTable<Node2D, Tween> Actions = new();
    private sealed class LifeState { internal bool Dead; }
    private sealed class DeathFade { internal bool Dead; internal Tween? Tween; }
    private static readonly ConditionalWeakTable<NCreatureVisuals, LifeState> Life = new();
    private static readonly ConditionalWeakTable<Node2D, DeathFade> DeathFades = new();
    private static GradientTexture2D? _glow;

    private static void SetDead(Node2D part, bool dead)
    {
        var state = DeathFades.GetValue(part, owner =>
        {
            var next = new DeathFade();
            void Finish()
            {
                RequiemMotion.Kill(next.Tween); next.Tween = null;
                if (next.Dead) owner.SelfModulate = new Color(1, 1, 1, 0);
            }
            RequiemMotion.Watch(owner, () => { if (!RequiemMotion.Enabled) Finish(); });
            owner.TreeExiting += Finish;
            return next;
        });
        state.Dead = dead; RequiemMotion.Kill(state.Tween); state.Tween = null;
        if (!dead) { part.SelfModulate = Colors.White; return; }
        if (part.GetNodeOrNull<Node2D>(ActionPose) is { } pose) ResetAction(pose);
        // self_modulate is independent of aura pulses/material tint. The previous Modulate fade raced them.
        if (RequiemMotion.Enabled && part.IsInsideTree())
        {
            state.Tween = part.CreateTween();
            state.Tween.TweenProperty(part, "self_modulate:a", 0f, 0.6);
        }
        else part.SelfModulate = new Color(1, 1, 1, 0);
    }

    private static void ResetAction(Node2D pose)
    {
        if (Actions.TryGetValue(pose, out var active)) RequiemMotion.Kill(active);
        pose.Rotation = 0; pose.Position = Vector2.Zero;
    }
    private static Tween BeginAction(Node2D pose)
    {
        ResetAction(pose);
        var tween = pose.CreateTween(); Actions.AddOrUpdate(pose, tween); return tween;
    }

    public static void Refresh(Player? player)
    {
        if (player?.Creature == null) return;
        try
        {
            var node = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
            if (node != null) Refresh(player, node);
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"PaperDoll: {e.Message}");
        }
    }

    public static void Refresh(Player player, NCreature node)
    {
        try
        {
            var visuals = node.Visuals;
            if (visuals == null || !GodotObject.IsInstanceValid(visuals)) return;
            var on = RequiemConfig.ShowPaperDoll && RequiemMode.IsInSystem(player);
            var beast = player.Creature.GetPower<BeastFormPower>() != null;
            var weapons = on && PaperDollRules.ShowsWeapons(OwnArtAnimator.IsOwnArt(visuals), beast);

            var main = weapons ? Loadout.InSlot(player, EquipSlot.MainHand) : null;
            var mainKind = main?.Weapon ?? WeaponKind.None;
            var off = weapons && !PaperDollRules.BlocksOffHand(mainKind) ? Loadout.InSlot(player, EquipSlot.OffHand) : null;
            SetItem(visuals, MainNode, main, mainHand: true);
            SetItem(visuals, OffNode, off, mainHand: false);

            var aura = on
                ? PaperDollRules.AuraFor(Loadout.Equipped(player)
                    .Where(e => EquipmentRules.IsArmorSlot(e.Slot))
                    .Select(e => (e.Armor, e.Tier, e.EffectiveMaterial)))
                : ArmorAura.None;
            SetAura(visuals, aura);
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"PaperDoll: {e.Message}");
        }
    }

    private static Color Tint(ItemMaterial material, float alpha = 1f)
    {
        var (r, g, b) = PaperDollRules.MaterialColor(material);
        return new Color(r, g, b, alpha);
    }

    private static string DollPath(string key) => $"ui/doll/{key}.png".ImagePath();

    private static Texture2D? Load(string path) =>
        VanillaArt.Exists(path) ? ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Reuse) : null;

    /// <summary>Индекс тела героя среди детей визуала: накладки «за телом» встают прямо под него (знамя Пути — ещё глубже).</summary>
    private static int BodyIndex(NCreatureVisuals visuals)
    {
        Node? node = visuals.GetCurrentBody();
        while (node != null && node.GetParent() != visuals) node = node.GetParent();
        return node?.GetIndex() ?? 0;
    }

    // ───── оружие и щит ─────

    private static void SetItem(NCreatureVisuals visuals, string name, EquipmentRelic? item, bool mainHand)
    {
        var kind = item?.Weapon ?? WeaponKind.None;
        var art = PaperDollRules.WeaponArt(kind);
        var material = item?.EffectiveMaterial ?? ItemMaterial.None;
        var key = art == null ? null : $"{art}:{material}";
        var existing = visuals.GetNodeOrNull<Node2D>(name);
        if (existing != null && key != null && existing.GetMeta(KeyMeta, "").AsString() == key) return;
        existing?.QueueFree();
        if (art == null) return;
        var baseTexture = Load(DollPath(art));
        var metalTexture = Load(DollPath(art + "_metal"));
        if (baseTexture == null && metalTexture == null) return;

        var (px, py) = PaperDollRules.Pivot(kind);
        var offset = new Vector2(-px, -py);
        var (ax, ay) = PaperDollRules.HandAnchor(mainHand);
        var bounds = visuals.Bounds;
        var holder = new Node2D
        {
            Name = name,
            Position = bounds.Position + bounds.Size * new Vector2(ax, ay),
            Rotation = PaperDollRules.RestAngle(kind),
            Scale = Vector2.One * PaperDollRules.WeaponScale(kind),
        };
        holder.SetMeta(KeyMeta, key!);
        // Idle sway belongs to the hand holder; attacks/recoil to a child pose. Two tweens must not fight for
        // the same rotation, or repeated/multihit triggers accumulate a displaced resting angle.
        var pose = new Node2D { Name = ActionPose };
        holder.AddChild(pose);
        if (baseTexture != null)
            pose.AddChild(new Sprite2D { Texture = baseTexture, Centered = false, Offset = offset });
        Sprite2D? metal = null;
        if (metalTexture != null)
        {
            metal = new Sprite2D { Texture = metalTexture, Centered = false, Offset = offset, Modulate = Tint(material) };
            pose.AddChild(metal);
        }
        visuals.AddChild(holder);
        if (!mainHand) visuals.MoveChild(holder, BodyIndex(visuals)); // левая рука — за телом

        var rest = holder.Rotation;
        RequiemMotion.Decorate(holder, () =>
        {
            holder.Rotation = rest; ResetAction(pose);
            if (metal != null) metal.SelfModulate = Colors.White;
        }, () =>
        {
            var sway = holder.CreateTween().SetLoops();
            sway.TweenProperty(holder, "rotation", rest + 0.035f, 1.7).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            sway.TweenProperty(holder, "rotation", rest - 0.025f, 1.7).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            if (metal == null || !PaperDollRules.HasGlint(material)) return [sway];
            var glint = metal.CreateTween().SetLoops();
            glint.TweenInterval(2.6 + (mainHand ? 0 : 1.1));
            glint.TweenProperty(metal, "self_modulate", new Color(1.8f, 1.8f, 1.8f), 0.12);
            glint.TweenProperty(metal, "self_modulate", Colors.White, 0.35);
            return [sway, glint];
        });
    }

    // ───── аура брони ─────

    private static GradientTexture2D Glow()
    {
        if (_glow != null && GodotObject.IsInstanceValid(_glow)) return _glow;
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(1f, 1f, 1f, 1f));
        gradient.SetColor(1, new Color(1f, 1f, 1f, 0f));
        gradient.AddPoint(0.45f, new Color(1f, 1f, 1f, 0.55f));
        _glow = new GradientTexture2D
        {
            Gradient = gradient,
            Width = 256,
            Height = 256,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(0.5f, 0f),
        };
        return _glow;
    }

    private static void SetAura(NCreatureVisuals visuals, ArmorAura aura)
    {
        var key = aura.IsVisible ? $"{aura.Class}:{aura.Material}:{aura.Strength:0.00}" : null;
        var existing = visuals.GetNodeOrNull<Node2D>(AuraNode);
        if (existing != null && key != null && existing.GetMeta(KeyMeta, "").AsString() == key) return;
        existing?.QueueFree();
        if (key == null) return;

        var style = PaperDollRules.StyleFor(aura.Class);
        var alpha = PaperDollRules.AuraAlpha(style) * aura.Strength;
        var bounds = visuals.Bounds;
        var root = new Node2D { Name = AuraNode, Position = bounds.Position + bounds.Size * new Vector2(0.5f, 0.58f) };
        root.SetMeta(KeyMeta, key);
        var texture = Glow();
        var glow = new Sprite2D
        {
            Texture = texture,
            Scale = new Vector2(bounds.Size.X * 1.5f / 256f, bounds.Size.Y * 1.2f / 256f),
            Modulate = Tint(aura.Material, alpha),
        };
        root.AddChild(glow);
        visuals.AddChild(root);
        visuals.MoveChild(root, BodyIndex(visuals));

        var period = PaperDollRules.AuraPulseSeconds(style);
        var low = Tint(aura.Material, alpha * (style == AuraStyle.Flicker ? 0.45f : 0.65f));
        var high = Tint(aura.Material, alpha);
        var baseScale = glow.Scale;
        RequiemMotion.Decorate(glow, () =>
        {
            glow.Visible = RequiemConfig.CombatVfx;
            glow.Modulate = high; glow.Scale = baseScale; // static material cue in reduced motion
        }, () =>
        {
            var pulse = glow.CreateTween().SetLoops();
            pulse.TweenProperty(glow, "modulate", low, period * 0.5).SetTrans(Tween.TransitionType.Sine);
            pulse.TweenProperty(glow, "modulate", high, period * 0.5).SetTrans(Tween.TransitionType.Sine);
            if (style != AuraStyle.Shimmer) return [pulse];
            var breathe = glow.CreateTween().SetLoops();
            breathe.TweenProperty(glow, "scale", baseScale * 1.08f, period).SetTrans(Tween.TransitionType.Sine);
            breathe.TweenProperty(glow, "scale", baseScale, period).SetTrans(Tween.TransitionType.Sine);
            return [pulse, breathe];
        }, effectsOnly: true);
        if (PaperDollRules.HasSmoke(aura.Material)) AddSmoke(root, texture, bounds.Size);
    }

    /// <summary>Бездонное: три тёмных клуба дыма поднимаются и тают.</summary>
    private static void AddSmoke(Node2D root, Texture2D texture, Vector2 size)
    {
        for (var i = 0; i < 3; i++)
        {
            var puff = new Sprite2D
            {
                Texture = texture,
                Scale = Vector2.One * (size.X * 0.45f / 256f),
                Modulate = new Color(0.12f, 0.02f, 0.04f, 0f),
            };
            root.AddChild(puff);
            var x = (i - 1) * size.X * 0.28f;
            var start = new Vector2(x, size.Y * 0.3f);
            var end = new Vector2(x * 1.3f, -size.Y * 0.45f);
            var delay = i * 0.9;
            RequiemMotion.Decorate(puff, () =>
            {
                puff.Position = start; puff.Modulate = new Color(0.12f, 0.02f, 0.04f, 0f);
            }, () =>
            {
                var tween = puff.CreateTween().SetLoops();
                tween.TweenInterval(delay);
                tween.TweenCallback(Callable.From(() => puff.Position = start));
                tween.TweenProperty(puff, "modulate", new Color(0.12f, 0.02f, 0.04f, 0.55f), 0.6);
                tween.Parallel().TweenProperty(puff, "position", end, 2.7).SetTrans(Tween.TransitionType.Sine);
                tween.TweenProperty(puff, "modulate", new Color(0.12f, 0.02f, 0.04f, 0f), 0.6);
                return [tween];
            }, effectsOnly: true);
        }
    }

    // ───── реакции на анимации героя ─────

    public static void OnTrigger(NCreature node, string trigger)
    {
        try
        {
            var visuals = node.Visuals;
            if (visuals == null || !GodotObject.IsInstanceValid(visuals)) return;
            var life = Life.GetValue(visuals, _ => new LifeState());
            if (life.Dead && trigger is not ("Revive" or "Dead")) return;
            switch (trigger)
            {
                case "Attack" or "heavyAttack" or "Shiv":
                    Swing(visuals.GetNodeOrNull<Node2D>(MainNode)?.GetNodeOrNull<Node2D>(ActionPose));
                    break;
                case "Hit":
                    Bump(visuals.GetNodeOrNull<Node2D>(OffNode)?.GetNodeOrNull<Node2D>(ActionPose));
                    break;
                case "Dead" or "Revive":
                    life.Dead = trigger == "Dead";
                    foreach (var name in new[] { MainNode, OffNode, AuraNode })
                        if (visuals.GetNodeOrNull<Node2D>(name) is { } part) SetDead(part, life.Dead);
                    break;
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"PaperDoll.{trigger}: {e.Message}");
        }
    }

    /// <summary>Взмах: замах назад, удар вперёд, возврат.</summary>
    private static void Swing(Node2D? weapon)
    {
        if (weapon == null || !weapon.IsInsideTree() || !RequiemMotion.Enabled) return;
        const float rest = 0;
        var tween = BeginAction(weapon);
        tween.TweenProperty(weapon, "rotation", rest - 0.55f, 0.1).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(weapon, "rotation", rest + 1.25f, 0.12).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        tween.TweenInterval(0.08);
        tween.TweenProperty(weapon, "rotation", rest, 0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    /// <summary>Удар по щиту: короткая тряска.</summary>
    private static void Bump(Node2D? shield)
    {
        if (shield == null || !shield.IsInsideTree() || !RequiemMotion.Enabled) return;
        var at = Vector2.Zero;
        var tween = BeginAction(shield);
        tween.TweenProperty(shield, "position", at + new Vector2(-10f, 0f), 0.05);
        tween.TweenProperty(shield, "position", at + new Vector2(5f, 0f), 0.06);
        tween.TweenProperty(shield, "position", at, 0.08);
    }
}

/// <summary>Анимации героя (удар, получение урона, смерть) двигают куклу — и у Spine-моделей, и у спрайтовых обликов.</summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
public static class PaperDollTriggerPatch
{
    [HarmonyPostfix]
    public static void Postfix(NCreature __instance, string trigger)
    {
        if (__instance.Entity?.Player != null) PaperDoll.OnTrigger(__instance, trigger);
    }
}

/// <summary>У спрайтовых обликов смерть идёт без триггера «Dead» — гасим куклу и здесь.</summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim))]
public static class PaperDollDeathPatch
{
    [HarmonyPrefix]
    public static void Prefix(NCreature __instance)
    {
        if (__instance.Entity?.Player != null) PaperDoll.OnTrigger(__instance, "Dead");
    }
}

// Native StartDeathAnim does not send a Dead trigger for a non-Spine body. Repeated Spine notifications are
// idempotent too: SetDead owns/cancels its previous fade. Revive explicitly restores equipment presentation.
[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim))]
public static class PaperDollDeathPatch
{
    [HarmonyPrefix]
    public static void Prefix(NCreature __instance)
    {
        if (__instance.Entity?.Player != null) PaperDoll.OnTrigger(__instance, "Dead");
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartReviveAnim))]
public static class PaperDollRevivePatch
{
    [HarmonyPostfix]
    public static void Postfix(NCreature __instance)
    {
        if (__instance.Entity?.Player != null) PaperDoll.OnTrigger(__instance, "Revive");
    }
}
