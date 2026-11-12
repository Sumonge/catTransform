# catTransform v0.2（开发中）

Windows 11 x64 离线中文简繁及地区词汇转换器。C# / .NET 10 / WinUI 3。

Windows 11 x64 离线简繁与地区词汇转换器。v0.2 增加可提取文字的 PDF 输入，并改进 DOCX、EPUB 文本片段处理。详见 [升级报告](docs/V0.2_REPORT.md)、[使用说明](docs/USER_GUIDE.md)和[架构](docs/ARCHITECTURE.md)。

运行 `artifacts/release/catTransform.exe` 时请保留整个目录。构建脚本输出三个安装包：`catTransform-0.2.0-win-x64-lite.exe`、`catTransform-0.2.0-win-x64-full.exe` 和 `catTransform-0.2.0-epub-addon-win-x64.exe`，均位于 `artifacts/installer`。精简版不含 Java/EPUBCheck；可离线安装 EPUB 组件包解锁 EPUB，或直接使用完整版。转换本身不需要联网。

PDF 输入会提取可选择的文字，默认生成可搜索的文字重排版 PDF，也可在设置中选择 UTF-8 TXT；不保留原 PDF 排版、图片或批注，也不支持扫描件 OCR。

## 构建
需要 .NET SDK 10.0.300、Windows SDK 26100 与 Visual Studio Windows 应用开发工具。

```powershell
dotnet restore catTransform.sln
dotnet build catTransform.sln --no-restore
dotnet test tests/CatTransform.Tests --no-restore
dotnet publish src/CatTransform.App -c Release -r win-x64 --self-contained true -o artifacts/release
```

EPUB 集成测试需要 tools/epubcheck/epubcheck-5.3.0/epubcheck.jar 和 Java 21。构建依赖下载联网；程序业务逻辑完全离线。完整打包使用 tools/publish.ps1，Java/EPUBCheck 打包由 tools/prepare_epub_runtime.py 负责；下载辅助脚本只用于开发，不进入应用运行流程。不要删除构建后的 catTransform.pri，缺失该资源索引会导致启动失败。

默认数据：当前用户 LocalAppData/catTransform；输出：Windows 已知 Documents/catTransform/Output。不会自动清理输出。测试报告位于 artifacts/test-results。


## Smaller offline build
The release script derives a reduced Temurin Java runtime with jlink from the pinned and SHA-256-verified JDK; EPUBCheck remains bundled and offline. The JDK archive and extraction stay in ignored build artifacts and are not included in the installer. See TECHNICAL_VALIDATION.md for provenance and validation.
