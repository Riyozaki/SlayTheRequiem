using System.Runtime.CompilerServices;
using Godot;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>Native Godot focus plus raw gamepad fallback. Only use in the owning active screen.
/// Read-only focusable rows make journals/disabled offers reachable too; accept only invokes enabled buttons.</summary>
public static class RequiemFocus
{
    private sealed class Stick { internal ulong Last; }
    private static readonly ConditionalWeakTable<Control, Stick> Sticks = new();
    private static bool Eligible(Control control) => GodotObject.IsInstanceValid(control) && !control.IsQueuedForDeletion()
        && control.IsVisibleInTree() && control.FocusMode != Control.FocusModeEnum.None && control is not Button { Disabled: true };

    public static void Wire(IReadOnlyList<Control> controls)
    {
        var enabled = controls.Where(Eligible).ToList();
        for (var i = 0; i < enabled.Count; i++)
        {
            var previous = enabled[(i + enabled.Count - 1) % enabled.Count].GetPath();
            var next = enabled[(i + 1) % enabled.Count].GetPath();
            enabled[i].FocusNeighborTop = previous; enabled[i].FocusPrevious = previous;
            enabled[i].FocusNeighborBottom = next; enabled[i].FocusNext = next;
            enabled[i].FocusNeighborLeft = previous; enabled[i].FocusNeighborRight = next;
        }
    }

    /// <summary>Long descriptions can exceed one viewport. Page keys/right stick scroll without moving focus.</summary>
    public static bool Scroll(Control screen, InputEvent input, ScrollContainer scroll)
    {
        var amount = 0f;
        if (input is InputEventKey { Pressed: true } key)
            amount = key.Keycode == Key.Pageup ? -.8f * scroll.Size.Y : key.Keycode == Key.Pagedown ? .8f * scroll.Size.Y : 0;
        else if (input is InputEventJoypadMotion { Axis: JoyAxis.RightY } motion && Math.Abs(motion.AxisValue) >= .5f)
        {
            var stick = Sticks.GetValue(scroll, _ => new()); var now = Time.GetTicksMsec();
            if (now - stick.Last >= 70) { stick.Last = now; amount = Math.Sign(motion.AxisValue) * 60; }
        }
        if (amount == 0) return false;
        scroll.ScrollVertical += (int)amount;
        screen.GetViewport().SetInputAsHandled(); return true;
    }

    public static void Handle(Control screen, InputEvent input, IEnumerable<Control> controls, Action? close = null)
    {
        var direction = 0; var accept = false; var cancel = false;
        if (input is InputEventKey { Pressed: true, Echo: false } key)
        {
            direction = key.Keycode is Key.Up or Key.Left || key.Keycode == Key.Tab && key.ShiftPressed ? -1
                : key.Keycode is Key.Down or Key.Right or Key.Tab ? 1 : 0;
            accept = key.Keycode is Key.Enter or Key.KpEnter or Key.Space;
            cancel = key.Keycode == Key.Escape;
        }
        else if (input is InputEventJoypadButton { Pressed: true } pad)
        {
            direction = pad.ButtonIndex is JoyButton.DpadUp or JoyButton.DpadLeft ? -1
                : pad.ButtonIndex is JoyButton.DpadDown or JoyButton.DpadRight ? 1 : 0;
            accept = pad.ButtonIndex == JoyButton.A; cancel = pad.ButtonIndex == JoyButton.B;
        }
        else if (input is InputEventJoypadMotion motion && motion.Axis is JoyAxis.LeftX or JoyAxis.LeftY && Math.Abs(motion.AxisValue) >= 0.65f)
        {
            var stick = Sticks.GetValue(screen, _ => new()); var now = Time.GetTicksMsec();
            if (now - stick.Last < 180) return;
            stick.Last = now; direction = Math.Sign(motion.AxisValue);
        }
        if (direction == 0 && !accept && !cancel) return;
        // Escape/B in a mandatory/busy decision is consumed, never a hidden skip or the pause screen behind it.
        if (cancel) close?.Invoke();
        else
        {
            var focus = controls.Where(Eligible).ToList();
            if (focus.Count > 0)
            {
                var current = focus.FindIndex(b => b.HasFocus());
                if (direction != 0) focus[current < 0 ? 0 : (current + direction + focus.Count) % focus.Count].GrabFocus();
                else if (focus[Math.Max(0, current)] is Button button) button.EmitSignal(Button.SignalName.Pressed);
            }
        }
        screen.GetViewport().SetInputAsHandled();
    }
}
