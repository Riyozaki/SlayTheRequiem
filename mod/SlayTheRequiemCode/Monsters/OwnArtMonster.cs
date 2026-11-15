using System.Runtime.CompilerServices;
using BaseLib.Utils.NodeFactories;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using SlayTheRequiem.SlayTheRequiemCode.Art;
using SlayTheRequiem.SlayTheRequiemCode.Extensions;

namespace SlayTheRequiem.SlayTheRequiemCode.Monsters;

/// <summary>
/// Враг со своим статичным спрайтом (M5, docs/plan/art-ui-animation-spec.md §3.9): PNG с прозрачным фоном
/// <c>images/monsters/{ArtKey}.png</c> + варианты <c>{ArtKey}_{variant}.png</c> (фаза 2, стойка) того же размера холста.
/// Узел строит BaseLib (<see cref="NodeFactory{T}.CreateFromResource"/>: Sprite2D «Visuals» + Bounds ×1.1), «анимации» —
/// твины <see cref="OwnArtAnimator"/>: дыхание, выпад, вспышка удара, каст, растворение при смерти, смена картинки.
/// Нет файла — откат на облик ванильного врага (<see cref="RequiemMonster.LookOf"/>), чтобы бой не ломался.
/// </summary>
public abstract class OwnArtMonster : RequiemMonster
{
    /// <summary>Имя файла без расширения в <c>images/monsters/</c> (snake_case; его же ищет tools/assets/asset_manifest.py).</summary>
    public abstract string ArtKey { get; }

    /// <summary>Дополнительные картинки (суффиксы), которые нужно подгрузить заранее.</summary>
    protected virtual IReadOnlyList<string> ArtVariants => [];

    public string ArtPath(string? variant = null) =>
        $"monsters/{ArtKey}{(string.IsNullOrEmpty(variant) ? "" : "_" + variant)}.png".ImagePath();

    public bool HasOwnArt => VanillaArt.Exists(ArtPath());

    public override string? CustomVisualPath => HasOwnArt ? null : base.CustomVisualPath;

    public override CreatureAnimator? SetupCustomAnimationStates(MegaSprite controller) =>
        HasOwnArt ? null : base.SetupCustomAnimationStates(controller);

    public override NCreatureVisuals? CreateCustomVisuals()
    {
        if (!HasOwnArt) return null;
        var texture = ResourceLoader.Load<Texture2D>(ArtPath(), null, ResourceLoader.CacheMode.Reuse);
        if (texture == null) return null;
        var visuals = NodeFactory<NCreatureVisuals>.CreateFromResource(texture);
        OwnArtAnimator.Attach(visuals);
        return visuals;
    }

    /// <summary>Своя картинка вместо сцены облика: сцену из пути визуала не грузим, PNG (и варианты) — подгружаем.</summary>
    public override IEnumerable<string> AssetPaths
    {
        get
        {
            if (!HasOwnArt) return base.AssetPaths;
            var skip = VisualsPath;
            return base.AssetPaths.Where(p => p != skip)
                .Append(ArtPath())
                .Concat(ArtVariants.Select(v => ArtPath(v)).Where(VanillaArt.Exists))
                .ToList();
        }
    }

    /// <summary>Растворение (<see cref="OwnArtAnimator.DeathSeconds"/>) успевает доиграть до удаления узла.</summary>
    public override float DeathAnimLengthOverride => HasOwnArt ? OwnArtAnimator.DeathSeconds : base.DeathAnimLengthOverride;

    /// <summary>Сменить картинку (фаза, стойка) со вспышкой; null — основная.</summary>
    protected void ShowArt(string? variant)
    {
        if (!HasOwnArt) return;
        var path = ArtPath(variant);
        if (!VanillaArt.Exists(path)) return;
        OwnArtAnimator.Swap(Creature, path);
    }
}

/// <summary>
/// Код-анимации статичных спрайтов. Всё на Sprite2D «Visuals»: дыхание — scale/position (низ спрайта прижат к земле),
/// выпад и тряска — offset, вспышки — self_modulate (Modulate узла остаётся игре: жидкость, оттенки форм).
/// </summary>
public static class OwnArtAnimator
{
    public const float DeathSeconds = 0.9f;
    public const float BreathScale = 0.022f;
    public const float BreathSeconds = 1.6f;
    public const float LungeDistance = 70f;

    private static readonly ConditionalWeakTable<Sprite2D, Tween> Idle = new();
    private static readonly ConditionalWeakTable<Sprite2D, Tween> Action = new();

    public static void Attach(NCreatureVisuals visuals) =>
        visuals.TreeEntered += () => { if (Body(visuals) is { } body) StartIdle(body); };

    private static Sprite2D? Body(NCreatureVisuals? visuals) => visuals?.GetNodeOrNull<Sprite2D>("%Visuals");

    private static float HalfHeight(Sprite2D body) => body.Texture == null ? 0f : body.Texture.GetSize().Y * 0.5f;

    private static void StartIdle(Sprite2D body)
    {
        if (!body.IsInsideTree()) return;
        if (Idle.TryGetValue(body, out var old) && old.IsValid()) old.Kill();
        var h = HalfHeight(body);
        body.Scale = Vector2.One;
        body.Position = new Vector2(0, -h);
        // Сдвиг фазы — чтобы несколько одинаковых спрайтов не дышали в унисон.
        var phase = (float)(body.GetInstanceId() % 7) * 0.11f;
        var tween = body.CreateTween().SetLoops();
        tween.TweenInterval(phase);
        var inhale = new Vector2(1f - BreathScale * 0.4f, 1f + BreathScale);
        tween.TweenProperty(body, "scale", inhale, BreathSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tween.Parallel().TweenProperty(body, "position", new Vector2(0, -h * inhale.Y), BreathSeconds)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(body, "scale", Vector2.One, BreathSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tween.Parallel().TweenProperty(body, "position", new Vector2(0, -h), BreathSeconds)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        Idle.AddOrUpdate(body, tween);
    }

    private static Tween NewAction(Sprite2D body)
    {
        if (Action.TryGetValue(body, out var old) && old.IsValid()) old.Kill();
        body.Offset = Vector2.Zero;
        body.SelfModulate = Colors.White;
        body.Rotation = 0f;
        var tween = body.CreateTween();
        Action.AddOrUpdate(body, tween);
        return tween;
    }

    /// <summary>Триггер анимации от игры («Attack», «Cast», «Hit», «Dead», «Revive» и свои имена ходов).</summary>
    public static void Play(NCreatureVisuals visuals, string trigger)
    {
        var body = Body(visuals);
        if (body == null || !body.IsInsideTree()) return;
        try
        {
            switch (trigger)
            {
                case "Dead": Die(body); break;
                case "Revive": Revive(body); break;
                case "Hit": Hurt(body); break;
                case "Cast": Cast(body); break;
                case "Idle": break;
                default: Lunge(body); break; // «Attack» и все прочие ударные анимации
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"OwnArtAnimator.{trigger}: {e.Message}");
        }
    }

    /// <summary>Выпад к героям (враги стоят справа — влево), затем возврат.</summary>
    private static void Lunge(Sprite2D body)
    {
        var tween = NewAction(body);
        tween.TweenProperty(body, "offset", new Vector2(18f, 4f), 0.08).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(body, "offset", new Vector2(-LungeDistance, 0f), 0.10).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        tween.Parallel().TweenProperty(body, "rotation", -0.06f, 0.10);
        tween.TweenInterval(0.06);
        tween.TweenProperty(body, "offset", Vector2.Zero, 0.30).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(body, "rotation", 0f, 0.30);
    }

    /// <summary>Каст: свечение и лёгкий подъём.</summary>
    private static void Cast(Sprite2D body)
    {
        var tween = NewAction(body);
        tween.TweenProperty(body, "self_modulate", new Color(1.45f, 1.35f, 1.7f), 0.18).SetTrans(Tween.TransitionType.Sine);
        tween.Parallel().TweenProperty(body, "offset", new Vector2(0f, -14f), 0.18).SetTrans(Tween.TransitionType.Sine);
        tween.TweenInterval(0.12);
        tween.TweenProperty(body, "self_modulate", Colors.White, 0.35);
        tween.Parallel().TweenProperty(body, "offset", Vector2.Zero, 0.35).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
    }

    /// <summary>Получил удар: красно-белая вспышка и тряска назад.</summary>
    private static void Hurt(Sprite2D body)
    {
        var tween = NewAction(body);
        body.SelfModulate = new Color(2.2f, 1.5f, 1.5f);
        tween.TweenProperty(body, "offset", new Vector2(16f, 0f), 0.05);
        tween.TweenProperty(body, "offset", new Vector2(-8f, 0f), 0.06);
        tween.TweenProperty(body, "offset", new Vector2(4f, 0f), 0.05);
        tween.TweenProperty(body, "offset", Vector2.Zero, 0.06);
        tween.Parallel().TweenProperty(body, "self_modulate", Colors.White, 0.22);
    }

    /// <summary>Смерть: потемнение, оседание и растворение.</summary>
    private static void Die(Sprite2D body)
    {
        if (Idle.TryGetValue(body, out var idle) && idle.IsValid()) idle.Kill();
        var tween = NewAction(body);
        var h = HalfHeight(body);
        body.SelfModulate = new Color(2f, 2f, 2f);
        tween.TweenProperty(body, "self_modulate", new Color(0.55f, 0.5f, 0.6f, 0.85f), 0.25);
        tween.TweenProperty(body, "scale", new Vector2(1.06f, 0.82f), DeathSeconds - 0.25f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.Parallel().TweenProperty(body, "position", new Vector2(0, -h * 0.82f), DeathSeconds - 0.25f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.Parallel().TweenProperty(body, "self_modulate", new Color(0.3f, 0.25f, 0.35f, 0f), DeathSeconds - 0.25f);
    }

    private static void Revive(Sprite2D body)
    {
        var tween = NewAction(body);
        body.SelfModulate = new Color(1f, 1f, 1f, 0f);
        tween.TweenProperty(body, "self_modulate", Colors.White, 0.4);
        tween.TweenCallback(Callable.From(() => StartIdle(body)));
    }

    /// <summary>Смена картинки: белая вспышка, на пике — новая текстура, затем «вдох».</summary>
    public static void Swap(Creature creature, string texturePath)
    {
        try
        {
            var body = Body(NCombatRoom.Instance?.GetCreatureNode(creature)?.Visuals);
            if (body == null || !body.IsInsideTree()) return;
            var texture = ResourceLoader.Load<Texture2D>(texturePath, null, ResourceLoader.CacheMode.Reuse);
            if (texture == null || texture == body.Texture) return;
            var tween = NewAction(body);
            tween.TweenProperty(body, "self_modulate", new Color(3f, 3f, 3.2f), 0.16);
            tween.TweenCallback(Callable.From(() => { body.Texture = texture; StartIdle(body); }));
            tween.TweenProperty(body, "offset", new Vector2(0f, -10f), 0.12);
            tween.TweenProperty(body, "self_modulate", Colors.White, 0.4);
            tween.Parallel().TweenProperty(body, "offset", Vector2.Zero, 0.4).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"OwnArtAnimator.Swap: {e.Message}");
        }
    }
}

/// <summary>У спрайтовых врагов нет Spine-аниматора — триггеры игры переводим в твины.</summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
public static class OwnArtTriggerPatch
{
    [HarmonyPostfix]
    public static void Postfix(NCreature __instance, string trigger)
    {
        if (__instance.Entity?.Monster is OwnArtMonster && !__instance.HasSpineAnimation)
            OwnArtAnimator.Play(__instance.Visuals, trigger);
    }
}

/// <summary>Без Spine игра не шлёт «Dead» — запускаем растворение сами.</summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim))]
public static class OwnArtDeathPatch
{
    [HarmonyPrefix]
    public static void Prefix(NCreature __instance)
    {
        if (__instance.Entity?.Monster is OwnArtMonster && !__instance.HasSpineAnimation)
            OwnArtAnimator.Play(__instance.Visuals, "Dead");
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartReviveAnim))]
public static class OwnArtRevivePatch
{
    [HarmonyPostfix]
    public static void Postfix(NCreature __instance)
    {
        if (__instance.Entity?.Monster is OwnArtMonster && !__instance.HasSpineAnimation)
            OwnArtAnimator.Play(__instance.Visuals, "Revive");
    }
}
