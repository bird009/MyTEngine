using System;
using System.Collections.Generic;
using GameServices;
using UnityEngine;

namespace GameLogic
{
    public sealed class AnalyticsModule : Singleton<AnalyticsModule>
    {
        private IRemoteConfigBackend _backend;
        private readonly HashSet<string> _exposed = new HashSet<string>();
        private bool _released;
        public string LastEvent { get; private set; } = "none";
        public int ExposureCount => _exposed.Count;
        public string SessionId { get; } = Guid.NewGuid().ToString("N");
        public void Initialize(IRemoteConfigBackend backend) { _backend = backend; }

        /// <summary>Custom gameplay event. Returns false when disabled, invalid, or the SDK rejected it; no pre-consent queue.</summary>
        public bool TrackEvent(string name, IReadOnlyDictionary<string, object> parameters = null)
        {
            if (!ValidName(name) || name.StartsWith("firebase_", StringComparison.Ordinal) ||
                name.StartsWith("google_", StringComparison.Ordinal) || name.StartsWith("ga_", StringComparison.Ordinal)) return false;
            if (parameters != null && parameters.Count > 24) return false; // Reserve one parameter for is_test.
            var copy = new Dictionary<string, object>();
            if (parameters != null)
            {
                foreach (var pair in parameters)
                {
                    if (!ValidName(pair.Key)) return false;
                    object value = pair.Value;
                    if (value is int i) value = (long)i;
                    else if (value is float f) value = (double)f;
                    else if (value is bool b) value = b ? 1L : 0L;
                    if (!(value is string) && !(value is long) && !(value is double)) return false;
                    if (value is double number && (double.IsNaN(number) || double.IsInfinity(number))) return false;
                    copy.Add(pair.Key, value);
                }
            }
            return Send(name, copy);
        }

        private static bool ValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 40 || !IsLetter(name[0])) return false;
            foreach (char c in name) if (!IsLetter(c) && !(c >= '0' && c <= '9') && c != '_') return false;
            return true;
        }
        private static bool IsLetter(char c) => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z';

        public void TrackOpportunity(AdPolicy policy, string entrance)
        {
            if (policy.IsExperiment && !_exposed.Contains(policy.Experiment))
            {
                if (Send(RemoteConfigKeys.Exposure, Parameters(policy, entrance))) _exposed.Add(policy.Experiment);
            }
            Send("interstitial_opportunity", Parameters(policy, entrance));
        }
        public void TrackResult(AdPolicy policy, string entrance, string result)
        {
            var parameters = Parameters(policy, entrance);
            parameters["result"] = result;
            Send("interstitial_result", parameters);
        }
        public void TrackConfigInvalid(string reason) => Send("config_invalid",
            new Dictionary<string, object> { { "reason", reason } });

        private Dictionary<string, object> Parameters(AdPolicy policy, string entrance) => new Dictionary<string, object>
        {
            { "policy_revision", policy.Revision }, { "experiment_key", policy.Experiment },
            { "variant", policy.Variant }, { "interval_sec", policy.MinIntervalSeconds },
            { "config_source", policy.Source }, { "config_release", policy.Release },
            { "entrance", entrance }, { "business_session", SessionId }
        };
        private bool Send(string name, Dictionary<string, object> parameters)
        {
            if (_released || _backend == null || !_backend.IsReady ||
                (!_backend.IsSimulation && !FirebaseRuntime.AnalyticsAllowed)) return false;
            try
            {
                foreach (var key in new List<string>(parameters.Keys))
                    if (parameters[key] is string text && text.Length > 100) parameters[key] = text.Substring(0, 100);
                parameters["is_test"] = _backend.IsSimulation ? 1L : 0L;
                _backend.LogEvent(name, parameters);
                LastEvent = name;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Analytics event failed: " + exception.Message);
                return false; // Analytics failure must never block an ad/gameplay operation.
            }
        }
        protected override void OnRelease() { _released = true; _backend = null; _exposed.Clear(); }
    }
}
