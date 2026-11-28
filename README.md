# LGameFramework

Unity 2022.3 游戏框架第一版，资源模块基于 Addressables 1.21.21。
现有工程记录为 **2022.3.62f3c1（中国版）**；目标 API 为用户指定的 2022.3.62f1。
没有改写 ProjectVersion.txt。请优先使用现有工程对应的编辑器打开。

## 目录

```text
Assets/
├── LGameFramework/
│   ├── Runtime/
│   │   ├── Core/                # 框架公共基础
│   │   └── Resource/            # 资源接口、服务、句柄、作用域、Addressables 后端
│   ├── Editor/Resource/         # 资源编辑器工具（独立 Editor 程序集）
│   ├── Samples/ResourceDemo/    # 可选示例（独立程序集）
│   └── Tests/EditMode/          # 资源生命周期测试
├── Game/
│   ├── Runtime/Bootstrap/      # AppMain，装配框架服务
│   ├── Runtime/Features/       # 以后按 Login、Battle 等业务功能扩展
│   ├── Editor/                 # 游戏专属编辑器工具，Unity 默认 Editor 编译边界
│   └── Content/                # Prefabs、UI、Audio、Configs
├── ThirdParty/                 # 外部插件及许可证
├── Scenes/                     # 保留现有场景
└── Settings/                   # 保留现有 URP 设置
Docs/ResourceSystem/             # 模块计划、设计、Review、验证、下一轮
Tools/                          # 云端检查工具
```

框架借鉴 [UnityGameFramework](https://github.com/EllanJiang/UnityGameFramework/tree/master/Scripts) 的 Runtime/Editor 和职责模块划分，以及 [QFramework](https://github.com/liangxiegame/QFramework/tree/master/QFramework.Unity2018%2B/Assets) 的框架、工具和测试隔离。没有引入其代码。
依赖方向：Game/Samples → Framework.Runtime → Addressables。Framework.Runtime 不依赖游戏业务或 UnityEditor。
UI、网络、流程、对象池等模块未来按职责加入 Runtime，第一版不放空实现。

## 本地快速体验

1. 拉取本分支，等待 Unity Package Manager 导入 Addressables 及 Scriptable Build Pipeline。
2. 菜单 **LGameFramework → Resources → Create Demo Addressable** 创建 cube Prefab、默认 Addressables Settings 和 `demo/cube` 地址。重复执行保留已有 Prefab/地址设置。
3. 打开 SampleScene，新建空物体，挂载 `ResourceDemo`，进入 Play Mode，应出现一个 Cube；销毁示例物体后，实例应被释放。
4. 首次在 Groups 窗口使用 **Use Asset Database** 模式验证编辑器逻辑。验证真实 Bundle 时执行 **Build → New Build → Default Build Script**，再切换 **Use Existing Build**；Player 构建也需要有效的 Addressables 内容构建。
5. 在 Test Runner 的 **EditMode** 中运行 `ResourceServiceTests`。

`AppMain` 已迁移至 Game/Runtime/Bootstrap，保留原脚本 GUID。它在 Awake 创建服务、OnDestroy 释放，按需初始化；若需要业务入口，在场景物体上挂载 AppMain。ResourceDemo 使用自己的服务，便于独立体验。

## API

所有调用和释放必须发生在 Unity 主线程。内部使用 Task 和 Addressables Completed 回调，未依赖 handle.Task。

```csharp
using LGameFramework.Resource;
using UnityEngine;

// resources 来自 AppMain.Resources；scope 与页面/关卡的生命周期绑定。
var scope = new ResourceScope();
var prefab = scope.Own(await resources.LoadAsync<GameObject>("characters/hero"));
Debug.Log(prefab.Asset.name);

var instance = scope.Own(await resources.InstantiateAsync("characters/hero", parent));
instance.Asset.transform.localPosition = Vector3.zero;

long bytes = await resources.GetDownloadSizeAsync("chapter-1");
await resources.DownloadDependenciesAsync("chapter-1");

// 页面退出时执行（不要放在上述异步初始化函数末尾提前释放）：
scope.Dispose();
```

- 每次加载返回独立句柄；底层资源与依赖共享由 Addressables 自身引用计数负责。没有额外永久缓存。
- 单独释放用 `handle.Dispose()`；重复调用安全，释放后访问 Asset 抛出 ObjectDisposedException。
- Instantiate 返回的句柄释放时走 ReleaseInstance；不要仅调用 Object.Destroy 或自行 Addressables.Release。
- Load 返回的 Prefab 如果通过 Object.Instantiate 手工实例化，必须让加载句柄活到所有克隆销毁之后；推荐使用本服务 InstantiateAsync。
- ResourceScope.Own 必须接收加载结果。作用域已关闭时，会立即释放迟到的结果并抛出 ObjectDisposedException。
- 服务 Dispose 会回收当前仍持有的句柄；进行中的请求在完成/失败后回收，尚未返回的调用可能抛出 ObjectDisposedException。底层网络请求不被中断。
- 查询/预下载操作完成后立即释放临时 handle；预下载的 Bundle 留在磁盘缓存，不承诺常驻内存。
- 初始化在同一 Play 会话内共享。服务层失败后允许再次调用，但底层 Addressables 的失败恢复能力由包本身决定；修复无效 Settings/catalog 后建议重启 Play。
- 异常向调用方传播，业务必须 await 并捕获异常。第一版不提供取消、进度回调、场景加载、catalog 更新、远端 CDN 配置、对象池或业务缓存策略。

## 云端验证

不需要 Unity 或许可证。安装 .NET 8 SDK 后：

```bash
python3 Tools/validate_framework.py
python3 Tools/test_resource_lifecycle.py
```

第二项使用仓库中同一组 NUnit 测试与实际 ResourceService/Handle/Scope 源码，在临时目录中以类型替身隔离 UnityEngine 和 Addressables 后端；验证生命周期逻辑，**不证明 Unity 编译、Bundle、实际 Prefab 销毁或平台构建通过**。首次执行需要访问 NuGet。真实引擎验收见 [模块测试记录](Docs/ResourceSystem/module-tests.md)。
