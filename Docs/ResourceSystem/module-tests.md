# 验证记录

## 已运行（云端）
- `python3 Tools/validate_framework.py`：通过。检查 Unity meta 完整性/唯一 GUID、5 个程序集及引用、Editor 隔离、运行时依赖方向、包版本固定和 AppMain GUID。
- `LGAME_DOTNET=/tmp/lgame-dotnet/dotnet python3 Tools/test_resource_lifecycle.py`：16/16 NUnit 测试通过，使用 .NET SDK 8.0.428。
- `git diff --check`：通过。
- Addressables API/程序集与 1.21.21 官方源码逐项核对；依赖锁根据官方 registry 中 Addressables/SBP 元数据添加，未在 Unity UPM 中重新解析。

## 单元测试覆盖
重复释放/释放后访问、独立加载所有权、失败加载清理、加载期间关闭服务、共享初始化、初始化失败后服务层重试、初始化期间关闭服务、Scope 关闭后迟到句柄、服务关闭释放实例、下载成功/失败、大小查询成功/失败、非法地址、释放异常继续清理、后台线程释放被拒绝且保留所有权。

云端测试复用实际 Service/Handle/Scope 源码，替换 Unity 类型和 Addressables backend。它证明所有权策略的 C# 行为，未证明真实 Unity 集成。

## 本地 Unity 集成验收（未运行）
1. 使用工程匹配的 Unity 2022.3 打开，等待 UPM，确认无编译错误；记录包锁是否被中国版 registry 调整。
2. Test Runner / EditMode 运行 ResourceServiceTests；预期 16/16。云端替身验证不取代这一步。
3. 菜单创建 demo/cube；挂 ResourceDemo，Use Asset Database 模式 Play，验证 Cube 出现、销毁父对象/退出 Play 后实例回收。
4. 两次 Instantiate 相同地址，仅释放其中一个，另一个仍可使用；移除服务所有者后余下实例回收。
5. 使用无效地址确认异常日志；快速销毁加载所有者，确认无孤立实例、无 invalid handle 或重复释放警告。
6. 创建 label，配置远端 Profile 和测试服务器后验证下载大小、预下载及离线失败；纯本地 Bundle 可返回 0，不代表接口失败。
7. Build Addressables content，Use Existing Build 重复示例；再做目标平台 Player 验证。
8. Domain Reload 开启/关闭分别连续进入 Play 两次，检查共享初始化任务不跨 Play 会话遗留。

## 验收状态
- [x] 第一版目录、依赖隔离与 meta 完成。
- [x] 异步加载/实例化/预下载、所有权和失败清理 API 完成。
- [x] 可体验示例、文档、云端生命周期测试完成。
- [ ] 真实 Unity 编译/导入、EditMode、PlayMode、Bundle/Player 集成验证通过（待本地执行）。
