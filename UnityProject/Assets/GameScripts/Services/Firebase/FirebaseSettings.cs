using UnityEngine;

namespace GameServices
{
    [CreateAssetMenu(menuName = "TEngine/Firebase Settings")]
    public sealed class FirebaseSettings : ScriptableObject
    {
        public bool enableFirebase;
        public string environment = "development";
        [Tooltip("Must match the platform Firebase config file; prevents accidental cross-environment uploads.")]
        public string expectedProjectId = "";
        [Range(100, 10000)] public int startupBudgetMilliseconds = 3000;
        [Min(60)] public int minimumFetchIntervalSeconds = 43200;
        [Tooltip("Enable only if collection is already permitted at startup. Otherwise call FirebaseRuntime.SetAnalyticsConsent from your consent flow.")]
        public bool analyticsAllowedAtStartup;
        [Tooltip("Editor only. Never sent to Firebase; empty means Luban defaults.")]
        [TextArea(3, 10)] public string editorPolicyJson = "";
        public bool editorDisableInterstitial;

        public static FirebaseSettings Load()
        {
            var settings = Resources.Load<FirebaseSettings>("FirebaseSettings");
            return settings != null ? settings : CreateInstance<FirebaseSettings>();
        }
    }
}
