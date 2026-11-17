# 业务模块生命周期回归

需要 .NET 8 SDK 或更高版本，无外部测试包依赖。从仓库根目录执行：

```powershell
dotnet run --project Tools/Tests/BusinessLifecycle/BusinessLifecycle.csproj --configuration Release --disable-build-servers
dotnet run --project Tools/Tests/BusinessLifecycle/BusinessLifecycle.csproj --configuration Release --disable-build-servers -p:DefineConstants=UNITY_EDITOR
```

测试直接链接工作区的 `GameModule`、`GameApp`、`Singleton<T>`、`SingletonBehaviour<T>`、`SingletonSystem`、`ModuleSystem` 与 `UpdateDriver` 源码。Unity API、UI、广告等外部依赖使用测试桩；保留了用于复现旧版延迟订阅问题的确定性 UniTask 测试桩。主要用例使用已提交的 UI 入口，工作区存在广告入口时额外验证其缓存行为，因此测试不依赖未提交的广告集成。

覆盖单独释放后重新获取、旧引用重复释放、异常清理、批量关闭、业务重建、销毁通知、更新订阅取消、释放重入、关闭期间禁止新建，以及 Restart 是否经过统一关闭入口。

业务层约定：

- `GameModule.UI` 直接访问所属单例，不另存实例缓存；其他业务入口也应采用该方式。
- 单个模块调用自身 `Release()`；模块在 `OnRelease()` 里取消外部订阅、计时器、异步工作并释放资源。
- 整体关闭调用 `GameModule.Shutdown()`，它释放业务单例并清空框架访问缓存，不关闭框架模块。
- `GameApp.Release()` 和 `SingletonSystem.Restart()` 统一调用该入口。
- `SingletonSystem.Release(ISingleton)` 仅取消登记，供单例基类调用。
- 批量释放期间允许访问尚存活的业务单例，但禁止创建新单例，避免产生未被释放快照覆盖的对象。
- 更新驱动立即登记回调，不再延后一帧登记；事件派发中新增的监听从后续派发起生效。

验证边界：这些测试验证 C# 对象身份、释放和委托连接，不等同于 Unity Editor/Player 的真实生命周期验收。没有模拟完整 Unity 对象空值语义、Awake、场景重载或广告 SDK 原生回调；Restart 测试仅验证关闭路由和缓存清理，不证明场景重启完整可用。未修改既有的业务更新列表遍历策略，仍应避免在 `OnUpdate()` 遍历期间直接创建/释放参与同一更新列表的单例。
