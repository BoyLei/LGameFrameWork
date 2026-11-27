# 第一版资源模块计划

## Goal
在现有项目中建立分层游戏框架，交付基于 Addressables 的显式资源所有权 API、示例及可重复测试。

## Module / 现状
基线 ce55431：只有空 AppMain，没有资源 API、Addressables 依赖或 asmdef。
调用链：业务/示例 → IResourceService → ResourceService（所有权）→ IResourceBackend → Addressables（实际引用计数）。
框架负责初始化、异步加载、实例化、下载大小/依赖预下载，以及成功/失败/关闭时的句柄释放；不实现 UI、网络、热更新、对象池、场景切换或额外缓存。

## Constraints
- 目标 Unity 2022.3；用户指定 2022.3.62f1，现有 ProjectVersion 为 2022.3.62f3c1，保留现状。
- Addressables 固定 1.21.21（官方 registry 元数据最低 Unity 2019.4），使用完成回调桥接 Task，避免依赖 AsyncOperationHandle.Task 的 WebGL 限制。
- 所有公开调用、await continuation、Dispose 都在 Unity 主线程；不提供后台线程安全。
- 一次 Load 对应一个 handle；Addressables 内部共享底层资源，框架不增加永久缓存。
- 不在云端安装 Unity，静态检查与本地 Unity 执行结果必须区分。

## Tasks / Acceptance
1. 分层目录、程序集边界、依赖和 meta：可独立检查 JSON、GUID、依赖图。
2. 资源所有权模块：单元测试验证重复释放、失败回收、初始化重试、关闭期间加载、作用域释放。
3. Addressables 后端、编辑器样例创建工具与使用指南：对照包源码核对 API；本地创建样例后 Play Mode 验证加载/销毁。
4. 完整结构/边界 Review，汇总静态检查、单元测试与本地验收项。

## 目录参考
查询时间 2026-11-27（GitHub 动态 star 数仅用于选型）：
- UnityGameFramework（约 2.6k stars）：Scripts/Runtime 按 Resource、UI、Scene 等职责划分；Scripts/Editor 独立程序集。
  https://github.com/EllanJiang/UnityGameFramework/tree/master/Scripts
- QFramework（约 5.6k stars）：框架与 Toolkits 分开，Assets/QFrameworkTests 单独放测试。
  https://github.com/liangxiegame/QFramework/tree/master/QFramework.Unity2018%2B/Assets
仅借鉴目录边界，不引入上游实现或依赖。第一版只实做资源模块，其他业务目录不生成空 Manager。
