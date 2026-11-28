# 本轮修改

## 完成任务
1. 框架分层与程序集：完成。建立 Framework Runtime/Editor/Samples/EditModeTests、Game Runtime/Content/Editor、ThirdParty。保留现有场景与 URP 配置。
2. 所有权模块：完成。提供 IResourceService、ResourceService、ResourceHandle、ResourceScope；独立加载所有权、幂等释放、失败清理、服务关闭时处理迟到结果、主线程约束。
3. Addressables 后端及示例：完成。固定 1.21.21 和 SBP 1.21.23；初始化跨服务共享，完成回调转 Task；预加载资源、实例化与销毁、下载大小查询与依赖预下载；一键创建 demo/cube 的编辑器工具与 ResourceDemo。
4. Review、测试及文档：完成云端范围；真实引擎验收待本地执行。

AppMain 保留原 GUID 和类名迁入 Game/Runtime/Bootstrap；创建并公开资源服务，OnDestroy 回收。清理迁移后空的 Scripts 文件夹 meta。
每个新增 Assets 目录/文件都有 meta。包锁新增条目基于官方 registry 元数据，保留原有依赖版本和 registry 设置；实际 UPM 导入仍须本地验证。

## 影响范围
运行时代码新增两个生产程序集（Framework、Game），示例及 Editor/Tests 分开编译。旧脚本原来属于 Assembly-CSharp，迁移到新程序集后，未来使用反射全限定程序集名或 SerializeReference 时应使用新程序集名；当前基线没有这类引用。
没有场景内容改动，也未自动把 Bootstrap 或 Demo 挂入场景。首次打开需要 UPM 下载依赖。
