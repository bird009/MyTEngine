using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameLogic;
using GameServices;
using UnityEngine;

internal static class Program
{
    private static readonly AdPolicy Baseline = new AdPolicy(true, 0, 30);
    private static string Json(string variant = "b", int interval = 90) =>
        "{\"schema\":1,\"policy_revision\":\"test\",\"experiment_key\":\"inter_gap_001\",\"variant\":\"" + variant + "\",\"min_interval_sec\":" + interval + "}";
    private static RemoteConfigValues Values(string json = null, bool disabled = false) => new RemoteConfigValues(json ?? Json(), true, disabled, "r1");
    private static AdPolicy Policy() => AdPolicyResolver.Resolve(Baseline, Values(), true, "test", out _);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static async Task<int> Main()
    {
        var cases = new (string, Func<Task>)[]
        {
            ("Valid A/B values and immutable baseline", () => Sync(() => {
                Check(Policy().MinIntervalSeconds == 90 && Baseline.MinIntervalSeconds == 30, "mutated baseline");
                var a = AdPolicyResolver.Resolve(Baseline, Values(Json("a", 60)), true, "test", out var error);
                Check(error == "" && a.Variant == "a" && a.MinIntervalSeconds == 60, "A rejected"); })),
            ("Malformed, duplicate, unsupported and mismatched payloads rejected atomically", () => Sync(() => {
                foreach (var json in new[] { "{", Json().Replace("\"schema\":1", "\"schema\":2"), Json("a", 90),
                    Json().Replace("90", "-1"), Json().Replace("90", "\"90\""), Json().Replace("\"schema\":1", "\"schema\":1,\"schema\":1"),
                    Json().Replace("\"policy_revision\":\"test\",", "") }) {
                    var p = AdPolicyResolver.Resolve(Baseline, Values(json), true, "test", out var error);
                    Check(error.Length > 0 && p.MinIntervalSeconds == 30 && !p.IsExperiment, "accepted " + json);
                } })),
            ("No consent does not apply experiment; kill switch survives invalid payload", () => Sync(() => {
                var p = AdPolicyResolver.Resolve(Baseline, Values(), false, "test", out _);
                Check(!p.IsExperiment && p.MinIntervalSeconds == 30, "experiment applied without consent");
                p = AdPolicyResolver.Resolve(Baseline, Values("{", true), true, "test", out _);
                Check(!p.Enabled, "kill switch lost"); })),
            ("Default values never masquerade as control group", () => Sync(() => {
                var p = AdPolicyResolver.Resolve(Baseline, new RemoteConfigValues(Json("a", 60)), true, "test", out _);
                Check(!p.IsExperiment && p.Source == "builtin", "fallback enrolled"); })),
            ("Startup activates cached candidate and then successful fetch", async () => {
                var b = new FakeBackend { Pending = Values(Json("a", 60)), Fetched = Values() };
                var s = new ConfigStartupSession();
                var values = await Run(s, b);
                Check(values.PolicyJson == Json() && b.Activations == 2 && s.Status == "simulation", "wrong startup order"); }),
            ("Late fetch after timeout never activates", async () => {
                var completion = new TaskCompletionSource<bool>();
                var b = new FakeBackend { FetchWait = completion.Task, Activated = Values(Json("a", 60)), Fetched = Values() };
                var s = new ConfigStartupSession();
                var values = await Run(s, b, 15);
                Check(s.Status == "cached_timeout" && values.PolicyJson == Json("a", 60), "cache lost");
                completion.SetResult(true); await b.LastFetch;
                Check(b.Activations == 1 && b.Activated.PolicyJson == Json("a", 60), "late activation"); }),
            ("Late initialization never starts fetch/activation", async () => {
                var completion = new TaskCompletionSource<bool>();
                var b = new FakeBackend { InitWait = completion.Task };
                var s = new ConfigStartupSession(); await Run(s, b, 15);
                completion.SetResult(true); await b.LastInit;
                Check(s.Status == "initialization_timeout" && b.Activations == 0 && b.Fetches == 0, "late init leaked"); }),
            ("Fetch failure keeps valid activated cache", async () => {
                var b = new FakeBackend { Activated = Values(), FetchWait = Task.FromException(new Exception("offline")) };
                var s = new ConfigStartupSession(); var v = await Run(s, b);
                Check(s.Status == "cached_error" && v.PolicyJson == Json(), "offline fallback lost"); }),
            ("Cancellation blocks late fetch activation", async () => {
                var completion = new TaskCompletionSource<bool>(); var cts = new CancellationTokenSource();
                var b = new FakeBackend { FetchWait = completion.Task };
                var t = Run(new ConfigStartupSession(), b, 1000, cts.Token); cts.Cancel();
                try { await t; throw new Exception("not cancelled"); } catch (OperationCanceledException) { }
                completion.SetResult(true); await b.LastFetch;
                Check(b.Activations == 1, "released session activated"); }),
            ("Activation holds process gate until complete even after cancellation", async () => {
                var gate = new SemaphoreSlim(1, 1); var cts = new CancellationTokenSource();
                var completion = new TaskCompletionSource<bool>();
                var b = new FakeBackend { ActivationWait = completion.Task };
                var first = new ConfigStartupSession().RunAsync(b, 1000, () => true, cts.Token, gate);
                cts.Cancel(); var second = new ConfigStartupSession();
                await second.RunAsync(new FakeBackend(), 15, () => true, default, gate);
                Check(second.Status == "startup_busy", "overlapping activation");
                completion.SetResult(true);
                try { await first; throw new Exception("not cancelled"); } catch (OperationCanceledException) { }
                Check(gate.CurrentCount == 1, "gate leaked"); }),
            ("Exposure dedup and result snapshot", () => Sync(() => {
                var b = ReadyBackend(); var a = AnalyticsModule.Instance; a.Initialize(b);
                a.TrackOpportunity(Policy(), "level"); a.TrackOpportunity(Policy(), "level");
                a.TrackResult(Policy(), "level", "not_ready");
                Check(b.Events.Count(e => e.Name == RemoteConfigKeys.Exposure) == 1, "duplicate exposure");
                Check((double)b.Events.Last().Values["interval_sec"] == 90, "snapshot mismatch"); })),
#if TEST_AD_INTEGRATION
            ("Query and GM do not expose; not-ready opportunity still exposes", () => Sync(() => {
                var b = ReadyBackend(); var a = AnalyticsModule.Instance; a.Initialize(b);
                var ad = AdModule.Instance; ad.Configure(Policy(), a); ad.InitAdSdk(); Time.realtimeSinceStartup = 100;
                ad.CanShow(AdType.Interstitial); ad.CanShow(AdType.Interstitial);
                ad.ShowInterAd(AdsEntrance.Default, diagnosticOnly: true);
                Check(b.Events.Count == 0, "GM/query exposed");
                PlayerPrefs.DeleteAll(); EditorAdService.Ready = false;
                ad.ShowInterAd(AdsEntrance.LevelComplete);
                Check(b.Events.Any(e => e.Name == RemoteConfigKeys.Exposure), "not-ready excluded"); })),
            ("Interval-blocked opportunity still exposes", () => Sync(() => {
                var b = ReadyBackend(); var a = AnalyticsModule.Instance; a.Initialize(b);
                var ad = AdModule.Instance; ad.Configure(Policy(), a); ad.InitAdSdk(); Time.realtimeSinceStartup = 100;
                new AdScheduler().RecordShow(AdType.Interstitial);
                ad.ShowInterAd(AdsEntrance.LevelComplete);
                Check(b.Events.Any(e => e.Name == RemoteConfigKeys.Exposure), "interval gate preceded exposure");
                Check((string)b.Events.Last().Values["result"] == "interval", "wrong result"); })),
            ("Released ad callback cannot emit or touch replacement", () => Sync(() => {
                var b = ReadyBackend(); var a = AnalyticsModule.Instance; a.Initialize(b);
                var ad = AdModule.Instance; ad.Configure(Policy(), a); ad.InitAdSdk(); Time.realtimeSinceStartup = 100;
                EditorAdService.Defer = true; ad.ShowInterAd(AdsEntrance.LevelComplete);
                int events = b.Events.Count; ad.Release(); a.Release(); EditorAdService.Pending(true);
                Check(b.Events.Count == events && !AdModule.IsValid && !AnalyticsModule.IsValid, "callback revived modules"); })),
            ("UTC interval persists across app clock reset and daily stamp rolls", () => Sync(() => {
                var s = new AdScheduler(); s.Configure(Policy()); s.OnAppStart(); Time.realtimeSinceStartup = 1000;
                s.RecordShow(AdType.Interstitial);
                Time.realtimeSinceStartup = 0; var restarted = new AdScheduler(); restarted.Configure(Policy()); restarted.OnAppStart();
                Time.realtimeSinceStartup = 40;
                Check(!restarted.CanShowInterstitial(Policy(), out var why) && why == "interval", "cross-launch interval broken");
                PlayerPrefs.SetString("Ad_LastShowUtcV1_Interstitial", (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 100).ToString());
                PlayerPrefs.SetString("Ad_DayStamp", "20000101"); PlayerPrefs.SetInt("Ad_DayCount_Interstitial", 99);
                var capped = new AdPolicy(true, 1, 90);
                Check(restarted.CanShowInterstitial(capped, out _) && PlayerPrefs.GetInt("Ad_DayCount_Interstitial") == 0, "day not reset"); })),
#endif
            ("RemoteConfigModule release prevents late Applied", async () => {
                var b = ReadyBackend(); var completion = new TaskCompletionSource<bool>(); b.FetchWait = completion.Task;
                var a = AnalyticsModule.Instance; a.Initialize(b); var config = RemoteConfigModule.Instance;
                var task = config.InitializeAsync(b, new FirebaseSettings(), a); config.Release();
                try { await task; throw new Exception("not cancelled"); } catch (OperationCanceledException) { }
                completion.SetResult(true); await b.LastFetch;
                Check(!config.IsApplied && !RemoteConfigModule.IsValid, "released config applied"); }),
#if TEST_AD_INTEGRATION
            ("SDK exception clears in-flight state and permits next request", () => Sync(() => {
                var b = ReadyBackend(); var a = AnalyticsModule.Instance; a.Initialize(b);
                var ad = AdModule.Instance; ad.Configure(Policy(), a); ad.InitAdSdk(); Time.realtimeSinceStartup = 100;
                EditorAdService.ThrowOnShow = true; ad.ShowInterAd(AdsEntrance.LevelComplete);
                Check((string)b.Events.Last().Values["result"] == "sdk_exception", "missing exception result");
                EditorAdService.ThrowOnShow = false; bool completed = false;
                ad.ShowInterAd(AdsEntrance.LevelComplete, ok => completed = ok);
                Check(completed, "SDK exception left request stuck"); })),
            ("Duplicate callback from previous request cannot complete next request", () => Sync(() => {
                var b = ReadyBackend(); var a = AnalyticsModule.Instance; a.Initialize(b);
                var ad = AdModule.Instance; ad.Configure(Policy(), a); ad.InitAdSdk(); Time.realtimeSinceStartup = 100;
                EditorAdService.Defer = true; ad.ShowInterAd(AdsEntrance.LevelComplete);
                var first = EditorAdService.Pending; first(false);
                bool nextCompleted = false; ad.ShowInterAd(AdsEntrance.LevelComplete, ok => nextCompleted = ok);
                int count = b.Events.Count; first(true);
                Check(b.Events.Count == count && !nextCompleted, "old callback affected new request");
                EditorAdService.Pending(true); Check(nextCompleted, "new callback blocked"); })),
#endif
            ("Custom statistics validate parameters and honor release", () => Sync(() => {
                var b = ReadyBackend(); var a = AnalyticsModule.Instance; a.Initialize(b);
                Check(a.TrackEvent("level_complete", new Dictionary<string, object> { { "level", 3 } }), "custom event failed");
                Check(b.Events.Last().Values["level"] is long, "numeric value not normalized");
                Check(!a.TrackEvent("firebase_bad") && !a.TrackEvent("1bad") &&
                    !a.TrackEvent("event", new Dictionary<string, object> { { "value", double.NaN } }), "invalid event accepted");
                a.Release(); Check(!a.TrackEvent("level_complete"), "released analytics emitted"); })),
        };
        int failed = 0;
        foreach (var test in cases)
        {
            Reset();
            try { await test.Item2(); Console.WriteLine("PASS " + test.Item1); }
            catch (Exception e) { failed++; Console.WriteLine("FAIL " + test.Item1 + ": " + e); }
        }
        Reset(); Console.WriteLine($"{cases.Length - failed}/{cases.Length} passed"); return failed == 0 ? 0 : 1;
    }
    private static Task Sync(Action action) { action(); return Task.CompletedTask; }
    private static Task<RemoteConfigValues> Run(ConfigStartupSession session, FakeBackend backend, int budget = 1000, CancellationToken cancellation = default)
        => session.RunAsync(backend, budget, () => true, cancellation, new SemaphoreSlim(1, 1));
    private static FakeBackend ReadyBackend() { var b = new FakeBackend(); b.InitializeAsync().GetAwaiter().GetResult(); return b; }
    private static void Reset()
    {
#if TEST_AD_INTEGRATION
        if (AdModule.IsValid) AdModule.Instance.Release();
        EditorAdService.Ready = true; EditorAdService.Defer = false; EditorAdService.ThrowOnShow = false;
#endif
        if (RemoteConfigModule.IsValid) RemoteConfigModule.Instance.Release();
        if (AnalyticsModule.IsValid) AnalyticsModule.Instance.Release();
        PlayerPrefs.DeleteAll(); Time.realtimeSinceStartup = 0;
        FirebaseRuntime.SetAnalyticsConsent(false);
    }
}

internal sealed class FakeBackend : IRemoteConfigBackend
{
    public string Name => "fake";
    public bool IsReady { get; private set; }
    public bool IsSimulation => true;
    public Task InitWait = Task.CompletedTask, FetchWait = Task.CompletedTask, ActivationWait = Task.CompletedTask;
    public Task LastInit, LastFetch;
    public int Activations, Fetches;
    public RemoteConfigValues Activated = new RemoteConfigValues(), Pending, Fetched;
    public readonly List<(string Name, Dictionary<string, object> Values)> Events = new List<(string, Dictionary<string, object>)>();
    public Task InitializeAsync() => LastInit = Init();
    private async Task Init() { await InitWait; IsReady = true; }
    public Task FetchAsync() => LastFetch = Fetch();
    private async Task Fetch() { Fetches++; await FetchWait; Pending = Fetched; }
    public async Task ActivateAsync() { Activations++; await ActivationWait; if (Pending != null) { Activated = Pending; Pending = null; } }
    public RemoteConfigValues ReadActivated() => Activated;
    public void SetAnalyticsCollection(bool enabled) { }
    public void LogEvent(string name, IReadOnlyDictionary<string, object> parameters) => Events.Add((name, new Dictionary<string, object>(parameters)));
}
