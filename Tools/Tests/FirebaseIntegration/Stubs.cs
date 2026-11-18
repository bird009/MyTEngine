using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UnityEngine
{
    public class ScriptableObject { public static T CreateInstance<T>() where T : new() => new T(); }
    public class CreateAssetMenuAttribute : Attribute { public string menuName; }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    public class MinAttribute : Attribute { public MinAttribute(float v) { } }
    public class TextAreaAttribute : Attribute { public TextAreaAttribute(int a, int b) { } }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { } }
    public static class Resources { public static T Load<T>(string path) where T : class => null; }
    public static class Debug { public static void Log(object text) { } public static void LogWarning(object text) { } public static void LogError(object text) { } }
    public static class GUILayout { public static void Label(string text) { } public static bool Button(string text) => false; }
    public static class Time { public static float realtimeSinceStartup; }
    public static class PlayerPrefs
    {
        private static readonly Dictionary<string, string> Values = new Dictionary<string, string>();
        public static string GetString(string key, string fallback = "") => Values.TryGetValue(key, out var value) ? value : fallback;
        public static void SetString(string key, string value) => Values[key] = value;
        public static int GetInt(string key, int fallback = 0) => int.TryParse(GetString(key), out var value) ? value : fallback;
        public static void SetInt(string key, int value) => SetString(key, value.ToString());
        public static void DeleteKey(string key) => Values.Remove(key);
        public static void DeleteAll() => Values.Clear();
        public static void Save() { }
    }
}
namespace TEngine
{
    public static class Log { public static void Info(object text) { } public static void Debug(object text) { } public static void Warning(object text) { } }
    public interface IDebuggerWindow
    {
        void Initialize(params object[] args); void Shutdown(); void OnEnter(); void OnLeave();
        void OnUpdate(float elapsed, float real); void OnDraw();
    }
}
namespace GameConfig.ad
{
    public class Ad { public bool Enabled = true; public int DailyLimit; public float MinIntervalSec = 60; }
}
namespace GameLogic
{
    public interface ISingleton { void Release(); }
    public static class SingletonSystem
    {
        public static bool IsReleasing;
        public static void Retain(ISingleton value) { }
        public static void Release(ISingleton value) { }
    }
    public sealed class AdConfigMgr
    {
        public static readonly AdConfigMgr Instance = new AdConfigMgr();
        public GameConfig.ad.Ad Row = new GameConfig.ad.Ad();
        public GameConfig.ad.Ad GetAd(int id) => Row;
    }
#if TEST_AD_INTEGRATION
    public interface IAdEvent
    {
        void OnAdShown(AdType type, AdsEntrance point);
        void OnAdRewardCompleted(AdsEntrance point, int amount);
        void OnAdClosed(AdType type, AdsEntrance point);
    }
    public sealed class Events : IAdEvent
    {
        public void OnAdShown(AdType t, AdsEntrance p) { }
        public void OnAdRewardCompleted(AdsEntrance p, int a) { }
        public void OnAdClosed(AdType t, AdsEntrance p) { }
    }
    public static class GameEvent { public static T Get<T>() => (T)(object)new Events(); }
    public class EditorAdService : IAdService
    {
        public static bool Ready = true;
        public static bool Defer;
        public static bool ThrowOnShow;
        public static Action<bool> Pending;
        public void Init() { }
        public bool IsAdReady(AdType type) => Ready;
        public void ShowRewardAd(Action<bool, int> callback) => callback(true, 1);
        public void ShowInterAd(Action<bool> callback)
        {
            if (ThrowOnShow) throw new InvalidOperationException("SDK failure");
            if (Defer) Pending = callback; else callback(true);
        }
    }
#endif
}
