# Firebase 首版行为回归

运行 `dotnet run --project Tools/Tests/FirebaseIntegration/FirebaseIntegration.csproj --configuration Release --disable-build-servers`。

另用 `-p:DefineConstants=UNITY_EDITOR` 检查 Editor 分支与 GM 窗口。依赖 Unity 已解析的 Newtonsoft.Json 3.2.1（Library/PackageCache），不额外恢复另一版本。

测试链接生产的 GameServices 接口和本地后端、配置启动与策略模块、统计模块及 Singleton。工作区存在广告模块时，也会链接 AdModule 和 AdScheduler 并执行插屏接线回归。Unity 平台 API、广告原生服务、Luban 数据源和 Firebase 网络由测试桩提供。

测试覆盖 JSON 原子拒绝、默认值不伪装 control、许可与关闭开关、缓存/超时/取消/激活互斥、曝光去重与频控顺序、GM/查询不曝光、过期与重复回调、SDK 异常恢复、自定义事件校验、跨启动 UTC 间隔及每日计数。

真实 Firebase 14.1.0 托管 DLL 的单独编译核对只验证适配器 API，不等于完整 Unity/IL2CPP 原生构建或后台统计已验证。详见 Books/Firebase远程配置与插屏AB接入.md。
