using System.Collections.Generic;
using System.Threading.Tasks;

namespace GameServices
{
    public static class RemoteConfigKeys
    {
        public const string InterstitialPolicy = "ads_interstitial_policy_v1";
        public const string InterstitialDisabled = "ads_interstitial_disabled";
        public const string Release = "config_release";
        public const string Experiment = "inter_gap_001";
        public const string Exposure = "inter_gap_001_exposed";
    }

    /// <summary>Only activated values. Remote source does not imply a network request occurred this launch.</summary>
    public sealed class RemoteConfigValues
    {
        public string PolicyJson { get; }
        public bool PolicyIsRemote { get; }
        public bool InterstitialDisabled { get; }
        public string Release { get; }

        public RemoteConfigValues(string policyJson = "", bool policyIsRemote = false,
            bool interstitialDisabled = false, string release = "builtin")
        {
            PolicyJson = policyJson ?? "";
            PolicyIsRemote = policyIsRemote;
            InterstitialDisabled = interstitialDisabled;
            Release = release ?? "";
        }
    }

    /// <summary>Call from Unity's main thread; implementations never activate inside FetchAsync.</summary>
    public interface IRemoteConfigBackend
    {
        string Name { get; }
        bool IsReady { get; }
        bool IsSimulation { get; }
        Task InitializeAsync();
        Task FetchAsync();
        Task ActivateAsync();
        RemoteConfigValues ReadActivated();
        void SetAnalyticsCollection(bool enabled);
        void LogEvent(string name, IReadOnlyDictionary<string, object> parameters);
    }
}
