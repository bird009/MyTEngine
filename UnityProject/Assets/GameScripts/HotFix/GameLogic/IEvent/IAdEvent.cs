using TEngine;

namespace GameLogic
{
    /// <summary>
    /// 广告相关事件。由 AdModule 通过 GameEvent.Get&lt;IAdEvent&gt;() 广播，业务侧监听。
    /// </summary>
    [EventInterface(EEventGroup.GroupLogic)]
    public interface IAdEvent
    {
        /// <summary>
        /// 激励视频完整观看，可发放奖励。
        /// </summary>
        /// <param name="entrance">触发广告的入口点位。</param>
        /// <param name="rewardAmount">奖励数量。</param>
        void OnAdRewardCompleted(AdsEntrance entrance, int rewardAmount);

        /// <summary>
        /// 广告展示成功（激励/插屏均可触发，用于埋点）。
        /// </summary>
        /// <param name="adType">广告类型。</param>
        /// <param name="entrance">触发广告的入口点位。</param>
        void OnAdShown(AdType adType, AdsEntrance entrance);

        /// <summary>
        /// 广告关闭（展示结束，无论成功与否）。
        /// </summary>
        /// <param name="adType">广告类型。</param>
        /// <param name="entrance">触发广告的入口点位。</param>
        void OnAdClosed(AdType adType, AdsEntrance entrance);
    }
}
