#if AD_SDK_ADMOB
using System;
using GoogleMobileAds.Api;
using TEngine;

namespace GameLogic
{
    /// <summary>
    /// Google AdMob 广告服务。
    /// 依赖：Google Mobile Ads Unity 插件（已导入 11.5.0，API 已按该版本反射核对）。
    /// 激活：csc.rsp 定义 AD_SDK_ADMOB（Editor 运行走 EditorAdService 桩）。
    /// 注意：AdMob 全屏广告实例一次性使用，关闭后需重新 Load。
    /// </summary>
    public sealed class AdMobAdService : IAdService, IDisposable
    {
        private RewardedAd _rewardedAd;
        private InterstitialAd _interstitialAd;

        private Action<bool, int> _rewardCallback;
        private Action<bool> _interCallback;
        private bool _rewardReceived;
        private bool _disposed;

        public void Init()
        {
            // 广告事件回调提升到 Unity 主线程（插件要求，原生线程不可访问引擎 API）
            MobileAds.RaiseAdEventsOnUnityMainThread = true;

            MobileAds.Initialize(status =>
            {
                if (_disposed || status == null) return;
                Log.Info($"[Ad] AdMob 初始化完成：{status}");
                PreloadRewardAd();
                PreloadInterAd();
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var reward = _rewardedAd;
            var interstitial = _interstitialAd;
            _rewardedAd = null;
            _interstitialAd = null;
            _rewardCallback = null;
            _interCallback = null;
            reward?.Destroy();
            interstitial?.Destroy();
        }

        public bool IsAdReady(AdType adType)
        {
            return adType switch
            {
                AdType.Reward => _rewardedAd != null && _rewardedAd.CanShowAd(),
                AdType.Interstitial => _interstitialAd != null && _interstitialAd.CanShowAd(),
                _ => false,
            };
        }

        public void ShowRewardAd(Action<bool, int> callback)
        {
            if (_rewardCallback != null)
            {
                // 展示中重入：拒绝新请求，避免覆盖丢失上一次的发奖回调
                Log.Warning("[Ad] AdMob 激励视频正在展示中，本次请求被拒绝。");
                callback?.Invoke(false, 0);
                return;
            }

            if (!IsAdReady(AdType.Reward))
            {
                callback?.Invoke(false, 0);
                return;
            }

            _rewardCallback = callback;
            _rewardReceived = false;
            var ad = _rewardedAd;
            try
            {
                ad.Show(reward =>
                {
                    if (_rewardedAd == ad) _rewardReceived = true;
                });
            }
            catch
            {
                OnRewardAdFinished(ad, false);
                throw;
            }
        }

        public void ShowInterAd(Action<bool> callback)
        {
            if (_interCallback != null)
            {
                Log.Warning("[Ad] AdMob 插屏正在展示中，本次请求被拒绝。");
                callback?.Invoke(false);
                return;
            }

            if (!IsAdReady(AdType.Interstitial))
            {
                callback?.Invoke(false);
                return;
            }

            _interCallback = callback;
            var ad = _interstitialAd;
            try { ad.Show(); }
            catch
            {
                OnInterAdFinished(ad, false);
                throw;
            }
        }

        private static string GetAdUnitId(AdType adType)
        {
            return AdConfigMgr.Instance.GetAd((int)adType)?.AdUnitId ?? string.Empty;
        }

        private void PreloadRewardAd()
        {
            if (_disposed) return;
            RewardedAd.Load(GetAdUnitId(AdType.Reward), new AdRequest(), (ad, error) =>
            {
                if (_disposed)
                {
                    ad?.Destroy();
                    return;
                }
                if (ad == null || error != null)
                {
                    ad?.Destroy();
                    Log.Warning($"[Ad] AdMob 激励视频加载失败：{error?.GetMessage()}");
                    _rewardedAd = null;
                    return;
                }

                _rewardedAd = ad;
                _rewardedAd.OnAdPaid += adValue =>
                    Log.Debug($"[Ad] AdMob 激励视频收入：{adValue.Value} {adValue.CurrencyCode}");
                _rewardedAd.OnAdFullScreenContentClosed += () => OnRewardAdFinished(ad, _rewardReceived);
                _rewardedAd.OnAdFullScreenContentFailed += adError =>
                {
                    Log.Warning($"[Ad] AdMob 激励视频展示失败：{adError.GetMessage()}");
                    OnRewardAdFinished(ad, false);
                };
                Log.Debug("[Ad] AdMob 激励视频加载完成。");
            });
        }

        private void PreloadInterAd()
        {
            if (_disposed) return;
            InterstitialAd.Load(GetAdUnitId(AdType.Interstitial), new AdRequest(), (ad, error) =>
            {
                if (_disposed)
                {
                    ad?.Destroy();
                    return;
                }
                if (ad == null || error != null)
                {
                    ad?.Destroy();
                    Log.Warning($"[Ad] AdMob 插屏加载失败：{error?.GetMessage()}");
                    _interstitialAd = null;
                    return;
                }

                _interstitialAd = ad;
                _interstitialAd.OnAdPaid += adValue =>
                    Log.Debug($"[Ad] AdMob 插屏收入：{adValue.Value} {adValue.CurrencyCode}");
                _interstitialAd.OnAdFullScreenContentClosed += () => OnInterAdFinished(ad, true);
                _interstitialAd.OnAdFullScreenContentFailed += adError =>
                {
                    Log.Warning($"[Ad] AdMob 插屏展示失败：{adError.GetMessage()}");
                    OnInterAdFinished(ad, false);
                };
                Log.Debug("[Ad] AdMob 插屏加载完成。");
            });
        }

        private void OnRewardAdFinished(RewardedAd ad, bool received)
        {
            if (_disposed || _rewardedAd != ad) return;
            var callback = _rewardCallback;
            _rewardedAd = null;
            _rewardCallback = null;
            _rewardReceived = false;
            ad.Destroy();
            try { callback?.Invoke(received, received ? 1 : 0); }
            finally { PreloadRewardAd(); }
        }

        private void OnInterAdFinished(InterstitialAd ad, bool shown)
        {
            if (_disposed || _interstitialAd != ad) return;
            var callback = _interCallback;
            _interstitialAd = null;
            _interCallback = null;
            ad.Destroy();
            try { callback?.Invoke(shown); }
            finally { PreloadInterAd(); }
        }
    }
}
#endif
