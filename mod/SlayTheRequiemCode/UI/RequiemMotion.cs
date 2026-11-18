using System.Runtime.CompilerServices;
using Godot;
using SlayTheRequiem.SlayTheRequiemCode.Config;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>
/// Local presentation preference, deliberately absent from RunSettings/save/network schemas.
/// Watchers are connected only while their owner is in the tree; closing a window cannot leave a static
/// delegate rooting its controls. Settings changes stop ongoing decorative loops as well as future ones.
/// Does NOT disable native game animations or the persistent phase/intent/card-state presentation.
/// </summary>
public static class RequiemMotion
{
    public static bool Enabled => !RequiemConfig.ReducedMotion;
    private static event Action? Changed;

    internal static void SettingsChanged()
    {
        if (Changed == null) return;
        foreach (Action update in Changed.GetInvocationList())
        {
            try { update(); }
            catch (Exception e) { MainFile.Logger.Warn($"[Presentation] settings refresh: {e.Message}"); }
        }
    }

    public static void Watch(Node owner, Action update)
    {
        var listening = false;
        void Enter()
        {
            if (listening) return;
            listening = true;
            Changed += update;
            update();
        }
        void Exit()
        {
            if (!listening) return;
            listening = false;
            Changed -= update;
        }
        owner.TreeEntered += Enter;
        owner.TreeExiting += Exit;
        if (owner.IsInsideTree()) Enter();
    }

    /// <summary>One owner-scoped decoration, stopped/restored immediately when the local preference changes.</summary>
    public static void Decorate(Node owner, Action rest, Func<Tween[]> animate, bool effectsOnly = false)
    {
        (bool Motion, bool Effects)? previous = null;
        Tween[] tweens = [];
        void Stop()
        {
            foreach (var tween in tweens) Kill(tween);
            tweens = [];
        }
        Watch(owner, () =>
        {
            var state = (Enabled, !effectsOnly || RequiemConfig.CombatVfx);
            if (previous == state) return;
            previous = state;
            Stop(); rest();
            if (state.Item1 && state.Item2) tweens = animate();
        });
        owner.TreeExiting += () => { Stop(); previous = null; };
    }

    private sealed class PositionTransition
    {
        internal Vector2 Target;
        internal bool HasTarget;
        internal Tween? Tween;
        internal void Finish(Control owner)
        {
            Kill(Tween); Tween = null;
            if (HasTarget && GodotObject.IsInstanceValid(owner)) owner.Position = Target;
        }
    }
    private static readonly ConditionalWeakTable<Control, PositionTransition> Positions = new();

    /// <summary>One cancellable layout transition per summon; repeated Arrange cannot race several tweens.
    /// Reduced motion/re-entry commits its latest target, never a partly displaced layout.</summary>
    public static void MoveTo(Control owner, Vector2 target, double seconds)
    {
        var state = Positions.GetValue(owner, node =>
        {
            var next = new PositionTransition();
            Watch(node, () => { if (!Enabled) next.Finish(node); });
            node.TreeExiting += () => next.Finish(node);
            return next;
        });
        Kill(state.Tween); state.Target = target; state.HasTarget = true;
        if (!Enabled || !owner.IsInsideTree() || owner.Position.DistanceTo(target) <= 1f) { state.Finish(owner); return; }
        var tween = state.Tween = owner.CreateTween();
        tween.TweenProperty(owner, "position", target, seconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    internal static void Kill(Tween? tween)
    {
        if (tween != null && tween.IsValid()) tween.Kill();
    }
}
