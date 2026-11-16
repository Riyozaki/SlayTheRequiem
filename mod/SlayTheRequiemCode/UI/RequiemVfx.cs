using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.TestSupport;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>
/// Боевые эффекты Реквиема (M7 п. 4, art spec §5): ванильные эффекты игры с перекраской. Что и каким цветом — решает
/// <see cref="VfxRules"/>; здесь только узлы Godot. Любая ошибка эффекта гасится (одна строка в лог) — бой не ломается.
/// Настройка «Боевые эффекты Реквиема» (<see cref="RequiemConfig.CombatVfx"/>) выключает всё разом. Методы событий заодно
/// играют звук (<see cref="RequiemSfx"/>, M7 п. 5) — по своей настройке, независимо от эффектов.
/// </summary>
public static class RequiemVfx
{
    public static bool Enabled => RequiemConfig.CombatVfx && !TestMode.IsOn;

    private static readonly ConditionalWeakTable<Creature, Dictionary<HitFxKind, ulong>> LastHit = new();
    private static readonly ConditionalWeakTable<NCreatureVisuals, Pose> Poses = new();
    private static readonly HashSet<string> Reported = [];
    private static readonly AsyncLocal<bool> QuietScope = new();

    /// <summary>Внутри блока удары не дают вспышек стихий (у события свой эффект — например, ♥ за надрыв).</summary>
    public static IDisposable Quiet()
    {
        var previous = QuietScope.Value;
        QuietScope.Value = true;
        return new Restore(previous);
    }

    public static bool IsQuiet => QuietScope.Value;

    private sealed class Restore(bool previous) : IDisposable
    {
        public void Dispose() => QuietScope.Value = previous;
    }

    /// <summary>Покой модели до наших твинов: берётся заново, только когда наш твин не идёт (модель могли передвинуть).</summary>
    private sealed class Pose
    {
        public Vector2 Position;
        public Vector2 Scale;
        public Tween? Active;
    }

    private static (Pose Pose, Tween Tween) Animate(NCreatureVisuals visuals)
    {
        var pose = Poses.GetValue(visuals, _ => new Pose());
        if (pose.Active == null || !pose.Active.IsValid() || !pose.Active.IsRunning())
        {
            pose.Position = visuals.Position;
            pose.Scale = visuals.Scale;
        }
        pose.Active?.Kill();
        pose.Active = visuals.CreateTween();
        return (pose, pose.Active);
    }

    private static Color Tint(string hex, float alpha = 1f) => new(hex) { A = alpha };

    private static void Safe(string what, Action action)
    {
        if (!Enabled) return;
        try { action(); }
        catch (Exception e)
        {
            if (Reported.Add(what)) MainFile.Logger.Info($"[SlayTheRequiem] VFX {what}: {e.Message}");
        }
    }

    private static void Add(Creature target, Node? vfx, float scale = 1f)
    {
        if (vfx == null) return;
        if (vfx is Node2D node && Math.Abs(scale - 1f) > 0.01f) node.Scale *= scale;
        (target.GetVfxContainer() ?? NCombatRoom.Instance?.CombatVfxContainer)?.AddChildSafely(vfx);
    }

    private static Node? Modulated(Node? vfx, Color color)
    {
        if (vfx is CanvasItem item) item.Modulate = color;
        return vfx;
    }

    // ───── удары по типу урона ─────

    /// <summary>Вспышка стихии поверх ванильного эффекта удара (вызывается, когда удар уже записан в историю боя).</summary>
    public static void Hit(Creature target, DamageType type, bool poweredAttack, int totalDamage) => Safe("hit", () =>
    {
        if (VfxRules.For(type, poweredAttack, totalDamage) is not { } fx || target.GetCreatureNode() is not { } node) return;
        var last = LastHit.GetOrCreateValue(target);
        var now = Time.GetTicksMsec();
        if (VfxRules.Throttled(last.GetValueOrDefault(fx.Kind), now)) return;
        last[fx.Kind] = now;

        var tint = Tint(fx.Tint);
        Node? vfx = fx.Kind switch
        {
            HitFxKind.FireBurst => NFireBurstVfx.Create(target, 0.7f * fx.Scale),
            HitFxKind.FrostMist => NGaseousImpactVfx.Create(target, tint),
            HitFxKind.SparkSpray => NDaggerSprayImpactVfx.Create(target, tint, goingRight: target.IsEnemy),
            HitFxKind.PoisonCloud => NPoisonImpactVfx.Create(target),
            HitFxKind.BlightSpores => NSporeImpactVfx.Create(target, tint),
            HitFxKind.LightStars => Modulated(NStarryImpactVfx.Create(node.VfxSpawnPosition), tint),
            HitFxKind.MagicMissile => NSmallMagicMissileVfx.Create(node.VfxSpawnPosition, tint),
            HitFxKind.TrueSlice => NThinSliceVfx.Create(target, VfxColor.White),
            _ => null,
        };
        Add(target, vfx, fx.Kind == HitFxKind.FireBurst ? 1f : fx.Scale);
    });

    // ───── события ─────

    /// <summary>Надрыв: кровавые шипы под героем и лёгкая тряска (шкала ◆ и ♥ вспыхивают сами — NRequiemResourceBars).</summary>
    public static void Overstrain(Creature hero)
    {
        RequiemSfx.Play(SfxEvents.Overstrain);
        Safe("overstrain", () =>
        {
            Add(hero, NSpikeSplashVfx.Create(hero, VfxColor.Red));
            NGame.Instance?.ScreenShake(ShakeStrength.Weak, ShakeDuration.Short);
        });
    }

    /// <summary>Слом: осколки брони, тряска, враг «проседает» и выпрямляется.</summary>
    public static void Broken(Creature enemy)
    {
        RequiemSfx.Play(SfxEvents.Broken);
        Safe("broken", () =>
        {
            VfxCmd.PlayOnCreatureCenter(enemy, "vfx/vfx_rock_shatter");
            Add(enemy, NSpikeSplashVfx.Create(enemy, VfxColor.Gold));
            NGame.Instance?.ScreenShake(ShakeStrength.Medium, ShakeDuration.Short);
            if (enemy.GetCreatureNode()?.Visuals is not { } visuals) return;
            var (pose, tween) = Animate(visuals);
            visuals.Position = pose.Position;
            var rest = pose.Scale;
            tween.TweenProperty(visuals, "scale", new Vector2(rest.X * 1.04f, rest.Y * VfxRules.BrokenSag), VfxRules.BrokenSeconds * 0.3)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(visuals, "scale", rest, VfxRules.BrokenSeconds * 0.7).SetTrans(Tween.TransitionType.Elastic)
                .SetEase(Tween.EaseType.Out);
        });
    }

    /// <summary>Уворот: герой отскакивает от врагов и возвращается, над ним — «Уворот!».</summary>
    public static void Dodge(Creature hero)
    {
        RequiemSfx.Play(SfxEvents.Dodge);
        Safe("dodge", () =>
        {
            if (hero.GetCreatureNode() is not { } node) return;
            var (pose, tween) = Animate(node.Visuals);
            var visuals = node.Visuals;
            visuals.Scale = pose.Scale;
            var rest = pose.Position;
            var away = new Vector2(hero.IsEnemy ? VfxRules.DodgeShift : -VfxRules.DodgeShift, 0f);
            tween.TweenProperty(visuals, "position", rest + away, VfxRules.DodgeSeconds * 0.35).SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(visuals, "position", rest, VfxRules.DodgeSeconds * 0.65).SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);
            var text = NDamageBlockedVfx.Create(hero);
            if (text == null) return;
            DodgeText.Mark(text);
            Add(hero, text);
        });
    }

    /// <summary>Клич: волна крика от героя в цвете варианта (атака — рыжая, навык — золотая, сила — голубая).</summary>
    public static void Shout(Creature hero, ShoutFx variant)
    {
        RequiemSfx.Shout(variant);
        Safe("shout", () =>
        {
            if (hero.GetCreatureNode() is not { } node) return;
            Add(hero, Modulated(NScreamVfx.Create(node.VfxSpawnPosition), Tint(VfxRules.ShoutTint(variant))));
        });
    }

    /// <summary>Призыв: круг спор и дымка цвета призыва там, где встало существо.</summary>
    public static void Summon(Creature pet, SummonKind kind)
    {
        RequiemSfx.Play(SfxEvents.Summon);
        Safe("summon", () =>
        {
            var tint = Tint(VfxRules.SummonTint(kind));
            Add(pet, NSporeImpactVfx.Create(pet, tint));
            Add(pet, NGaseousImpactVfx.Create(pet, tint with { A = 0.7f }));
        });
    }

    /// <summary>Вход к супербоссу (Багровый портал): экран вспыхивает красным и гаснет, по краям — багровая дымка.</summary>
    public static void PortalEnter(NCombatRoom room)
    {
        RequiemSfx.Play(SfxEvents.PortalEnter);
        Safe("portal", () =>
        {
            var crimson = Tint(VfxRules.PortalTint);
            room.CombatVfxContainer.AddChildSafely(NSmokyVignetteVfx.Create(crimson with { A = 0.66f }, new Color(3f, 0f, 0f, 0.33f)));
            var layer = new CanvasLayer { Layer = 40, Name = "RequiemPortalEnter" };
            var veil = new ColorRect { Color = crimson with { A = VfxRules.PortalEnterAlpha }, MouseFilter = Control.MouseFilterEnum.Ignore };
            veil.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            layer.AddChild(veil);
            room.AddChildSafely(layer);
            var tween = veil.CreateTween();
            tween.TweenProperty(veil, "color:a", 0f, VfxRules.PortalEnterSeconds).SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.In);
            tween.TweenCallback(Callable.From(layer.QueueFree));
        });
    }

    /// <summary>Багровая пульсация за узлом супербосса на карте (ребёнок SpriteContainer — масштабируется при наведении вместе с ним).</summary>
    public static void PortalPulse(Node2D spriteContainer) => Safe("portal pulse", () =>
    {
        const string name = "RequiemPortalPulse";
        if (spriteContainer.GetNodeOrNull(name) != null) return;
        var crimson = Tint(VfxRules.PortalTint);
        var glow = new Sprite2D { Name = name, Texture = Glow(), Scale = Vector2.One * 1.7f, Modulate = crimson with { A = 0.35f } };
        spriteContainer.AddChild(glow);
        spriteContainer.MoveChild(glow, 0);
        var half = VfxRules.PortalPulseSeconds * 0.5;
        var pulse = glow.CreateTween().SetLoops();
        pulse.TweenProperty(glow, "modulate:a", 0.85f, half).SetTrans(Tween.TransitionType.Sine);
        pulse.Parallel().TweenProperty(glow, "scale", Vector2.One * 1.95f, half).SetTrans(Tween.TransitionType.Sine);
        pulse.TweenProperty(glow, "modulate:a", 0.35f, half).SetTrans(Tween.TransitionType.Sine);
        pulse.Parallel().TweenProperty(glow, "scale", Vector2.One * 1.7f, half).SetTrans(Tween.TransitionType.Sine);
    });

    /// <summary>
    /// Откат Камня Памяти: по экрану расходятся трещины, вспыхивает свет, свеча внизу гаснет, затем трещины «собираются»
    /// в центр. Возвращает задачу, которая ждёт, пока трещины видны (затем можно гасить экран — слой доиграет поверх).
    /// </summary>
    public static async Task Rewind(int seed)
    {
        RequiemSfx.Play(SfxEvents.Rewind);
        if (!Enabled || NGame.Instance == null) return;
        SceneTreeTimer timer;
        try
        {
            var size = NGame.Instance.GetViewportRect().Size;
            var layer = new CanvasLayer { Layer = 90, Name = "RequiemRewind" };
            var flash = new ColorRect { Color = new Color(0.85f, 0.92f, 1f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
            flash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            layer.AddChild(flash);
            var center = size * 0.5f;
            var cracks = new Node2D { Position = center };
            layer.AddChild(cracks);
            var lines = new List<Line2D>();
            foreach (var crack in VfxRules.RewindCracks(seed))
            {
                var line = new Line2D
                {
                    Width = 3f,
                    DefaultColor = new Color(0.9f, 0.96f, 1f, 0f),
                    JointMode = Line2D.LineJointMode.Sharp,
                    Points = crack.Select(p => new Vector2(p.X * center.X, p.Y * center.Y)).ToArray(),
                };
                cracks.AddChild(line);
                lines.Add(line);
            }
            var candle = new Sprite2D
            {
                Texture = Glow(),
                Position = new Vector2(center.X, size.Y * 0.86f),
                Scale = new Vector2(0.35f, 0.5f),
                Modulate = new Color(1f, 0.72f, 0.3f, 0.9f),
            };
            layer.AddChild(candle);
            NGame.Instance.AddChildSafely(layer);

            var total = VfxRules.RewindSeconds;
            var tween = layer.CreateTween().SetParallel();
            tween.TweenProperty(flash, "color:a", 0.35f, total * 0.12);
            tween.TweenProperty(flash, "color:a", 0f, total * 0.4).SetDelay(total * 0.12);
            for (var i = 0; i < lines.Count; i++)
                tween.TweenProperty(lines[i], "default_color:a", 1f, total * 0.1).SetDelay(total * 0.03 * i);
            tween.TweenProperty(candle, "scale", new Vector2(0.05f, 0.08f), total * 0.5).SetDelay(total * 0.15)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
            tween.TweenProperty(candle, "modulate:a", 0f, total * 0.5).SetDelay(total * 0.15);
            tween.TweenProperty(cracks, "scale", Vector2.Zero, total * 0.4).SetDelay(total * 0.6)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
            tween.TweenProperty(cracks, "modulate:a", 0f, total * 0.4).SetDelay(total * 0.6);
            tween.Chain().TweenCallback(Callable.From(layer.QueueFree));
            timer = NGame.Instance.GetTree().CreateTimer(total * 0.6);
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"[SlayTheRequiem] VFX rewind: {e.Message}");
            return;
        }
        await NGame.Instance.ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
    }

    private static GradientTexture2D? _glow;

    private static GradientTexture2D Glow()
    {
        if (_glow != null && GodotObject.IsInstanceValid(_glow)) return _glow;
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(1f, 1f, 1f, 1f));
        gradient.SetColor(1, new Color(1f, 1f, 1f, 0f));
        gradient.AddPoint(0.4f, new Color(1f, 1f, 1f, 0.5f));
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
}

/// <summary>Надпись «Уворот!» — ванильная табличка «Заблокировано» с другим текстом (подменяется в её _Ready).</summary>
public static class DodgeText
{
    private static readonly ConditionalWeakTable<NDamageBlockedVfx, object> Marked = new();

    public static void Mark(NDamageBlockedVfx vfx) => Marked.AddOrUpdate(vfx, true);

    public static bool IsMarked(NDamageBlockedVfx vfx) => Marked.TryGetValue(vfx, out _);
}
