using System;
using System.Threading;
using System.Threading.Tasks;

namespace CleanScan.Services;

public sealed class Debouncer
{
    private readonly TimeSpan _delay;
    private CancellationTokenSource? _cts;

    public Debouncer(TimeSpan delay) => _delay = delay;

    public async Task DebounceAsync(Func<Task> action)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

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

    public void Cancel() => _cts?.Cancel();
}
