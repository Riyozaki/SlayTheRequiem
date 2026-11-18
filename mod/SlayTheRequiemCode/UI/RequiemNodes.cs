using Godot;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>Presentation-only node ownership. QueueFree is deferred: detach NOW before reusing a child name,
/// otherwise Godot renames the replacement and future GetNode calls can leave unowned overlays behind.
/// Removing from the tree also releases owner-scoped subscriptions immediately. Never touches model state.</summary>
public static class RequiemNodes
{
    public static void Retire(Node? child)
    {
        if (child == null || !GodotObject.IsInstanceValid(child)) return;
        child.GetParent()?.RemoveChild(child);
        if (!child.IsQueuedForDeletion()) child.QueueFree();
    }

    public static T? Find<T>(Node parent, string path) where T : Node
    {
        if (!GodotObject.IsInstanceValid(parent)) return null;
        var child = parent.GetNodeOrNull<T>(path);
        if (child == null || !child.IsQueuedForDeletion()) return child;
        Retire(child); // do not resurrect/update a node that will disappear at the end of this frame
        return null;
    }

    public static void Clear(Node parent)
    {
        foreach (var child in parent.GetChildren()) Retire(child);
    }
}
