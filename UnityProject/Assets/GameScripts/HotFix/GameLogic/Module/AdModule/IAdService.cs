using System;

namespace GameLogic
{
    /// <summary>
    /// 广告 SDK 服务抽象。实现类封装具体聚合平台（当前 AdMob；MaxAdService 休眠待 MAX 账号开放），屏蔽平台差异。
    /// </summary>
    public interface IAdService
    {
        /// <summary>
        /// 初始化 SDK 并预加载广告。必须在隐私合规通过后调用（国内：隐私弹窗同意；海外：ATT 弹窗）。
        /// </summary>
        void Init();

        /// <summary>
        /// 指定类型广告是否已就绪（SDK 缓存就绪状态，不含频控判断）。
        /// </summary>
        /// <param name="adType">广告类型。</param>
        /// <returns>是否就绪。</returns>
        bool IsAdReady(AdType adType);

        /// <summary>
        /// 展示激励视频。
        /// </summary>
        /// <param name="callback">回调参数：是否完整观看（可发奖）、奖励数量。</param>
        void ShowRewardAd(Action<bool, int> callback);

        /// <summary>
        /// 展示插屏。
        /// </summary>
        /// <param name="callback">回调参数：是否完整展示。</param>
        void ShowInterAd(Action<bool> callback);
    }
}
