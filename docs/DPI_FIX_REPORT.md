# catTransform v0.1 高 DPI 修复报告

日期：2026-11-11。范围：Windows DPI 兼容性；产品名称和 v0.1 版本、转换算法、设置逻辑、配色、字体资源、字号及常规布局均保留。

## 根因与技术栈

项目使用 .NET 10、WinUI 3 / Windows App SDK 1.8，非打包自包含桌面应用。项目通过 ApplicationManifest 引用 app.manifest，启动入口由 WinUI XAML 编译器生成。

旧清单缺少 DPI Awareness 声明，启动代码也未设置它。运行旧版确认窗口为 DPI Unaware，GetDpiForWindow 返回 96；本机实际缩放为 175%。这是 Windows 整体位图拉伸的直接原因，不能靠放大字体解决。原有 AppWindow 的初始和最小尺寸使用未转换的像素常量，在启用 DPI 感知后也必须修正。

## 代码修改

- `src/CatTransform.App/app.manifest`：单一 PerMonitorV2 声明。没有另加旧 dpiAware、兼容模式或运行时 SetProcessDpiAwareness 调用。项目最低支持 Windows 11，不需要旧系统回退声明。
- `WindowSizing.cs` / `WindowDpi.cs`：将原来的 1440×960 默认尺寸、1100×720 最小尺寸作为逻辑单位，仅在原生 HWND/AppWindow 边界转换为目标 DPI 像素并限制于工作区。窗口仍可正常缩放、最大化；不是固定物理像素窗口。
- 使用 WM_GETMINMAXINFO 更新当前显示器上的最小尺寸，替代尺寸变化事件内再次 Resize 的反馈循环。
- WM_DPICHANGED 原样传递给 WinUI，包括系统建议矩形；WinUI 负责 XAML 栅格化和尺寸变化。事件处理完成后仅限制窗口在新工作区内，不再次按 DPI 比例缩放。
- `MainWindow.cs`：普通界面继续使用原有 XAML 逻辑单位。可用逻辑空间不足时提供横向/纵向滚动保护，批量页保留队列和操作区结构；预览抽屉不超过当前窗口宽度。这些是溢出保护，不改变常规布局、字号和配色。
- `NativeTray.cs`：保留原图标，按任务栏 DPI 和系统小图标尺寸加载 ICO；在 DPI/显示配置变化时刷新，释放旧图标句柄，替代固定 32px 加载。

## 字体与资源检查

- LXGWWenKaiMono-Light.ttf 保持原样，仍从应用资源加载；UI Automation 和窗口截图确认正文/标题使用该字体资源。
- 侧栏功能图标为 SymbolIcon 字形，随 WinUI 在目标 DPI 渲染。
- ICO 已有 16、24、32、48、64、128、256 像素资源，保留原设计；窗口图标交由 AppWindow 管理，托盘按目标尺寸加载。
- 当前界面没有 SVG 或其他内容位图，也没有 ScaleTransform、LayoutTransform、RenderTransform 或自定义字体缩放乘数。
- 系统标题栏和部分原生控件仍使用其系统字体，未为 DPI 修复强行替换它们。

## 已执行验证

|项目|结果|
|---|---|
|Release 自包含发布构建|成功，无编译错误|
|原有测试及新增 DPI 回归|70/70 通过，0 失败、0 跳过|
|EXE 实际嵌入清单|读取 RT_MANIFEST，唯一 dpiAwareness = PerMonitorV2|
|修复前窗口上下文|DPI Unaware，96 DPI|
|修复后窗口上下文|Per-Monitor V2，168 DPI，175%|
|本机显示器|单屏 3840×2160，工作区 3840×2076|
|默认窗口|2520×1680 物理像素，即 1440×960 逻辑单位；未发生二次尺寸缩放|
|最小窗口|1925×1260 物理像素，即 1100×720 逻辑单位；截图中操作区完整可见|
|175% 原生窗口截图|通过 PrintWindow 捕获窗口自身；正文、图标和控件可清晰显示，无整体低分辨率位图拉伸|
|功能 UI 回归|五页切换、实时简繁转换、切页文本保留全部通过|
|字体|UIA 可读取 LXGW 字体资源，截图可见对应字形|

截图：`artifacts/dpi-native-window.png`、`artifacts/dpi-small-viewport.png`。
机器可读结果：`artifacts/dpi-before.json`、`artifacts/dpi-verification.json`。
清单：`artifacts/dpi-embedded.manifest`。测试报告：`artifacts/test-results/dpi-tests.trx`。
回归脚本：`python tools/dpi_probe.py`。该脚本不修改系统缩放。

## 缩放覆盖与未验证项

|系统缩放|窗口尺寸/工作区计算测试|真实显示器渲染验证|
|---|---|---|
|100%|通过|未验证|
|125%|通过|未验证|
|150%|通过|未验证|
|175%|通过|已验证|
|200%|通过|未验证|
|250%|通过|未验证|
|300%|通过|未验证|

只有一台显示器，未实测不同 DPI 显示器间拖动、热插拔和显示缩放运行时切换。代码保留 WinUI 的原生 DPI 消息处理并添加工作区保护，但不能将代码检查视为多屏实测。

测试脚本请求 720×500 逻辑单位时被正确限制为 1100×720，验证的是最小窗口行为，不是小于最小尺寸的截图测试。低分辨率/极高缩放导致工作区小于逻辑最小尺寸时，已有工作区限幅和滚动回退；这种极小工作区未作实机验证。

## 官方依据

- Microsoft 推荐通过应用清单声明默认 DPI Awareness：[Setting the default DPI awareness for a process](https://learn.microsoft.com/en-us/windows/win32/hidpi/setting-the-default-dpi-awareness-for-a-process)。
- WinUI XAML 使用逻辑像素，原生窗口尺寸使用物理像素，应在边界进行转换：[Windowing overview](https://learn.microsoft.com/en-us/windows/apps/develop/ui/windowing-overview)。

本次修复消除了已复现的 DPI Unaware 配置错误；此前与 DPI 无关的系统级完整验收限制仍按 DEVELOPMENT_PROGRESS.md 保留。