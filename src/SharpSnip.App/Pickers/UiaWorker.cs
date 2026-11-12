using System.Collections.Concurrent;
using System.Windows.Automation;

namespace SharpSnip;

/// <summary>Dedicated STA thread for UI Automation (§11): UIA calls are slow and can block on hung apps.</summary>
internal sealed class UiaWorker : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;

    public UiaWorker()
    {
        _thread = new Thread(Loop) { IsBackground = true, Name = "SharpSnip.UIA" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ = RunAsync(() => AutomationElement.RootElement); // pre-warm (§1.3)
    }

    private void Loop()
    {
        foreach (var work in _queue.GetConsumingEnumerable())
        {
            try { work(); }
            catch (Exception ex) { Log.Warn("UIA work item failed: " + ex.Message); }
        }
    }

    public Task<T> RunAsync<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            _queue.Add(() =>
            {
                try { tcs.SetResult(func()); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
        }
        catch (InvalidOperationException) { tcs.SetCanceled(); }
        return tcs.Task;
    }

    public void Dispose() => _queue.CompleteAdding();
}
