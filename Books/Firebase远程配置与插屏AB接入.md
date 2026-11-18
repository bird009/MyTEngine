# Firebase 远程配置与插屏 A/B 首版

适用项目：TEngine / Unity 2022.3.62f3 / GameLogic 热更新。实现日期：2026-11-18。

## 当前交付边界

- 广告模块在 Firebase 独立提交之后接入。`GameApp` 先读取 Luban 插屏基线并生成策略，再交给 `AdModule`。业务调用 `ShowInterAd` 时登记曝光机会，广告回调沿用该请求的策略。

- 已实现真实 Firebase SDK 适配器、默认/Editor 模拟适配器、RemoteConfigModule、AnalyticsModule、启动协调、广告策略快照、曝光和结果事件、GM 诊断页。
- 已导入 Firebase Analytics、Remote Config **14.1.0** 及 App 依赖，EDM4U 统一为 **1.2.190**。Google Mobile Ads Unity **11.5.0** 随广告模块接入。现有 Android App ID 与广告位使用 [Google 官方测试 ID](https://developers.google.com/admob/unity/test-ads)，iOS 仍需配置相应的 App ID 与广告位。
- Unity 2022.3.62f3 Editor 编译及 Android Player 脚本编译通过（24 个程序集，包含真实 Firebase 适配器）。此前 19 项配置/广告行为测试和原有 16 项生命周期测试通过。尚未完成 IL2CPP 原生打包、真机和后台联调。
- EDM4U 强制解析成功，Gradle 7.5.1 / Android Gradle Plugin 7.4.2 的独立 Android runtime 依赖校验成功下载/解析 105 个制品，`checkReleaseAarMetadata` 通过（compileSdk 35 / Build Tools 34.0.0）；这不等同于完整 APK 构建通过。AGP 7.4.2 对 compileSdk 35 的支持范围警告仍存在，本次未改动 Unity 内置工具链。
- Android/iOS 已添加 `TENGINE_FIREBASE`，保留原有宏。Android 最低 API 24 不变，iOS 最低版本从 12.0 对齐至 15.0。
- 尚无 Firebase 平台配置文件、真实后台实验或可验证的广告收入报告。`FirebaseSettings.enableFirebase=false`、`analyticsAllowedAtStartup=false`，仍默认运行本地模式。
- 当前业务没有完整关卡结算入口，已接通统一 `ShowInterAd` 方法；具体玩法在结算时调用该方法即可。不会自动插入广告。

## 代码与职责

| 位置 | 内容 |
| --- | --- |
| `UnityProject/Assets/GameScripts/Services/Firebase` | `GameServices.Firebase` AOT 程序集、SDK 边界、进程级 FirebaseRuntime、Settings、默认后端 |
| `UnityProject/Assets/GameScripts/HotFix/GameLogic/Module/RemoteConfig` | 配置启动事务、解析校验、AdPolicy 不可变快照、业务单例、GM 页面 |
| `UnityProject/Assets/GameScripts/HotFix/GameLogic/Module/Analytics` | 自定义事件、实验曝光去重、广告机会与结果 |
| `UnityProject/Assets/Editor/Firebase` | Settings 菜单、构建前检查、iOS 默认采集开关 |
| `UnityProject/Assets/Plugins/Android/TEngineFirebase.androidlib` | Android 默认关闭 Analytics 自动采集的元数据 |
| `Tools/Tests/FirebaseIntegration` | 链接生产源码的 .NET 行为回归 |

GameModule 的 RemoteConfig / Analytics 直接返回对应 Instance，不额外缓存。两个模块由 SingletonSystem 统一释放。SDK 进程实例不在业务重启时销毁，不重置安装身份。

## 你需要在 Firebase 后台完成的内容

1. 创建测试、正式两个 Firebase 项目，分别启用 Google Analytics。记录各自 **Project ID**，不是显示名称或 App ID。
2. 在每个项目中，按实际 Android 包名、iOS Bundle ID 注册应用。下载 `google-services.json` / `GoogleService-Info.plist`。构建使用的配置必须与当前环境一致。
3. 在 Remote Config 创建以下三个参数并发布：

| 参数名 | 控制台类型 | 非实验默认值 |
| --- | --- | --- |
| `ads_interstitial_policy_v1` | JSON | 下方“非实验策略” |
| `ads_interstitial_disabled` | Boolean | `false` |
| `config_release` | String | 例如 `20261118_01`，每次发布人工更新 |

非实验策略（收到这个值后使用普通 60 秒策略，不登记实验曝光）：

```json
{"schema":1,"policy_revision":"inter_default_v1","experiment_key":"","variant":"","min_interval_sec":60}
```

4. 创建 Remote Config A/B 实验，名称建议 `inter_gap_001`，选择支持该功能的应用版本。仅覆盖小比例用户，例如 10%，A/B 组均分。**在实验的 baseline 和 variant 中都明确填值**，不把普通默认值直接当实验 A 组。

A 组（baseline）的 `ads_interstitial_policy_v1`：

```json
{"schema":1,"policy_revision":"inter_gap_001_a","experiment_key":"inter_gap_001","variant":"a","min_interval_sec":60}
```

B 组的同一参数：

```json
{"schema":1,"policy_revision":"inter_gap_001_b","experiment_key":"inter_gap_001","variant":"b","min_interval_sec":90}
```

5. 实验 activation event 填 `inter_gap_001_exposed`。它用于限制实际曝光人群，**不要再把它选为该实验的目标指标**。可先用测试包触发事件，让控制台识别名称。
6. 广告收入链路验证后，主要指标可选 Estimated ad revenue；辅助观察留存。没有接 Crashlytics 时不要把无崩溃指标当成已接通。
7. 关联 AdMob 应用与对应 Firebase/GA4 项目，确认真实广告收入进入报告。首版没有手工重报 AdMob 的 `ad_impression` / 收入，避免和自动采集重复。原 `OnAdPaid` 日志本身不证明收入已进入后台。
8. 用官方测试设备功能验证 A/B；开发日志中的 `variant` 是我们配置的诊断标签，后台归属仍以 Firebase 为准。正式开始前核对设备收到的数值与组别。

不要在同一参数上同时运行多个实验、个性化或其他冲突覆盖。将 B 从 90 秒改成 120 秒需要新实验版本及客户端允许的协议；首版刻意只接受 `inter_gap_001` 的 a=60 / b=90。

## Unity 接入步骤

1. **SDK 已导入，不需要重复导入。** 官方来源：https://developers.google.com/unity/packages 。后续升级应成套升级 Analytics、Remote Config 及 App 依赖，并仅保留一份 EDM4U 实现。
2. 将本次环境的配置文件放到 `UnityProject/Assets/google-services.json` 和/或 `UnityProject/Assets/GoogleService-Info.plist`。不用提供服务账号密钥。
3. 菜单 **TEngine → Firebase → Create or Select Settings**，选中已经生成的 `Assets/Resources/FirebaseSettings.asset`。
4. 设置 `enableFirebase=true`、`environment`（测试/正式标签）、`expectedProjectId`。填写的 Project ID 会在运行时与 SDK 核对。`environment` 标签本身不会切换 Firebase 项目，真正的环境由配置文件决定。
5. Android/iOS 的 `TENGINE_FIREBASE` **已启用**。若其他分支需要恢复，可执行 **TEngine → Firebase → Enable SDK Integration**，该菜单会检查 SDK 程序集、追加宏并保证 iOS 最低 15.0，不会开启运行时 Firebase 或采集。若 Settings 开启 Firebase 而宏/配置文件缺失，构建检查会报错。
6. 保持 `GameServices.Firebase` 和 Firebase SDK 在 AOT 主包，不加入 HybridCLR 热更新程序集列表。`GameLogic` 已引用接入层；`link.xml` 保留接入层。导入 SDK 后按现有项目流程重新生成 HybridCLR link、桥接及所需 AOT 元数据，再打 IL2CPP 真机包。
7. 更换配置文件后执行 **TEngine → Firebase → Resolve Android Dependencies**，再按下文验收。iOS Firebase 使用 Swift Package Manager，现有 AdMob 保留 CocoaPods；在 Mac 上完成 Xcode 依赖解析、Info.plist 检查与真机验证。当前代码未创建后台项目或发布实验。

### 已对齐的依赖与验证

| 层 | 当前版本 |
| --- | --- |
| Firebase Unity App / Analytics / Remote Config | 14.1.0 |
| EDM4U | 1.2.190，已删除 1.2.186 实现与旧清单 |
| Firebase Android Analytics / Remote Config / Common | 24.0.0 / 24.0.1 / 23.0.0 |
| Google Play Services Base | 18.11.1 |
| Google Mobile Ads Unity / Android / UMP Android | 11.5.0 / 25.4.0 / 4.0.0，保持现有声明 |
| Firebase Apple SDK | 13.1.1，官方包声明为 Swift Package |
| Google Mobile Ads iOS | 现有 CocoaPods `~> 13.9`，未改为另一版本或另一管理方式 |

Android 的 `mainTemplate.gradle`、`settingsTemplate.gradle` 和 `AndroidResolverDependencies.xml` 已由解析器更新，保留广告依赖并加入 Firebase 与本地 Maven 仓库。不要再手工塞入第二套 Firebase AAR 或混用另一版本的 Firebase Unity DLL。

Firebase 14 的最低 Unity 版本为 2022.3、Android 最低 API 24，iOS 依赖改用 Swift Packages；Unity 原生包使用 Xcode 26.4 构建。Firebase Apple 13.1.1 的 Package.swift 最低 iOS 15.0、Swift 6.2.3 / Xcode 26.2；Mac 验收建议使用 Xcode 26.4 或更新版本，同时核验 Unity 导出兼容性。依据：[Firebase Unity 发布说明](https://firebase.google.com/support/release-notes/unity)、[Apple 13.1.1 包声明](https://github.com/firebase/firebase-ios-sdk/blob/13.1.1/Package.swift)。本机 Windows 尚未完成 SPM/CocoaPods/Xcode 联合构建。

本机验证日志与导入前备份位于 `UnityProject/Library/CodexFirebaseSetup`（不提交 Git）：`import-analytics.log`、`import-remote.log`、`enable-final.log`、`resolve-android.log`、`compile-android.log`、`gradle-check-final.log`。`gradle-check/resolved-modules.txt` 保存本机实际解析版本。批处理可使用 `-executeMethod FirebaseBuildSetup.CompileAndroidScripts` 复验 Android Player 脚本；这会实际编译 Editor 模式排除的 SDK 后端。

本次官方包 SHA-256：

- `FirebaseAnalytics_14.1.0.unitypackage`：`baa23ef3cff6c8a29a78fe250bd1ca600d9c9c3b4228e3f3674527024a044992`
- `FirebaseRemoteConfig_14.1.0.unitypackage`：`a43cbfce2b160888b37f6e52195f5c5947245aedddadccfa40fafc97aa4a02ba`

### 采集许可

`analyticsAllowedAtStartup` 默认 false。它只适合由产品启动策略明确保证“进入 GameApp 前已允许采集”的场景，不是让用户默认同意的开关。

已有许可流程应在配置启动前调用：

```csharp
GameServices.FirebaseRuntime.SetAnalyticsConsent(true); // 真实许可结果
```

撤回时调用 false，会停止本模块发送并关闭 SDK Analytics 收集。未允许时不缓存待补发的业务事件，实验载荷退回普通基础策略。若许可发生在本次配置快照已固定之后，首版不会中途启用实验，下一次业务启动再应用。

Android/iOS 加入了默认关闭采集设置，但 Firebase 的运行时 collection 设置会持久化。产品需要在自己的许可流程中正确处理恢复和撤回；本次没有实现 CMP/隐私弹窗。Analytics 许可和广告 SDK 初始化许可是独立决策，不能用 ATT 是否允许代替所有许可状态。现有广告初始化入口保持原项目行为。

## 启动、生效和失败行为

`GameApp` 先创建统计/配置模块，再创建广告模块，主界面加载成功后关闭 Launcher。

- 启动默认有 3000ms 的初始化与网络等待预算。已开始的本地 ActivateAsync 要等待完成，避免它在业务开放后才改变 Firebase 归属；这不是整个启动的绝对硬超时。
- 初始化后先激活上一轮已下载候选，再尝试 Fetch；成功后在启动窗口内激活并生成快照。
- 超时/拉取失败使用 SDK 已激活缓存；SDK 不可用用 Luban。不得把缓存或默认值日志当作本次成功联网。
- 迟到 fetch 只落 SDK 的 fetched 缓存，下一次启动再激活。旧会话回调不访问 GameModule 属性。
- 启动事务使用进程级门锁；释放发生在本地激活期间时，新会话不能并行激活。
- 读取的是 SDK 激活后的值，JSON 校验失败会回退，但不能撤销 SDK 已完成的实验登记。发布前验证是首要防线；异常期间不发正常实验曝光，记录 `config_invalid`，必要时停止实验并处理污染数据。
- 首版只支持启动激活，不监听实时更新、不做在线强制切组。默认最小 Fetch 间隔 12 小时；测试项目可改为 60 秒。停止实验或关闭插屏，要等客户端再次成功拉取/激活。离线玩家不会立即更新。
- 独立关闭开关只关闭插屏，不能因策略 JSON 非法而丢失；本地 Luban 未启用广告时远程策略也不能强行开启。
- 策略字段限制：schema=1、interval 为 10–600 的有限数值、revision 不超过 64 字符，拒绝缺失字段、重复 JSON 键、未知实验和错误组别/数值组合。

## 业务怎么使用

关卡结算等有效机会直接调用：

```csharp
GameModule.Ad.ShowInterAd(AdsEntrance.LevelComplete, shown => { /* 继续结算 */ });
```

**不要先按 `CanShow(Interstitial)` 返回值过滤再调用**，否则 60/90 秒频控会影响进入曝光统计的资格。`CanShow` 仍可用于纯 UI 状态查询，不产生事件。

曝光在广告已启用、启动保护结束的有效业务机会发生，并在间隔/日上限/SDK ready 判断前记录。同一业务运行中每个实验只发一次曝光，后续机会和结果分别记录。重复 SDK 回调不会完成下一次广告请求；模块释放后的回调不复活实例。

`interstitial_result` 的 `completed` 代表现有广告服务回调报告完成，不代替真实 impression 或收入事件。收入由经验证的官方关联链路负责。

通用业务埋点：

```csharp
GameModule.Analytics.TrackEvent("level_complete", new Dictionary<string, object>
{
    { "level", levelId },
    { "duration_sec", durationSeconds }
});
```

支持 string、int/long、float/double、bool；名称为英文开头、字母数字下划线且最长 40 字符，自定义参数最多 24 个，另外添加 `is_test`。拒绝非法值和保留前缀；字符串截断到 100 字符；不重报系统自动事件。没有采集许可或模块已释放时返回 false。

## Editor 与 GM

Editor 始终走 local-simulation，不上传 Firebase；将 A/B JSON 填入 Settings 的 `editorPolicyJson` 后重新进入 Play。

`GM/远程配置` 显示环境、后端、启动状态、采集许可、策略来源、实际间隔、诊断组别、曝光数及错误。仅本地模式提供模拟关卡结算机会按钮。`GM/广告` 原来的展示按钮使用 diagnosticOnly，不触发实验曝光。

无参数默认使用 Luban；Editor 模拟开关支持测试插屏关闭。首版没有运行时强制切组按钮，以免与会话快照冲突。

## 测试与已知范围

```powershell
dotnet run --project Tools/Tests/FirebaseIntegration/FirebaseIntegration.csproj --configuration Release --disable-build-servers
dotnet run --project Tools/Tests/FirebaseIntegration/FirebaseIntegration.csproj --configuration Release --disable-build-servers -p:DefineConstants=UNITY_EDITOR
dotnet run --project Tools/Tests/BusinessLifecycle/BusinessLifecycle.csproj --configuration Release --disable-build-servers
```

行为测试链接真实解析器、启动事务、业务模块、Singleton、广告调度与入口；Unity API、广告 SDK、Firebase 网络后端由测试桩代替。覆盖超时、取消、非法载荷、曝光/查询分离、默认值身份、SDK 异常、重复回调、释放竞态、跨启动间隔和每日计数。

广告间隔持久化改用 `Ad_LastShowUtcV1_*`，不再把一次启动内的 realtimeSinceStartup 持久化后跨启动比较。旧键不删除、不用于新比较；上线首次升级重新建立间隔记录。墙上时钟仍可能被玩家修改，这不是服务端防作弊系统。

上线前必须真机验收：断网首次启动、缓存启动、A/B 两组实际值、采集拒绝/撤回、超时迟到、重启、非法配置、关闭实验、收入无重复，以及包名/Project ID/构建环境一致。Firebase DebugView 和控制台报表需要实际设备与项目配置才能验收。

## 官方依据

- Unity 接入：https://firebase.google.com/docs/unity/setup
- Unity Remote Config：https://firebase.google.com/docs/remote-config/unity/get-started
- A/B 与激活事件：https://firebase.google.com/docs/ab-testing/abtest-config
- 计费与 10 万次每日免费 Fetch：https://firebase.google.com/docs/remote-config/pricing
- AdMob 关联：https://support.google.com/firebase/answer/6387949
