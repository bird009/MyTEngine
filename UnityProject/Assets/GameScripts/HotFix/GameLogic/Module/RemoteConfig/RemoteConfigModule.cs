using System;
using System.Threading;
using System.Threading.Tasks;
using GameServices;

namespace GameLogic
{
    public sealed class RemoteConfigModule : Singleton<RemoteConfigModule>
    {
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly ConfigStartupSession _startup = new ConfigStartupSession();
        private CancellationToken _token;
        private Task _initialization;
        public AdPolicy Interstitial { get; private set; } = new AdPolicy(false, 0, 60);
        public string Status => _startup.Status;
        public string Error { get; private set; } = "";
        public bool IsApplied { get; private set; }

        protected override void OnInit() { _token = _lifetime.Token; }

        public Task InitializeAsync(IRemoteConfigBackend backend, FirebaseSettings settings, AnalyticsModule analytics,
            Func<AdPolicy> loadBaseline = null)
            => _initialization ??= InitializeCoreAsync(backend, settings, analytics, loadBaseline);

        private async Task InitializeCoreAsync(IRemoteConfigBackend backend, FirebaseSettings settings,
            AnalyticsModule analytics, Func<AdPolicy> loadBaseline)
        {
            try { Interstitial = loadBaseline?.Invoke() ?? Interstitial; }
            catch (Exception exception) { Error = "Local ad policy: " + exception.Message; }

            var values = await _startup.RunAsync(backend, Math.Max(100, settings.startupBudgetMilliseconds),
                () => FirebaseRuntime.AnalyticsAllowed, _token, FirebaseRuntime.StartupGate);
            _token.ThrowIfCancellationRequested();
            Interstitial = AdPolicyResolver.Resolve(Interstitial, values,
                backend.IsSimulation || FirebaseRuntime.AnalyticsAllowed,
                backend.IsSimulation ? "simulation" : "sdk_activated", out var validationError);
            Error = string.Join("; ", new[] { Error, _startup.Error, validationError }).Trim(';', ' ');
            IsApplied = true;
            if (validationError.Length > 0) analytics.TrackConfigInvalid(validationError);
        }
        protected override void OnRelease()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            IsApplied = false;
        }
    }
}
