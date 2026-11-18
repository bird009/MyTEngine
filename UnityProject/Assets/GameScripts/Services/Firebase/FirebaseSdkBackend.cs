#if TENGINE_FIREBASE && (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Analytics;
using Firebase.RemoteConfig;

namespace GameServices
{
    internal sealed class FirebaseSdkBackend : IRemoteConfigBackend
    {
        private readonly FirebaseSettings _settings;
        private FirebaseApp _app;
        private FirebaseRemoteConfig _config;
        private Task _initialization;
        private Task _fetch;
        public string Name => "firebase";
        public bool IsReady { get; private set; }
        public bool IsSimulation => false;

        public FirebaseSdkBackend(FirebaseSettings settings) { _settings = settings; }
        public Task InitializeAsync()
        {
            if (_initialization == null || _initialization.IsFaulted) _initialization = InitializeCoreAsync();
            return _initialization;
        }

        private async Task InitializeCoreAsync()
        {
            var status = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (status != DependencyStatus.Available)
                throw new InvalidOperationException("Firebase dependencies: " + status);
            _app = FirebaseApp.DefaultInstance;
            FirebaseAnalytics.SetAnalyticsCollectionEnabled(false);
            if (string.IsNullOrWhiteSpace(_settings.expectedProjectId) ||
                _app.Options.ProjectId != _settings.expectedProjectId)
                throw new InvalidOperationException("Firebase project ID does not match FirebaseSettings.");

            _config = FirebaseRemoteConfig.DefaultInstance;
            await _config.EnsureInitializedAsync();
            await _config.SetDefaultsAsync(new Dictionary<string, object>
            {
                { RemoteConfigKeys.InterstitialPolicy, "" },
                { RemoteConfigKeys.InterstitialDisabled, false },
                { RemoteConfigKeys.Release, "builtin" }
            });
            await _config.SetConfigSettingsAsync(new ConfigSettings
            {
                FetchTimeoutInMilliseconds = 10000,
                MinimumFetchIntervalInMilliseconds = (ulong)Math.Max(60, _settings.minimumFetchIntervalSeconds) * 1000
            });
            IsReady = true;
            // Do not enable collection here: initialization can complete after the business startup deadline.
        }

        public Task FetchAsync()
        {
            if (_fetch == null || _fetch.IsCompleted) _fetch = FetchCoreAsync();
            return _fetch;
        }
        private async Task FetchCoreAsync()
        {
            await _config.FetchAsync(TimeSpan.FromSeconds(Math.Max(60, _settings.minimumFetchIntervalSeconds)));
            if (_config.Info.LastFetchStatus != LastFetchStatus.Success)
                throw new InvalidOperationException("Remote Config fetch: " + _config.Info.LastFetchStatus);
        }
        public async Task ActivateAsync() { await _config.ActivateAsync(); }
        public RemoteConfigValues ReadActivated()
        {
            var policy = _config.GetValue(RemoteConfigKeys.InterstitialPolicy);
            return new RemoteConfigValues(policy.StringValue, policy.Source == ValueSource.RemoteValue,
                _config.GetValue(RemoteConfigKeys.InterstitialDisabled).BooleanValue,
                _config.GetValue(RemoteConfigKeys.Release).StringValue);
        }
        public void SetAnalyticsCollection(bool enabled) => FirebaseAnalytics.SetAnalyticsCollectionEnabled(enabled);
        public void LogEvent(string name, IReadOnlyDictionary<string, object> parameters)
        {
            var values = new List<Parameter>(parameters.Count);
            foreach (var pair in parameters)
            {
                if (pair.Value is long number) values.Add(new Parameter(pair.Key, number));
                else if (pair.Value is double real) values.Add(new Parameter(pair.Key, real));
                else values.Add(new Parameter(pair.Key, pair.Value?.ToString() ?? ""));
            }
            FirebaseAnalytics.LogEvent(name, values.ToArray());
        }
    }
}
#endif
