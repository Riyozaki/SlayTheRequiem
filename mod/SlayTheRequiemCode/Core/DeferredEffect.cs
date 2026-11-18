namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>
/// One reserved effect. Detach before awaiting callbacks: nested effects cannot commit/cancel this one twice.
/// Cancellation releases only a reservation that has not started; a thrown callback is not a safe retry.
/// </summary>
public sealed class DeferredEffect(Func<Task> commit, Action cancel)
{
    public enum Phase { Reserved, Started, Completed, Cancelled, Faulted }
    public Phase State { get; private set; }

    public async Task Commit()
    {
        if (State != Phase.Reserved) return;
        State = Phase.Started;
        try { await commit(); State = Phase.Completed; }
        catch { State = Phase.Faulted; throw; }
    }

    public void Cancel()
    {
        if (State != Phase.Reserved) return;
        State = Phase.Cancelled;
        cancel();
    }
}
