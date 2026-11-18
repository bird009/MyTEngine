#if DEVELOPMENT_BUILD || UNITY_EDITOR
using TEngine;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// 广告调试窗口（GM 控制台页签，`~` 键呼出）。
    /// Development Build 与 Editor 激活，正式包随 Debugger/IMGUI 自动剔除。
    /// 通过 GameModule.Debugger.RegisterDebuggerWindow("GM/广告", ...) 注册。
    /// </summary>
    public sealed class AdDebuggerWindow : IDebuggerWindow
    {
        private string _status = "就绪（点按钮测试广告）";

        public void Initialize(params object[] args)
        {
        }

        public void Shutdown()
        {
        }

        public void OnEnter()
        {
        }

        public void OnLeave()
        {
        }

        public void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }

        public void OnDraw()
        {
            GUILayout.BeginVertical();

            // 就绪态实时反映（IMGUI 每帧重绘，无需轮询）
            GUILayout.Label($"激励视频：{(GameModule.Ad.CanShow(AdType.Reward) ? "[就绪]" : "[无]")}");
            GUILayout.Label($"插屏：{(GameModule.Ad.CanShow(AdType.Interstitial) ? "[就绪]" : "[无]")}");
            GUILayout.Label($"GM 开关：{(GameModule.Ad.AdsDisabled ? "已关广告（激励直接发奖）" : "广告开启")}");

            GUILayout.Space(12);

            if (GUILayout.Button("展示激励视频", GUILayout.Height(40)))
            {
                GameModule.Ad.ShowRewardAd(AdsEntrance.Default, (success, amount) =>
                    _status = success ? $"激励完成，发奖 x{amount}" : "激励未完成（拦截/中途退出）");
            }

            if (GUILayout.Button("展示插屏", GUILayout.Height(40)))
            {
                GameModule.Ad.ShowInterAd(AdsEntrance.Default, shown =>
                    _status = shown ? "插屏展示完成" : "插屏跳过（频控/未就绪）", diagnosticOnly: true);
            }

            if (GUILayout.Button("切换 GM 广告开关", GUILayout.Height(40)))
            {
                GameModule.Ad.AdsDisabled = !GameModule.Ad.AdsDisabled;
                _status = $"GM 广告开关 → {(GameModule.Ad.AdsDisabled ? "关" : "开")}";
            }

            if (GUILayout.Button("初始化 SDK", GUILayout.Height(40)))
            {
                GameModule.Ad.InitAdSdk();
                _status = "已触发 InitAdSdk（幂等）";
            }

            GUILayout.Space(12);
            GUILayout.Label("最近结果：");
            GUILayout.Label(_status);

            GUILayout.EndVertical();
        }
    }
}
#endif
