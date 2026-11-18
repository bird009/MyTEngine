using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace GameServices
{
    /// <summary>Process owner. Business restart must not reset Firebase installation/Analytics identity.</summary>
    public static class FirebaseRuntime
    {
        private static IRemoteConfigBackend _backend;
        private static bool? _consent;
        public static readonly SemaphoreSlim StartupGate = new SemaphoreSlim(1, 1);
        public static bool AnalyticsAllowed => _consent == true;

        public static IRemoteConfigBackend GetBackend(FirebaseSettings settings)
        {
            if (!_consent.HasValue) _consent = settings.analyticsAllowedAtStartup;
            if (_backend != null) return _backend;
#if UNITY_EDITOR
            _backend = new LocalConfigBackend(settings.editorPolicyJson, settings.editorDisableInterstitial);
#elif TENGINE_FIREBASE && (UNITY_ANDROID || UNITY_IOS)
            _backend = settings.enableFirebase
                ? (IRemoteConfigBackend)new FirebaseSdkBackend(settings)
                : new LocalConfigBackend();
#else
            _backend = new LocalConfigBackend();
            if (settings.enableFirebase)
                Debug.LogWarning("Firebase requested but SDK integration is unavailable. Check TENGINE_FIREBASE and target platform.");
#endif
            return _backend;
        }

        /// <summary>Use the actual consent decision; ATT status alone is not a general Analytics consent decision.</summary>
        public static void SetAnalyticsConsent(bool allowed)
        {
            _consent = allowed;
            if (_backend != null && _backend.IsReady) _backend.SetAnalyticsCollection(allowed);
        }

        // Covers Editor play sessions with domain reload disabled. Native process state is not reset in players.
#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEditor() { _backend = null; _consent = null; }
#endif
    }

    public sealed class LocalConfigBackend : IRemoteConfigBackend
    {
        private readonly RemoteConfigValues _values;
        public string Name => "local-simulation";
        public bool IsReady { get; private set; }
        public bool IsSimulation => true;
        public LocalConfigBackend(string policyJson = "", bool disabled = false)
        {
            _values = new RemoteConfigValues(policyJson, !string.IsNullOrWhiteSpace(policyJson), disabled, "local-test");
        }
        public Task InitializeAsync() { IsReady = true; return Task.CompletedTask; }
        public Task FetchAsync() => Task.CompletedTask;
        public Task ActivateAsync() => Task.CompletedTask;
        public RemoteConfigValues ReadActivated() => _values;
        public void SetAnalyticsCollection(bool enabled) { }
        public void LogEvent(string name, IReadOnlyDictionary<string, object> parameters)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[Analytics/local-only] " + name);
#endif
        }
    }
}
