using System;
using System.Globalization;
using GameConfig.ad;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// 广告频控调度器。按广告类型控制最小展示间隔、每日展示上限与启动保护，参数来自 TbAd 配置表（可热更）。
    /// 激励视频沿用本地配置，插屏使用启动时确定的策略及启动保护。
    /// </summary>
    public class AdScheduler
    {
        private const string PREF_LAST_SHOW_PREFIX = "Ad_LastShowUtcV1_";
        private const string PREF_DAY_COUNT_PREFIX = "Ad_DayCount_";
        private const string PREF_DAY_STAMP = "Ad_DayStamp";

        /// <summary>
        /// 游戏启动后多少秒内不展示插屏（启动保护，产品策略暂不进配置表）。
        /// </summary>
        private const float INTER_START_PROTECT = 30f;

        private float _appStartTime;
        private AdPolicy _interstitial;

        public void Configure(AdPolicy policy) { _interstitial = policy; }

        public AdPolicy InterstitialPolicy
        {
            get
            {
                if (_interstitial != null) return _interstitial;
                return AdModule.LoadInterstitialBaseline() ?? new AdPolicy(false, 0, 60);
            }
        }

        // No variant-dependent interval check here; this is the exposure eligibility boundary.
        public bool CanOfferInterstitial(AdPolicy policy) => policy.Enabled &&
            Time.realtimeSinceStartup - _appStartTime >= INTER_START_PROTECT;

        /// <summary>
        /// 记录应用启动时间，用于启动保护。在模块初始化时调用。
        /// </summary>
        public void OnAppStart()
        {
            _appStartTime = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// 判断指定类型广告当前是否允许展示（频控维度，不含 SDK 就绪状态）。
        /// 所有广告类型统一走配置表（enabled/日上限/最小间隔），启动保护仅对插屏生效。
        /// 配置未加载或行缺失时视为不允许展示。
        /// </summary>
        /// <param name="adType">广告类型。</param>
        /// <returns>是否允许展示。</returns>
        public bool CanShow(AdType adType)
        {
            if (adType == AdType.Interstitial) return CanShowInterstitial(InterstitialPolicy, out _);
            Ad cfg = AdConfigMgr.Instance.GetAd((int)adType);
            if (cfg == null || !cfg.Enabled)
            {
                return false;
            }

            // 每日上限（0 = 不限）
            if (cfg.DailyLimit > 0 && GetDailyCount(adType) >= cfg.DailyLimit)
            {
                return false;
            }

            // 最小间隔
            if (!IntervalElapsed(adType, cfg.MinIntervalSec))
            {
                return false;
            }

            return true;
        }

        public bool CanShowInterstitial(AdPolicy policy, out string reason)
        {
            reason = "disabled";
            if (!policy.Enabled) return false;
            reason = "startup_protection";
            if (!CanOfferInterstitial(policy)) return false;
            reason = "daily_limit";
            if (policy.DailyLimit > 0 && GetDailyCount(AdType.Interstitial) >= policy.DailyLimit) return false;
            reason = "interval";
            if (!IntervalElapsed(AdType.Interstitial, policy.MinIntervalSeconds)) return false;
            reason = "allowed";
            return true;
        }

        private static bool IntervalElapsed(AdType type, double interval)
        {
            var stored = PlayerPrefs.GetString(PREF_LAST_SHOW_PREFIX + type, "");
            if (!long.TryParse(stored, NumberStyles.Integer, CultureInfo.InvariantCulture, out long last)) return true;
            return (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - last) >= interval;
        }

        /// <summary>
        /// 记录一次成功展示，用于间隔与日上限统计。
        /// </summary>
        /// <param name="adType">广告类型。</param>
        public void RecordShow(AdType adType)
        {
            PlayerPrefs.SetString(PREF_LAST_SHOW_PREFIX + adType,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
            EnsureDayStamp();
            PlayerPrefs.SetInt(PREF_DAY_COUNT_PREFIX + adType, GetDailyCount(adType) + 1);
            PlayerPrefs.Save();
        }

        private static int GetDailyCount(AdType adType)
        {
            EnsureDayStamp();
            return PlayerPrefs.GetInt(PREF_DAY_COUNT_PREFIX + adType, 0);
        }

        /// <summary>
        /// 跨天时重置当日计数。
        /// </summary>
        private static void EnsureDayStamp()
        {
            string today = DateTime.Now.ToString("yyyyMMdd");
            var previousDay = PlayerPrefs.GetString(PREF_DAY_STAMP, "");
            if (previousDay != today)
            {
                PlayerPrefs.SetString(PREF_DAY_STAMP, today);
                if (previousDay.Length > 0)
                {
                    PlayerPrefs.DeleteKey(PREF_DAY_COUNT_PREFIX + AdType.Reward);
                    PlayerPrefs.DeleteKey(PREF_DAY_COUNT_PREFIX + AdType.Interstitial);
                }
            }
        }
    }
}
