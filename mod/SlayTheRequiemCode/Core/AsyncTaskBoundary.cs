namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>
/// A completion notification may fire inside a callback BEFORE its owner registers the returned Task.
/// Yield past registration, await every observed task without clearing the owner's bookkeeping, and recheck for
/// tasks appended by cleanup continuations. Failure is propagated, never treated as a confirmed save boundary.
/// </summary>
public static class AsyncTaskBoundary
{
    public static async Task<bool> Settle(Func<IReadOnlyList<Task>> snapshot, Func<bool> stillCurrent)
    {
        await Task.Yield();
        for (var pass = 0; pass < 128 && stillCurrent(); pass++)
        {
            var before = new HashSet<Task>(snapshot(), ReferenceEqualityComparer.Instance);
            if (before.Count > 4096) throw new InvalidOperationException("Event task boundary exceeds its safety limit");
            await Task.WhenAll(before); // fault/cancellation must prevent checkpoint capture
            await Task.Yield();
            if (!stillCurrent()) return false;
            var after = snapshot();
            if (after.All(t => t.IsCompletedSuccessfully) && before.SetEquals(after)) return true;
        }
        return false; // task set never stabilized; do not manufacture a confirmed start
    }
}
