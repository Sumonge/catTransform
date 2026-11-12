# catTransform v0.1 开发进度（历史记录）

本文件保留 v0.1 阶段的验证记录。v0.2 已于 2026-11-12 合并到主分支，项目负责人确认系统验收完成；当前版本状态见 [v0.2 报告](docs/V0.2_REPORT.md)。以下未验证项和后续计划均为当时的历史记录。

## 环境检查
- 工作目录初始为空。
- Windows x64 10.0.26200，.NET SDK 10.0.300 / MSBuild 18.6.3。
- Windows SDK 22621 / 26100，Visual Studio 2022 与 2026，Java 21。
- NuGet 实际还原完成；Windows App SDK 1.8.260921001、OpenccNetLib 1.7.0。

## 阶段状态
1. 工程基础：七个分层项目、WinUI 五页导航、MVVM、DI、用户配置、OpenCC 已完成并编译运行。
2. 文件与批量：八种解析器、冻结配置、UUID、去重、重名编号、并发安全提交、取消、失败隔离、跳过范围、文件预览与差异行已实现。DOCX/EPUB 采用结构解析与验证。
3. 规则：替换、保护、正则、方向/地区/格式/区域条件、排序、启用、冲突检测、单条/规则包测试轨迹、JSON 导入导出已实现。复杂条件目前通过规则 JSON 编辑。
4. 文本与后台：实时防抖和版本检查、会话保留、纯文本剪贴板事件监听、暂停/停止、托盘、可配置全局快捷键、HKCU 启动项已实现。
5. 历史与设置：SQLite WAL/事务/备份、查询分页筛选、批次/文件详情、重新排队、CSV/JSON 导入导出、主题与设置已实现。
6. 集成与发布：Release 构建和自包含目录成功；已捆绑 .NET、Windows App SDK、Temurin JRE 21.0.13+9 与 EPUBCheck 5.3.0。安装脚本已就绪，安装包构建结果见下方最新记录。

## 实测结果
- `dotnet test tests/CatTransform.Tests --no-restore`：55/55 通过，0 失败，0 跳过；结果保存在 artifacts/test-results/release-tests.trx。
- OpenCC 八种配置，每配置 2000 次并发调用一致；生产封装仍锁定实例避免诊断状态共享风险。
- EPUBCheck：实际 Java 验证通过，检查导航/资源/元数据保持。
- UI Automation：五个页面、实时转换、切页正文保留通过。
- 真实文件选择器 → 队列 → TXT/JSON/EPUB 输出 → SQLite 历史通过；未手动开始不执行；完成项不重复转换。EPUB 使用发布目录内置 Java。
- 托盘：暂停监听后关闭窗口隐藏、托盘恢复、停止清空缓存通过。
- WindowsProbe：当前用户显示启动、静默启动、关闭三种注册表状态通过；探针 finally 恢复原值。
- 证据：artifacts/ui-results.json、batch-ui-results.json、tray-ui-results.json、batch-window.png、text-window.png。
- 发布故障已修复：Windows App SDK 默认发布遗漏应用 PRI 索引导致 0xc000027b。工程增加发布后复制与缺失失败检查，发布版已启动并完成 UI 测试。

## 已实现但尚未充分验证
- 剪贴板真实 ContentChanged 自动转换/自写去重的全部边界：当前用户剪贴板含富文本，自动化脚本拒绝覆盖；保留用户数据。监听状态策略有单元测试，托盘生命周期有真实 UI 测试。
- 实际注销/重新登录后的启动行为；注册表集成探针单独执行并恢复原值。
- 多显示器 DPI 切换、屏幕阅读器、全套键盘导航、多版本 Windows 11。
- 非受控复杂 DOCX（嵌套域、修订、复杂文本框）与大型真实 EPUB 语料回归。

## 未完成的完整验收工作
- 干净 Windows 11 虚拟机上的安装、升级、卸载和无全局运行时验证。
- 真实磁盘耗尽、断电/进程强杀、权限边界和恶意重解析点竞争的完整系统测试。
- 安装包代码签名及对外发布前的许可证再分发审核。

## 已知限制
- 默认文件大小上限 128 MiB，单文本片段最多 400 万 UTF-16 字符；不是无限大流式处理。
- 无法可靠识别的编码拒绝转换，不静默猜测 GBK。
- DOCX 只跨连续普通 run 合并；多 run 发生长度变化时失败，避免格式猜测。字段、图片、绘图保持边界。
- EPUB 含 encryption.xml 时拒绝，包括字体混淆；不绕过 DRM。
- JSON 使用精确 JSON Pointer 路径，不提供 JSONPath 通配符。
- 高级规则 JSON 编辑器仍有可用性提升空间；差异显示按行，而非专业编辑器级最小字符差异。
- 正常链接输入被拒绝，但不声称能抵抗同账户恶意进程的目录替换竞争。

## 恢复开发
先读取本文件及 TECHNICAL_VALIDATION.md。最终程序目录为 artifacts/release；artifacts/publish 为早期诊断目录，不用于交付。下一步优先完成上述系统级验收，不重复重写已通过的核心模块。

## 验收原则
未经测试的功能不标记通过；未通过全部验收前不称为正式 v0.1 发布。

## 2026-11-11 UI update
- Default window: 1440 x 960, centered and clamped to monitor work area; sidebar width: 240.
- Sidebar groups: conversion tools; history and settings. Stable page IDs preserve tray navigation.
- Bundled LXGWWenKaiMono-Light.ttf via app font resource and TextBlock style; no system installation required.
- Release publish succeeded. Tests: 55/55 passed (2026-11-11). UI Automation passed all five pages, live text conversion and session retention.
- Fixed a native startup crash caused by setting TextElement.FontFamilyProperty on Grid; verified corrected build starts and completes UI tests.
- Screen capture was obscured by another fullscreen application; visual font rendering remains unverified. Existing system-level acceptance limitations still apply.


## 2026-11-11 DPI compatibility fix
- Root cause reproduced: DPI Unaware / 96 DPI at 175% system scale.
- Fixed manifest to sole PerMonitorV2; native DIP-to-pixel geometry, framework-owned DPI changes, overflow guards and taskbar icon scaling.
- Final executable verified: PMv2, 168 DPI; native screenshots and five-page UI regression passed. 70/70 tests passed.
- Only one physical monitor available. Other scale levels have sizing tests only; cross-monitor transitions remain unverified.
- Full report: docs/DPI_FIX_REPORT.md. The earlier note about unverified font rendering is superseded by this native capture and UIA check.



## 2026-11-11 size reduction
- Replaced the bundled full Temurin JRE with a jlink image built from the same-version pinned and verified Temurin JDK. The published Java image is about 109 MiB instead of about 145 MiB.
- Real EPUBCheck 5.3.0 validation and application batch UI TXT/JSON/EPUB conversion passed with the smaller Java image.
- The Windows App SDK dependency-narrowing experiment saved only about 2 MiB and left AI binaries present, so it was reverted.
- Earlier size figures and JRE source notes refer to the prior verification build; the current build provenance is in TECHNICAL_VALIDATION.md.

- Final package uses Inno Setup LZMA2 ultra64. Compared with the previous 165,792,578-byte installer, the candidate shrank to about 129 MB while preserving the offline EPUB workflow. See docs/SIZE_OPTIMIZATION.md.
