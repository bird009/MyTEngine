namespace GameLogic
{
    /// <summary>
    /// 广告展示入口点位。用于埋点区分与频控策略，新增入口时在此扩展。
    /// </summary>
    public enum AdsEntrance
    {
        /// <summary>
        /// 通用兜底入口。
        /// </summary>
        Default = 0,

        /// <summary>
        /// 关卡失败结算。
        /// </summary>
        LevelFail = 1,

        /// <summary>
        /// 关卡胜利结算。
        /// </summary>
        LevelComplete = 2,

        /// <summary>
        /// 复活。
        /// </summary>
        Revive = 3,

        /// <summary>
        /// 双倍奖励。
        /// </summary>
        DoubleReward = 4,

        /// <summary>
        /// 商店/道具。
        /// </summary>
        Shop = 5,

        /// <summary>
        /// 场景切换（插屏）。
        /// </summary>
        SceneChange = 6,
    }
}
