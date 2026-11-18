using System.Collections.Generic;
using System.Reflection;
using System;
using Cysharp.Threading.Tasks;
using GameServices;
using Launcher;
using GameLogic;
#if ENABLE_OBFUZ
using Obfuz;
#endif
using TEngine;
#pragma warning disable CS0436


/// <summary>
/// 游戏App。
/// </summary>
#if ENABLE_OBFUZ
[ObfuzIgnore(ObfuzScope.TypeName | ObfuzScope.MethodName)]
#endif
public partial class GameApp
{
    private static List<Assembly> _hotfixAssembly;
    private static int _businessGeneration;

    /// <summary>
    /// 热更域App主入口。
    /// </summary>
    /// <param name="objects"></param>
    public static void Entrance(object[] objects)
    {
        GameEventHelper.Init();
        _hotfixAssembly = (List<Assembly>)objects[0];
        Log.Warning("======= 看到此条日志代表你成功运行了热更新代码 =======");
        Log.Warning("======= Entrance GameApp =======");
        Utility.Unity.AddDestroyListener(Release);
        Log.Warning("======= StartGameLogic =======");
        StartGameLogic(++_businessGeneration).Forget();
    }
    
    private static async UniTaskVoid StartGameLogic(int generation)
    {
        var settings = FirebaseSettings.Load();
        var backend = FirebaseRuntime.GetBackend(settings);
        var analytics = GameModule.Analytics;
        analytics.Initialize(backend);
        var configs = GameModule.RemoteConfig;
        try
        {
            await configs.InitializeAsync(backend, settings, analytics, AdModule.LoadInterstitialBaseline);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception exception) { Log.Warning("Remote config startup failed: " + exception.Message); }
        if (generation != _businessGeneration) return;

        GameModule.Ad.Configure(configs.Interstitial, analytics);
        // Existing ad consent entry; the product consent flow must separately gate ad SDK initialization.
        GameModule.Ad.InitAdSdk();

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        // 广告调试页签：注册进 GM 控制台（~ 键呼出），正式包随 Debugger 自动剔除
        GameModule.Debugger?.RegisterDebuggerWindow("GM/广告", new AdDebuggerWindow());
        GameModule.Debugger?.RegisterDebuggerWindow("GM/远程配置", new RemoteConfigDebuggerWindow(
            configs, analytics, backend.Name, settings.environment,
            () => GameModule.Ad.ShowInterAd(AdsEntrance.LevelComplete)));
#endif

        // GameEvent.Get<ILoginUI>().ShowLoginUI();
        try
        {
            await GameModule.UI.ShowUIAsyncAwait<BattleMainUI>();
            if (generation == _businessGeneration) LauncherMgr.HideAllUI();
        }
        catch (Exception exception) { Log.Warning("Main UI startup failed: " + exception.Message); }
    }
    
    private static void Release()
    {
        ++_businessGeneration;
        GameModule.Shutdown();
        _hotfixAssembly = null;
        Log.Warning("======= Release GameApp =======");
    }
}
