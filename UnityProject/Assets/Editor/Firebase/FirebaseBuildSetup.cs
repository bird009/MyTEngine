using System;
using System.IO;
using System.Linq;
using GameServices;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
#endif

public sealed class FirebaseBuildSetup : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    [MenuItem("TEngine/Firebase/Enable SDK Integration")]
    public static void EnableSdkIntegration()
    {
        foreach (var name in new[] { "Firebase.App", "Firebase.Analytics", "Firebase.RemoteConfig" })
            if (!AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == name))
                throw new BuildFailedException("Missing SDK assembly: " + name);
        foreach (var group in new[] { BuildTargetGroup.Android, BuildTargetGroup.iOS })
        {
            var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            if (!symbols.Split(';').Contains("TENGINE_FIREBASE"))
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group,
                    string.IsNullOrEmpty(symbols) ? "TENGINE_FIREBASE" : symbols + ";TENGINE_FIREBASE");
        }
        if (!Version.TryParse(PlayerSettings.iOS.targetOSVersionString, out var iosVersion) ||
            iosVersion < new Version(15, 0))
            PlayerSettings.iOS.targetOSVersionString = "15.0";
        AssetDatabase.SaveAssets();
        Debug.Log("Firebase SDK integration enabled for Android/iOS. Runtime settings remain unchanged.");
    }

    [MenuItem("TEngine/Firebase/Resolve Android Dependencies")]
    public static void ResolveAndroidDependencies()
    {
        var resolver = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("GooglePlayServices.PlayServicesResolver"))
            .FirstOrDefault(type => type != null);
        var resolve = resolver?.GetMethod("ResolveSync", new[] { typeof(bool) });
        if (resolve == null || !(bool)resolve.Invoke(null, new object[] { true }))
            throw new BuildFailedException("Android dependency resolution failed. See the resolver log.");
        Debug.Log("Android dependencies resolved successfully.");
    }

    // Also compiles the player-only Firebase adapter, which Editor compilation excludes.
    public static void CompileAndroidScripts()
    {
        var result = UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(
            new UnityEditor.Build.Player.ScriptCompilationSettings
            {
                target = BuildTarget.Android,
                group = BuildTargetGroup.Android,
                options = UnityEditor.Build.Player.ScriptCompilationOptions.None
            }, "Library/CodexFirebaseSetup/AndroidScriptAssemblies");
        if (result.assemblies == null || result.assemblies.Count == 0)
            throw new BuildFailedException("Android player script compilation produced no assemblies.");
        Debug.Log("Android player script compilation succeeded: " + result.assemblies.Count + " assemblies.");
    }

    [MenuItem("TEngine/Firebase/Create or Select Settings")]
    public static void CreateSettings()
    {
        const string path = "Assets/Resources/FirebaseSettings.asset";
        var asset = AssetDatabase.LoadAssetAtPath<FirebaseSettings>(path);
        if (asset == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            asset = ScriptableObject.CreateInstance<FirebaseSettings>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
        }
        Selection.activeObject = asset;
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        var settings = Resources.Load<FirebaseSettings>("FirebaseSettings");
        if (settings == null || !settings.enableFirebase) return;
        var target = report.summary.platform;
        if (target != BuildTarget.Android && target != BuildTarget.iOS)
            throw new BuildFailedException("This Firebase integration supports Android/iOS players only.");
        var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildPipeline.GetBuildTargetGroup(target));
        if (Array.IndexOf(symbols.Split(';'), "TENGINE_FIREBASE") < 0)
            throw new BuildFailedException("Install Firebase Analytics + Remote Config, then enable TENGINE_FIREBASE.");
        if (string.IsNullOrWhiteSpace(settings.expectedProjectId))
            throw new BuildFailedException("Set FirebaseSettings.expectedProjectId to the selected Firebase project's ID.");
        var config = target == BuildTarget.Android ? "google-services.json" : "GoogleService-Info.plist";
        if (!File.Exists(Path.Combine(Application.dataPath, config)))
            throw new BuildFailedException("Missing Assets/" + config);
    }

#if UNITY_IOS
    [PostProcessBuild(100)]
    public static void DisableDefaultAnalytics(BuildTarget target, string output)
    {
        if (target != BuildTarget.iOS) return;
        var settings = Resources.Load<FirebaseSettings>("FirebaseSettings");
        if (settings == null || !settings.enableFirebase) return;
        var path = Path.Combine(output, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(path);
        plist.root.SetBoolean("FIREBASE_ANALYTICS_COLLECTION_ENABLED", false);
        plist.WriteToFile(path);
    }
#endif
}
