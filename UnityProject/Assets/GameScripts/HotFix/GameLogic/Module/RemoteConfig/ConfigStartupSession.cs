using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using GameServices;

namespace GameLogic
{
    /// <summary>One startup transaction. Network waits are bounded; local activation is awaited to completion.
    /// A process-wide gate prevents an old activation from racing a restarted business session.</summary>
    public sealed class ConfigStartupSession
    {
        public string Status { get; private set; } = "not_started";
        public string Error { get; private set; } = "";
        public async Task<RemoteConfigValues> RunAsync(IRemoteConfigBackend backend, int budgetMilliseconds,
            Func<bool> analyticsAllowed, CancellationToken cancellation, SemaphoreSlim gate)
        {
            var clock = Stopwatch.StartNew();
            bool acquired = false;
            bool initialized = false;
            int Remaining() => Math.Max(0, budgetMilliseconds - (int)clock.ElapsedMilliseconds);
            try
            {
                acquired = await gate.WaitAsync(Remaining(), cancellation);
                if (!acquired) { Status = "startup_busy"; return new RemoteConfigValues(); }
                Status = "initializing";
                if (!await WithinBudget(backend.InitializeAsync(), Remaining(), cancellation))
                { Status = "initialization_timeout"; return new RemoteConfigValues(); }
                cancellation.ThrowIfCancellationRequested();
                initialized = true;
                backend.SetAnalyticsCollection(analyticsAllowed());
                // Finish any prior launch's downloaded candidate before this launch starts emitting events.
                await backend.ActivateAsync();
                cancellation.ThrowIfCancellationRequested();
                Status = "fetching";
                if (Remaining() == 0 || !await WithinBudget(backend.FetchAsync(), Remaining(), cancellation))
                { Status = "cached_timeout"; return backend.ReadActivated(); }
                cancellation.ThrowIfCancellationRequested();
                // Never place activation in an abandoned fetch continuation.
                await backend.ActivateAsync();
                cancellation.ThrowIfCancellationRequested();
                Status = backend.IsSimulation ? "simulation" : "fetch_completed";
                return backend.ReadActivated();
            }
            catch (OperationCanceledException) { Status = "released"; throw; }
            catch (Exception exception)
            {
                cancellation.ThrowIfCancellationRequested();
                Error = exception.Message;
                Status = initialized ? "cached_error" : "initialization_error";
                if (initialized)
                {
                    try { return backend.ReadActivated(); }
                    catch { /* Defaults remain usable even if the native cache cannot be read. */ }
                }
                return new RemoteConfigValues();
            }
            finally { if (acquired) gate.Release(); }
        }

        private static async Task<bool> WithinBudget(Task work, int milliseconds, CancellationToken cancellation)
        {
            using (var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                var delay = Task.Delay(milliseconds, delayCancellation.Token);
                var winner = await Task.WhenAny(work, delay);
                delayCancellation.Cancel();
                if (winner != work || cancellation.IsCancellationRequested)
                {
                    // Firebase operations cannot always be canceled. Observe faults without touching business state.
                    Observe(work);
                    cancellation.ThrowIfCancellationRequested();
                    return false;
                }
                await work;
                return true;
            }
        }
        private static async void Observe(Task task)
        {
            try { await task; } catch { /* The owning startup already reported timeout/cancellation. */ }
        }
    }
}
