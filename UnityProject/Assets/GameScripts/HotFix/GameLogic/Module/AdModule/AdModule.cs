using System;
using TEngine;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// 广告管理模块。统一门面：频控调度、SDK 服务分发、事件广播。
    /// 业务侧通过 GameModule.Ad 访问。
    /// </summary>
    public sealed class AdModule : Singleton<AdModule>
    {
        private const string PREF_ADS_DISABLED = "Ad_GM_Disabled";

        private readonly AdScheduler _scheduler = new AdScheduler();
        private IAdService _adService;
        private bool _sdkInitialized;
        private bool _released;
        private bool _interstitialInFlight;
        private int _interstitialRequest;
        private AnalyticsModule _analytics;

        public static AdPolicy LoadInterstitialBaseline()
        {
            var row = AdConfigMgr.Instance.GetAd((int)AdType.Interstitial);
            return row == null ? null : new AdPolicy(row.Enabled, row.DailyLimit, row.MinIntervalSec);
        }

        public void Configure(AdPolicy policy, AnalyticsModule analytics)
        {
            _scheduler.Configure(policy);
            _analytics = analytics;
        }

        /// <summary>
        /// GM 广告总开关（持久化到本地）：true 时所有展示入口直接走无广告分支，激励视频回调直接成功。
        /// </summary>
        public bool AdsDisabled
        {
            get => PlayerPrefs.GetInt(PREF_ADS_DISABLED, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(PREF_ADS_DISABLED, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// 是否已发起 SDK 初始化。广告就绪状态通过 IsAdReady 查询。
        /// </summary>
        public bool SdkInitialized => _sdkInitialized;

        protected override void OnInit()
        {
            _scheduler.OnAppStart();

            // 聚合平台分发：按构建宏选择（当前 AdMob；MaxAdService 留作 MAX 账号开放后启用）。
            // 未导入 SDK 插件的构建（宏未定义）走 Editor 模拟桩，保证流程可跑通。
#if UNITY_EDITOR
            _adService = new EditorAdService();
#elif AD_SDK_ADMOB && (UNITY_ANDROID || UNITY_IOS)
            _adService = new AdMobAdService();
#elif AD_SDK_MAX
            _adService = new MaxAdService();
#else
            _adService = new EditorAdService();
#endif
        }

        protected override void OnRelease()
        {
            _released = true;
            _analytics = null;
            var service = _adService;
            _adService = null;
            _sdkInitialized = false;
            (service as IDisposable)?.Dispose();
        }

        /// <summary>
        /// 初始化广告 SDK。正式接入由业务根据广告许可结果调用。
        /// </summary>
        public void InitAdSdk()
        {
            if (_released) return;
            if (_sdkInitialized)
            {
                return;
            }

            try
            {
                _adService.Init();
                _sdkInitialized = true;
                Log.Info("[Ad] 广告 SDK 初始化已发起。");
            }
            catch (Exception exception)
            {
                Log.Warning("[Ad] 广告 SDK 初始化失败：" + exception.Message);
            }
        }

        /// <summary>
        /// 指定类型广告是否就绪（SDK 缓存维度，不含频控判断）。
        /// </summary>
        /// <param name="adType">广告类型。</param>
        /// <returns>是否就绪。</returns>
        public bool IsAdReady(AdType adType)
        {
            return !AdsDisabled && _sdkInitialized && _adService.IsAdReady(adType);
        }

        /// <summary>
        /// 综合判断广告当前是否可展示（GM 开关 + 频控 + SDK 就绪）。业务侧控制按钮可用态时使用。
        /// </summary>
        /// <param name="adType">广告类型。</param>
        /// <returns>是否可展示。</returns>
        public bool CanShow(AdType adType)
        {
            return IsAdReady(adType) && _scheduler.CanShow(adType);
        }

        /// <summary>
        /// 展示激励视频。受配置表频控约束（enabled/日上限/最小间隔），被拦截时不发奖。
        /// </summary>
        /// <param name="entrance">入口点位。</param>
        /// <param name="callback">回调参数：是否完整观看（可发奖）、奖励数量。</param>
        public void ShowRewardAd(AdsEntrance entrance, Action<bool, int> callback)
        {
            if (_released)
            {
                callback?.Invoke(false, 0);
                return;
            }
            if (AdsDisabled)
            {
                // GM 关闭广告：直接走成功分支，便于调试
                callback?.Invoke(true, 1);
                return;
            }

            if (!_sdkInitialized || !_scheduler.CanShow(AdType.Reward))
            {
                Log.Debug("[Ad] 激励视频展示被拦截：SDK 未初始化或频控限制。");
                callback?.Invoke(false, 0);
                return;
            }

            _adService.ShowRewardAd((success, rewardAmount) =>
            {
                if (_released) return;
                if (success)
                {
                    _scheduler.RecordShow(AdType.Reward);
                    GameEvent.Get<IAdEvent>().OnAdShown(AdType.Reward, entrance);
                    GameEvent.Get<IAdEvent>().OnAdRewardCompleted(entrance, rewardAmount);
                }

                GameEvent.Get<IAdEvent>().OnAdClosed(AdType.Reward, entrance);
                callback?.Invoke(success, rewardAmount);
            });
        }

        /// <summary>
        /// 展示插屏。受频控约束（间隔/日上限/启动保护），被拦截时静默失败。
        /// </summary>
        /// <param name="entrance">入口点位。</param>
        /// <param name="callback">回调参数：是否完整展示。</param>
        public void ShowInterAd(AdsEntrance entrance, Action<bool> callback = null, bool diagnosticOnly = false)
        {
            if (_released || AdsDisabled || _interstitialInFlight)
            {
                callback?.Invoke(false);
                return;
            }
            var policy = _scheduler.InterstitialPolicy;
            var analytics = diagnosticOnly ? null : _analytics;
            var point = entrance.ToString();
            // An actual business request, not CanShow/UI polling. Record before variant-dependent gates.
            if (_scheduler.CanOfferInterstitial(policy)) analytics?.TrackOpportunity(policy, point);
            if (!_scheduler.CanShowInterstitial(policy, out var reason))
            {
                analytics?.TrackResult(policy, point, reason);
                callback?.Invoke(false);
                return;
            }
            if (!_sdkInitialized || !_adService.IsAdReady(AdType.Interstitial))
            {
                analytics?.TrackResult(policy, point, _sdkInitialized ? "not_ready" : "sdk_not_initialized");
                callback?.Invoke(false);
                return;
            }
            _interstitialInFlight = true;
            int request = ++_interstitialRequest;
            try
            {
                _adService.ShowInterAd(shown =>
                {
                    if (_released || !_interstitialInFlight || request != _interstitialRequest) return;
                    _interstitialInFlight = false;
                    // Capture the request's policy and module, never re-resolve GameModule from a delayed callback.
                    analytics?.TrackResult(policy, point, shown ? "completed" : "failed");
                    if (shown)
                    {
                        _scheduler.RecordShow(AdType.Interstitial);
                        GameEvent.Get<IAdEvent>().OnAdShown(AdType.Interstitial, entrance);
                    }

                    GameEvent.Get<IAdEvent>().OnAdClosed(AdType.Interstitial, entrance);
                    callback?.Invoke(shown);
                });
            }
            catch (Exception exception)
            {
                Log.Warning("[Ad] 插屏调用失败：" + exception.Message);
                if (!_released && _interstitialInFlight && request == _interstitialRequest)
                {
                    _interstitialInFlight = false;
                    analytics?.TrackResult(policy, point, "sdk_exception");
                    callback?.Invoke(false);
                }
            }
        }
    }
}
