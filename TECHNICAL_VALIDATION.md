# catTransform 技术验证

公开文档支持与本机测试结果分列，不能互相替代。

## 开发环境
- .NET SDK 10.0.300，MSBuild 18.6.3，Windows x64 10.0.26200。
- Visual Studio Community 2026 18.6.2、Windows SDK 26100；Java 21.0.3。

## 选型与证据
|问题|方案与资料|实际验证|
|---|---|---|
|OpenCC/.NET 10|[OpenccNetLib 1.7.0](https://www.nuget.org/packages/OpenccNetLib/1.7.0)，内嵌词典|tools/IntegrationProbe 实际运行八种配置，每配置 2000 次并发一致；xUnit 验证地区词汇；生产封装额外串行化单实例访问|
|WinUI 3|[Windows App SDK 1.8](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-notes/windows-app-sdk-1-8)，固定 1.8.260921001 稳定维护版本|全解决方案/Release 构建成功，五页 UI Automation、实时转换、队列和历史交互通过|
|自包含部署|[微软部署说明](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/unpackage-winui-app?tabs=csharp)|同时设置 SelfContained 与 WindowsAppSDKSelfContained。发布遗漏 catTransform.pri 曾导致原生崩溃，已增加复制与缺失失败检查；最终目录已实际启动。干净系统部署仍待验证|
|DOCX|DocumentFormat.OpenXml 3.3.0，SDK DOM 和 OpenXmlValidator|跨 run 转换、粗体和 run 数保持测试通过；长度变化多 run 明确拒绝；复杂域保持边界|
|EPUB|独立 ZIP/XML 层，[W3C EPUBCheck](https://github.com/w3c/epubcheck)|EPUBCheck 5.3.0 实际通过；单元/集成检查资源、导航、元数据和 mimetype。最终 UI 批量测试使用捆绑 JRE 实际转换 EPUB 成功|
|Markdown/CSV|Markdig 0.41.3 / CsvHelper 33.1.0|代码块、链接目标、CSV 引号和嵌入换行测试通过|
|SQLite|Microsoft.Data.Sqlite 10.0.12 + SQLitePCLRaw.bundle_e_sqlite3 3.0.5|初始旧依赖有 NU1903，已升级；事务、导入、备份测试通过|
|规则安全|NonBacktracking 优先，显式回退+150ms 超时；大小限制|顺序、保护、冲突、正则超时和规则包冲突测试通过|
|托盘/剪贴板/启动|Shell_NotifyIcon、RegisterHotKey、Clipboard.ContentChanged、HKCU Run|暂停关闭隐藏、托盘恢复、停止清缓存 UI 实测通过；HKCU 显示启动/静默/关闭探针通过并恢复原值。剪贴板自动转换全流程未覆盖：当前含用户富文本，测试拒绝替换|

## 捆绑验证运行时
Temurin 21.0.13+9 Windows x64 JRE 从 [Adoptium 官方发布](https://github.com/adoptium/temurin21-binaries/releases/tag/jdk-21.0.13%2B9) 获取。
ZIP SHA-256：`2332c0b4cff6bed722da3567c91d5e47568bcba863fa4945d0a8d6fc76158628`，与官方 API 元数据一致。
完整 `legal`、`NOTICE`、EPUBCheck 的 `LICENSE.txt`、`THIRD-PARTY.txt` 和 `licenses` 目录随包保留。
运行时首选 `runtime/java/bin/java.exe` 与 `runtime/epubcheck/epubcheck.jar`，不需要用户安装全局 Java；未捆绑的开发构建才使用配置的本地工具。

## 测试证据
自动测试结果：`artifacts/test-results/release-tests.trx`；真实 UI 结果：`artifacts/ui-results.json`、`artifacts/batch-ui-results.json`、`artifacts/tray-ui-results.json`。
所有自动测试均实际执行，没有 Skip。没有通过降级格式或跳过 EPUBCheck 来通过测试。

## 已知边界
- 128 MiB 输入上限，文本片段 400 万 UTF-16 字符上限；不是无限流式处理。
- 未知编码拒绝，不使用无声替换字符猜测 GBK。
- JSON Pointer 精确路径，未提供 JSONPath 通配语法。
- EPUB 字体混淆和 DRM 都拒绝；不绕过加密。
- OpenCC 不受信任实例并发重配置不允许；封装不暴露实例。
- 不承诺抵抗同账户恶意程序对输出目录实施重解析点竞争；正常链接输入被拒绝。


## 2026-11-11 Java runtime size reduction
The earlier full-JRE description above records the previous verification build. The current release preparation uses pinned Temurin JDK 21.0.13+9 (SHA-256 a4dbaa05fca4bd89d84584e58c459bd732efebb1722af80ab13c6516832004f9) and jlink to build a minimal Java image. Full EPUBCheck 5.3.0, its dependency JARs, and Java legal/NOTICE remain bundled. The pinned JDK metadata is tools/temurin-jdk-runtime.json. A real EPUB fixture passed direct EPUBCheck and the application batch UI conversion with this image.
