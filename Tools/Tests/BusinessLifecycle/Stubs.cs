// Only the Unity/platform and unrelated business dependencies are replaced.
// Lifecycle, facade, module manager and update driver are linked production sources.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace UnityEngine
{
    public class Object
    {
        public static readonly List<GameObject> PendingDestroy = new List<GameObject>();
        public static T FindObjectOfType<T>() where T : Object => null;
        public static void DontDestroyOnLoad(Object obj) { }
        public static void Destroy(Object obj)
        {
            if (obj is GameObject go && !PendingDestroy.Contains(go)) PendingDestroy.Add(go);
        }
        public static void FlushDestroy()
        {
            while (PendingDestroy.Count > 0)
            {
                var go = PendingDestroy[0];
                PendingDestroy.RemoveAt(0);
                go.Invoke("OnDestroy");
                GameObject.All.Remove(go);
            }
        }
    }
    public class GameObject : Object
    {
        public static readonly List<GameObject> All = new List<GameObject>();
        private readonly List<MonoBehaviour> _components = new List<MonoBehaviour>();
        public string name;
        public Transform transform = new Transform();
        public GameObject(string name) { this.name = name; All.Add(this); }
        public void SetActive(bool active) { }
        public static GameObject Find(string name) => All.Find(go => go.name == name.TrimStart('/'));
        public T GetComponent<T>() where T : MonoBehaviour => _components.Find(c => c is T) as T;
        public T AddComponent<T>() where T : MonoBehaviour
        {
            var component = (T)Activator.CreateInstance(typeof(T), true);
            component.gameObject = this;
            _components.Add(component);
            return component;
        }
        public void Invoke(string name)
        {
            foreach (var component in _components.ToArray())
                component.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(component, null);
        }
    }
    public class MonoBehaviour : Object
    {
        public GameObject gameObject;
        public Coroutine StartCoroutine(string name) => new Coroutine();
        public Coroutine StartCoroutine(string name, object value) => new Coroutine();
        public Coroutine StartCoroutine(IEnumerator routine) => new Coroutine();
        public void StopCoroutine(string name) { }
        public void StopCoroutine(IEnumerator routine) { }
        public void StopCoroutine(Coroutine routine) { }
        public void StopAllCoroutines() { }
    }
    public class Transform { public Vector3 position; }
    public struct Vector3 { public static Vector3 zero => default; }
    public class Coroutine { }
    public class Camera : MonoBehaviour { public static Camera main; }
    public static class Application { public static bool isPlaying = true; }
    public static class Resources { public static void UnloadUnusedAssets() { } }
    public static class Debug
    {
        public static int ExceptionCount;
        public static void LogError(object message) { }
        public static void LogException(Exception exception) { ExceptionCount++; }
    }
}
namespace UnityEngine.SceneManagement
{
    public static class SceneManager { public static void LoadScene(int index) { } }
}
namespace UnityEngine.Internal
{
    public sealed class DefaultValueAttribute : Attribute { public DefaultValueAttribute(string value) { } }
}
// Deterministic stand-in for the pre-fix driver's deferred subscriptions.
namespace Cysharp.Threading.Tasks
{
    public enum PlayerLoopTiming { LastEarlyUpdate }
    public static class UniTask
    {
        internal static readonly Queue<Action> Pending = new Queue<Action>();
        public static YieldAwaitable Yield(PlayerLoopTiming timing = default) => new YieldAwaitable();
        public static void Flush() { while (Pending.Count > 0) Pending.Dequeue()(); }
    }
    public struct YieldAwaitable : INotifyCompletion
    {
        public YieldAwaitable GetAwaiter() => this;
        public bool IsCompleted => false;
        public void GetResult() { }
        public void OnCompleted(Action continuation) => UniTask.Pending.Enqueue(continuation);
    }
    [AsyncMethodBuilder(typeof(UniTaskVoidBuilder))]
    public struct UniTaskVoid { public void Forget() { } }
    public struct UniTaskVoidBuilder
    {
        private AsyncTaskMethodBuilder _builder;
        public static UniTaskVoidBuilder Create() => new UniTaskVoidBuilder { _builder = AsyncTaskMethodBuilder.Create() };
        public UniTaskVoid Task => default;
        public void SetResult() => _builder.SetResult();
        public void SetException(Exception exception) => _builder.SetException(exception);
        public void SetStateMachine(IAsyncStateMachine stateMachine) => _builder.SetStateMachine(stateMachine);
        public void Start<T>(ref T machine) where T : IAsyncStateMachine => _builder.Start(ref machine);
        public void AwaitOnCompleted<TA, TS>(ref TA awaiter, ref TS machine)
            where TA : INotifyCompletion where TS : IAsyncStateMachine => _builder.AwaitOnCompleted(ref awaiter, ref machine);
        public void AwaitUnsafeOnCompleted<TA, TS>(ref TA awaiter, ref TS machine)
            where TA : ICriticalNotifyCompletion where TS : IAsyncStateMachine => _builder.AwaitUnsafeOnCompleted(ref awaiter, ref machine);
    }
}
namespace TEngine
{
    public interface IDebuggerModule { void RegisterDebuggerWindow(string name, object window); }
    public interface IFsmModule { }
    public interface IProcedureModule { }
    public interface IResourceModule { }
    public interface IAudioModule { }
    public interface ISceneModule { }
    public interface ITimerModule { }
    public interface ILocalizationModule { }
    public sealed class DebuggerModule : Module, IDebuggerModule
    {
        public override void OnInit() { }
        public override void Shutdown() { }
        public void RegisterDebuggerWindow(string name, object window) { }
    }
    public class RootModule : UnityEngine.MonoBehaviour { }
    public sealed class ResourceModule : Module, IResourceModule
    {
        public override void OnInit() { }
        public override void Shutdown() { }
    }
    public static class Log
    {
        public static void Info(object message) { }
        public static void Warning(object message) { }
        public static void Fatal(object message) { }
        public static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
    public class GameFrameworkException : Exception { public GameFrameworkException(string message) : base(message) { } }
    public static class MemoryPool { public static void ClearAll() { } }
    public static class Utility
    {
        public static class Text { public static string Format(string format, params object[] args) => string.Format(format, args); }
        public static class Marshal { public static void FreeCachedHGlobal() { } }
        public static class Unity
        {
            public static void AddDestroyListener(Action action) => ModuleSystem.GetModule<IUpdateDriver>().AddDestroyListener(action);
        }
    }
}
namespace GameLogic
{
    public sealed class AdModule : Singleton<AdModule>
    {
        public int Releases;
        public void InitAdSdk() { }
        protected override void OnRelease() { Releases++; }
    }
    public sealed class UIModule : Singleton<UIModule>
    {
        public int Releases;
        public void ShowUIAsync<T>() { }
        protected override void OnRelease() { Releases++; }
    }
    public class BattleMainUI { }
    public class AdDebuggerWindow { }
    public static class GameEventHelper { public static void Init() { } }
}
