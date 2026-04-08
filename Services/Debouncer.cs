using System;
using System.Threading;
using System.Threading.Tasks;

namespace AvyScanLab.Services;

public sealed class Debouncer
{
    private readonly TimeSpan _delay;
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;

    public Debouncer(TimeSpan delay) => _delay = delay;

    public async Task DebounceAsync(Func<Task> action)
    {
        CancellationToken token;
        lock (_lock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            token = _cts.Token;
        }

        try
        {
            await Task.Delay(_delay, token);
            if (!token.IsCancellationRequested)
            {
                await action();
            }
        }
        catch (TaskCanceledException)
        {
            // noop
        }
    }

    public void Cancel()
    {
        lock (_lock)
        {
            _cts?.Cancel();
        }
    }
}
