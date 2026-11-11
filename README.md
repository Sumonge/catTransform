# catTransform v0.1

Windows 11 x64 离线中文简繁及地区词汇转换器。C# / .NET 10 / WinUI 3。

**可运行验证构建，尚未完成全部系统级 v0.1 验收。** 详见 [进度](DEVELOPMENT_PROGRESS.md)、[技术验证](TECHNICAL_VALIDATION.md)、[使用说明](docs/USER_GUIDE.md)和[架构](docs/ARCHITECTURE.md)。

运行 `artifacts/release/catTransform.exe`，请保留整个目录。安装包输出位置为 `artifacts/installer/catTransform-0.1.0-win-x64.exe`。发行目录包含 .NET、Windows App SDK、Java 和 EPUBCheck；使用时不需要联网下载运行时。

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
