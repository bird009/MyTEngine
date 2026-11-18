#if AD_SDK_MAX
using System;
using TEngine;

namespace GameLogic
{
    /// <summary>
    /// AppLovin MAX 广告服务（休眠：MAX 插件已移除——账号被拒后主路径切换 AdMob，插件残留曾导致 minSdk 冲突无法打包）。
    /// 本类被 AD_SDK_MAX 宏隔离，不参与编译。恢复路径：重新导入 Downloads/ 下的 MAX unitypackage、
    /// GameLogic.asmdef 加回 MaxSdk.Scripts 引用（GUID:a4cfc1a18fa3a469b96d885db522f42e）、csc.rsp 切换宏。
    /// </summary>
    public sealed class MaxAdService : IAdService
    {
        private Action<bool, int> _rewardCallback;
        private Action<bool> _interCallback;

        public void Init()
        {
            MaxSdk.SetSdkKey(AdPlacementIds.MaxSdkKey);
            MaxSdk.InitializeSdk();

            // 激励视频回调
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += OnRewardAdLoaded;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnRewardAdReceived;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnRewardAdHidden;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += (_, errorInfo) =>
            {
                Log.Warning($"[Ad] MAX 激励视频加载失败：{errorInfo.AdUnitIdentifier} {errorInfo.Message}");
                PreloadRewardAd();
            };
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += (_, _, _) => OnRewardAdFinished(false);

            // 插屏回调
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += (_, _) => OnInterAdFinished(true);
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += (_, errorInfo) =>
            {
                Log.Warning($"[Ad] MAX 插屏加载失败：{errorInfo.AdUnitIdentifier} {errorInfo.Message}");
                PreloadInterAd();
            };
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += (_, _, _) => OnInterAdFinished(false);

            PreloadRewardAd();
            PreloadInterAd();
        }

        public bool IsAdReady(AdType adType)
        {
            return adType switch
            {
                AdType.Reward => MaxSdk.IsRewardedAdReady(GetAdUnitId(adType)),
                AdType.Interstitial => MaxSdk.IsInterstitialReady(GetAdUnitId(adType)),
                _ => false,
            };
        }

        public void ShowRewardAd(Action<bool, int> callback)
        {
            if (_rewardCallback != null)
            {
                // 展示中重入：拒绝新请求，避免覆盖丢失上一次的发奖回调
                Log.Warning("[Ad] MAX 激励视频正在展示中，本次请求被拒绝。");
                callback?.Invoke(false, 0);
                return;
            }

            _rewardCallback = callback;
            MaxSdk.ShowRewardedAd(GetAdUnitId(AdType.Reward));
        }

        public void ShowInterAd(Action<bool> callback)
        {
            if (_interCallback != null)
            {
                Log.Warning("[Ad] MAX 插屏正在展示中，本次请求被拒绝。");
                callback?.Invoke(false);
                return;
            }

            _interCallback = callback;
            MaxSdk.ShowInterstitial(GetAdUnitId(AdType.Interstitial));
        }

        /// <summary>
        /// 从 TbAd 配置表读取指定类型的广告位 ID（MAX 账号开放后启用）。
        /// </summary>
        private static string GetAdUnitId(AdType adType)
        {
            return AdConfigMgr.Instance.GetAd((int)adType)?.AdUnitId ?? string.Empty;
        }

        private void PreloadRewardAd()
        {
            MaxSdk.LoadRewardedAd(GetAdUnitId(AdType.Reward));
        }

        private void PreloadInterAd()
        {
            MaxSdk.LoadInterstitial(GetAdUnitId(AdType.Interstitial));
        }

        private void OnRewardAdLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Log.Debug($"[Ad] MAX 激励视频加载完成：{adUnitId}");
        }

        private void OnRewardAdReceived(string adUnitId, MaxSdkBase.Reward reward, MaxSdkBase.AdInfo adInfo)
        {
            // 收到奖励发放事件，在广告关闭时统一回调
            _rewardReceived = true;
        }

        private bool _rewardReceived;

        private void OnRewardAdHidden(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            OnRewardAdFinished(_rewardReceived);
            _rewardReceived = false;
        }

        private void OnRewardAdFinished(bool received)
        {
            PreloadRewardAd();
            _rewardCallback?.Invoke(received, received ? 1 : 0);
            _rewardCallback = null;
        }

        private void OnInterAdFinished(bool shown)
        {
            PreloadInterAd();
            _interCallback?.Invoke(shown);
            _interCallback = null;
        }
    }
}
#endif
