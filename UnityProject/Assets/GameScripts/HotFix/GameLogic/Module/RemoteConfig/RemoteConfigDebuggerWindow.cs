#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using TEngine;
using UnityEngine;

namespace GameLogic
{
    public sealed class RemoteConfigDebuggerWindow : IDebuggerWindow
    {
        private readonly RemoteConfigModule _config;
        private readonly AnalyticsModule _analytics;
        private readonly string _backend;
        private readonly string _environment;
        private readonly Action _simulateOpportunity;
        public RemoteConfigDebuggerWindow(RemoteConfigModule config, AnalyticsModule analytics, string backend,
            string environment, Action simulateOpportunity = null)
        {
            _config = config;
            _analytics = analytics;
            _backend = backend;
            _environment = environment;
            _simulateOpportunity = simulateOpportunity;
        }
        public void Initialize(params object[] args) { }
        public void Shutdown() { }
        public void OnEnter() { }
        public void OnLeave() { }
        public void OnUpdate(float elapseSeconds, float realElapseSeconds) { }
        public void OnDraw()
        {
            var policy = _config.Interstitial;
            GUILayout.Label($"环境：{_environment} / {_backend}");
            GUILayout.Label($"启动：{_config.Status}；业务已应用：{_config.IsApplied}");
            GUILayout.Label($"采集许可：{GameServices.FirebaseRuntime.AnalyticsAllowed}");
            GUILayout.Label($"来源：{policy.Source}；发布：{policy.Release}");
            GUILayout.Label($"策略：{policy.Revision}；插屏：{policy.Enabled}；间隔：{policy.MinIntervalSeconds}s");
            GUILayout.Label($"运营关闭插屏：{policy.DisabledByRemote}");
            GUILayout.Label($"诊断标记：{policy.Experiment} / {policy.Variant}（后台归属以 Firebase 为准）");
            GUILayout.Label($"本次曝光数：{_analytics.ExposureCount}；最近事件：{_analytics.LastEvent}");
            GUILayout.Label($"错误：{_config.Error}");
            GUILayout.Label("运行中不刷新实验。修改 Editor 模拟值后重新进入 Play；GM 广告按钮不计曝光。");
            if (_simulateOpportunity != null && _backend == "local-simulation" &&
                GUILayout.Button("模拟一次关卡结算插屏机会（仅本地日志）"))
                _simulateOpportunity();
        }
    }
}
#endif
