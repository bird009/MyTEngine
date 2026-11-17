using System;
using System.Collections.Generic;
using System.Reflection;
using GameLogic;
using TEngine;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Module = TEngine.Module;

internal static class Program
{
    private static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("Facade returns fresh UI (and Ad when integrated) after release", FacadeRefresh),
            ("Old reference cannot release a replacement singleton", StaleRelease),
            ("Throwing cleanup still unregisters the singleton", ThrowingRelease),
            ("Bulk release continues after one cleanup throws", BulkRelease),
            ("GameModule shutdown releases business and resets framework facade", Shutdown),
            ("Driver shutdown invokes GameApp cleanup exactly once", DriverShutdown),
            ("Direct driver destruction invokes GameApp cleanup", DriverDestroy),
            ("One destroy callback cannot suppress the others", DestroyException),
            ("Removed pending lifecycle listeners cannot reappear", RemovedListeners),
            ("Business reinitialization uses the replacement update driver", Reinitialize),
            ("Reentrant singleton cleanup runs once", ReentrantRelease),
            ("Bulk cleanup cannot create orphaned business modules", NoCreationDuringRelease),
            ("Removing callbacks from retired driver creates no GameObject", RetiredDriver),
            ("Reentrant driver shutdown remains safe", ReentrantDriver),
            ("Framework shutdown reaches business cleanup", FrameworkShutdown),
            ("Restart clears facade through unified shutdown", Restart),
        };
        int failures = 0;
        foreach (var test in tests)
        {
            try { Reset(); test.Run(); Console.WriteLine("PASS " + test.Name); }
            catch (Exception error) { failures++; Console.WriteLine("FAIL " + test.Name + ": " + error.GetBaseException().Message); }
            finally { Reset(); }
        }
        Console.WriteLine($"{tests.Length - failures}/{tests.Length} passed");
        return failures == 0 ? 0 : 1;
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Reset()
    {
        // Cleanup is intentionally resilient so pre-fix failures don't hide later reproductions.
        try { SingletonSystem.Release(); } catch { }
        foreach (var field in typeof(Program).Assembly.GetTypes())
        {
            if (field.BaseType?.IsGenericType == true && field.BaseType.GetGenericTypeDefinition() == typeof(Singleton<>))
                field.BaseType.GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, null);
        }
        try { GameModule.Shutdown(); } catch { }
        try { ModuleSystem.Shutdown(); } catch { }
        try { UniTask.Flush(); } catch { }
        try { UnityEngine.Object.FlushDestroy(); } catch { }
        foreach (var field in typeof(SingletonSystem).GetFields(BindingFlags.Static | BindingFlags.NonPublic))
        {
            if (field.GetValue(null) is System.Collections.IList list) list.Clear();
            if (field.GetValue(null) is System.Collections.IDictionary dictionary) dictionary.Clear();
        }
        typeof(SingletonSystem).GetField("_isInit", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, false);
        typeof(SingletonSystem).GetField("_updateDriver", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        GameObject.All.Clear();
        Debug.ExceptionCount = 0;
    }
    private static void FacadeRefresh()
    {
        var ui = GameModule.UI;
        ui.Release();
        Check(!ReferenceEquals(ui, GameModule.UI), "UI facade retained released instance");
        // 广告接入可能仍是工作区独立改动；干净提交不依赖该入口存在。
        var adProperty = typeof(GameModule).GetProperty("Ad", BindingFlags.Public | BindingFlags.Static);
        if (adProperty != null)
        {
            var ad = (ISingleton)adProperty.GetValue(null);
            ad.Release();
            Check(!ReferenceEquals(ad, adProperty.GetValue(null)), "Ad facade retained released instance");
        }
    }
    private static void StaleRelease()
    {
        var old = Probe.Instance; old.Release();
        var current = Probe.Instance; old.Release();
        Check(old.Releases == 1 && ReferenceEquals(current, Probe.Instance), "old reference released again or cleared replacement");
    }
    private static void ThrowingRelease()
    {
        var probe = ThrowingProbe.Instance;
        try { probe.Release(); } catch (InvalidOperationException) { }
        Check(!ThrowingProbe.IsValid, "failed cleanup left singleton valid");
        Check(SingletonSystem.GetSingleton(probe.ToString()) == null, "failed cleanup left singleton registered");
    }
    private static void BulkRelease()
    {
        var good = Probe.Instance; var bad = ThrowingProbe.Instance;
        SingletonSystem.Release(); SingletonSystem.Release();
        Check(good.Releases == 1 && !Probe.IsValid && !ThrowingProbe.IsValid, "bulk cleanup was incomplete");
        Check(Debug.ExceptionCount == 1, "cleanup error was not reported exactly once");
    }
    private static void Shutdown()
    {
        var ui = GameModule.UI; var resource = GameModule.Resource;
        GameModule.Shutdown(); GameModule.Shutdown();
        Check(ui.Releases == 1 && !UIModule.IsValid, "shutdown did not release business once");
        Check(ReferenceEquals(resource, ModuleSystem.GetModule<IResourceModule>()), "business shutdown destroyed framework");
        ModuleSystem.Shutdown();
        Check(!ReferenceEquals(resource, GameModule.Resource), "framework facade wasn't cleared");
    }
    private static void StartApp() => GameApp.Entrance(new object[] { new List<Assembly>() });
    private static void DriverShutdown()
    {
        StartApp(); var ui = GameModule.UI;
        var driver = ModuleSystem.GetModule<IUpdateDriver>();
        ((Module)driver).Shutdown(); ((Module)driver).Shutdown();
        UnityEngine.Object.FlushDestroy();
        Check(ui.Releases == 1 && !UIModule.IsValid, "destroy callback was lost or duplicated");
    }
    private static void DriverDestroy()
    {
        StartApp(); var ui = GameModule.UI;
        UnityEngine.Object.Destroy(GameObject.Find("[UpdateDriver]")); UnityEngine.Object.FlushDestroy();
        Check(ui.Releases == 1 && !UIModule.IsValid, "direct destruction skipped business cleanup");
    }
    private static void DestroyException()
    {
        var driver = ModuleSystem.GetModule<IUpdateDriver>(); int called = 0;
        driver.AddDestroyListener(() => throw new InvalidOperationException("expected callback failure"));
        driver.AddDestroyListener(() => called++);
        ((Module)driver).Shutdown(); UnityEngine.Object.FlushDestroy();
        Check(called == 1 && Debug.ExceptionCount == 1, "destroy callbacks were skipped or repeated");
    }
    private static void RemovedListeners()
    {
        var driver = ModuleSystem.GetModule<IUpdateDriver>(); int called = 0;
        Action callback = () => called++;
        driver.AddUpdateListener(callback); driver.AddFixedUpdateListener(callback); driver.AddLateUpdateListener(callback);
        driver.RemoveUpdateListener(callback); driver.RemoveFixedUpdateListener(callback); driver.RemoveLateUpdateListener(callback);
        UniTask.Flush(); Tick();
        Check(called == 0, "deferred registration resurrected removed callbacks");
    }
    private static void Reinitialize()
    {
        var first = Probe.Instance; UniTask.Flush(); Tick();
        Check(first.Updates == 1, "first instance not updated");
        GameModule.Shutdown(); ModuleSystem.Shutdown(); UnityEngine.Object.FlushDestroy();
        var second = Probe.Instance; UniTask.Flush(); Tick();
        Check(!ReferenceEquals(first, second) && first.Releases == 1 && second.Updates == 1 && first.Updates == 1,
            "business remained attached to the old driver");
    }
    private static void ReentrantRelease()
    {
        var probe = ReentrantProbe.Instance; probe.Release();
        Check(probe.Releases == 1 && !ReentrantProbe.IsValid, "reentrant cleanup ran twice");
    }
    private static void NoCreationDuringRelease()
    {
        var probe = CreatingProbe.Instance;
        SingletonSystem.Release();
        Check(!CreatingProbe.IsValid && !Probe.IsValid && Debug.ExceptionCount == 1,
            "cleanup created an untracked replacement module");
        Check(Probe.Instance != null, "creation remained blocked after shutdown");
    }
    private static void RetiredDriver()
    {
        var driver = ModuleSystem.GetModule<IUpdateDriver>();
        ((Module)driver).Shutdown(); UnityEngine.Object.FlushDestroy();
        Action callback = () => { };
        driver.RemoveUpdateListener(callback); driver.RemoveFixedUpdateListener(callback); driver.RemoveLateUpdateListener(callback);
        driver.RemoveDestroyListener(callback); driver.RemoveOnApplicationPauseListener(_ => { });
        driver.RemoveOnDrawGizmosListener(callback); driver.RemoveOnDrawGizmosSelectedListener(callback);
        Check(GameObject.All.Count == 0, "unsubscribe recreated the destroyed driver");
    }
    private static void ReentrantDriver()
    {
        var driver = ModuleSystem.GetModule<IUpdateDriver>(); int called = 0;
        driver.AddDestroyListener(() => { called++; ((Module)driver).Shutdown(); });
        ((Module)driver).Shutdown(); UnityEngine.Object.FlushDestroy();
        Check(called == 1 && Debug.ExceptionCount == 0, "reentrant driver cleanup failed");
    }
    private static void FrameworkShutdown()
    {
        StartApp(); var ui = GameModule.UI;
        ModuleSystem.Shutdown(); UnityEngine.Object.FlushDestroy();
        Check(ui.Releases == 1 && !UIModule.IsValid, "framework shutdown skipped business cleanup");
    }
    private static void Restart()
    {
        var resource = GameModule.Resource; var ui = GameModule.UI;
        SingletonSystem.Restart(); ModuleSystem.Shutdown(); UnityEngine.Object.FlushDestroy();
        Check(ui.Releases == 1 && !ReferenceEquals(resource, GameModule.Resource), "restart left facade state behind");
    }
    private static void Tick()
    {
        foreach (var go in GameObject.All.ToArray()) { go.Invoke("Update"); go.Invoke("FixedUpdate"); go.Invoke("LateUpdate"); }
    }
}
public sealed class Probe : Singleton<Probe>, IUpdate
{
    public int Releases;
    public int Updates;
    public void OnUpdate() { Updates++; }
    protected override void OnRelease() { Releases++; }
}
public sealed class ThrowingProbe : Singleton<ThrowingProbe>
{
    protected override void OnRelease() => throw new InvalidOperationException("expected cleanup failure");
}
public sealed class ReentrantProbe : Singleton<ReentrantProbe>
{
    public int Releases;
    protected override void OnRelease() { Releases++; Release(); }
}
public sealed class CreatingProbe : Singleton<CreatingProbe>
{
    protected override void OnRelease() { _ = Probe.Instance; }
}
