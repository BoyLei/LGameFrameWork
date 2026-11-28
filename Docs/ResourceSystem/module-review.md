# Review

## 结构 Review
- Game 和 Samples 依赖 Runtime；Runtime 只依赖 Addressables/ResourceManager，未引用 Editor/Game。
- Backend 负责具体 Unity handle，Service 负责所有权和服务生命周期；Scope 负责页面生命周期。
- 资源共享直接复用 Addressables 引用计数，避免另建不透明强缓存。
- 原 AppMain GUID 保留；未修改场景、URP 和版本配置。

## 代码 Review
- 成功 Load 交付所有权；失败/Dispose 后迟到结果均释放 operation；重复 Dispose 不重复释放。
- Prefab 实例通过 ReleaseInstance(handle) 清理，不与 LoadAsset handle 释放混用。
- 初始化 handle 使用 autoReleaseHandle=false 和 finally 释放；初始化 Task 在各后端实例间共享，避免同一全局初始化操作被多次释放。Play 会话注册时重置静态任务。
- 下载大小、依赖下载 handle 通过 using 在成功或异常后释放。
- Scope 和 Service 在一份资源释放异常时仍继续回收其他资源，最终汇总异常。
- 主线程约束覆盖服务调用、资源释放、Scope 管理；未使用 ConfigureAwait(false)、后台 Unity API 或阻塞等待生产异步 API。
- 对照 Addressables 1.21.21 包源码核对 InitializeAsync(bool)、LoadAssetAsync、InstantiateAsync、ReleaseInstance(handle)、DownloadDependenciesAsync，以及 Editor Settings API 和程序集名。
- 测试环境增加 owner-thread SynchronizationContext 泵，避免并发初始化测试在 .NET thread pool 续跑；生产线程约束保持严格。

## 限制
没有 Unity Editor，无法评估实际 UPM 导入、UnityEngine null 语义、打包剥离或实际释放实例的行为。云端测试中的 Unity 类型和后端是替身，不能代替本地引擎验收。
Scope 不取消下载；服务关闭也不会立即终止底层请求。所有者应捕获迟到加载的异常。初始化失败后底层 Addressables 未必支持直接恢复，应修复配置后重启 Play。
第一版没有 Scene API、catalog 更新、CDN、进度/取消、缓存淘汰或对象池，留到确认需求后扩展。
