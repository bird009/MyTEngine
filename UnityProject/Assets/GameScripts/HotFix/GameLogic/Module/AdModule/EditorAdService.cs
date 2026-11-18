#if UNITY_EDITOR || !AD_SDK_MAX
using System;
using TEngine;

namespace GameLogic
{
    /// <summary>
    /// 无 SDK 环境的模拟广告服务。用于 Editor 与未接入 SDK 的构建，回调语义与真机一致。
    /// </summary>
    public class EditorAdService : IAdService
    {
        public void Init()
        {
            Log.Info("[Ad] EditorAdService 初始化（模拟桩）。");
        }

        public bool IsAdReady(AdType adType)
        {
            return true;
        }

        public void ShowRewardAd(Action<bool, int> callback)
        {
            Log.Info("[Ad] 模拟展示激励视频。");
            callback?.Invoke(true, 1);
        }

        public void ShowInterAd(Action<bool> callback)
        {
            Log.Info("[Ad] 模拟展示插屏。");
            callback?.Invoke(true);
        }
    }
}
#endif
