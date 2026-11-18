using GameConfig;
using GameConfig.ad;

namespace GameLogic
{
    /// <summary>
    /// 广告配置管理器。封装 TbAd 的查询逻辑，避免业务代码直接散落 ConfigSystem 调用。
    /// </summary>
    public class AdConfigMgr
    {
        private static AdConfigMgr _instance;
        public static AdConfigMgr Instance => _instance ??= new AdConfigMgr();

        /// <summary>
        /// 获取指定广告类型的配置，找不到返回 null。
        /// </summary>
        /// <param name="id">广告类型标识，与 AdType 枚举值一致（1=激励 2=插屏）。</param>
        public Ad GetAd(int id)
        {
            return ConfigSystem.Instance.Tables.TbAd.GetOrDefault(id);
        }
    }
}
