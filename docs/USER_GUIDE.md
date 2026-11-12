# catTransform 使用说明

1. 批量转换：添加文件、文件夹或拖放到队列；选择 OpenCC 配置，点击开始。成功文件保留在队列，再次开始只处理等待项。失败重试只重新排队，不自动执行。
2. 输出：默认系统 Documents 下 catTransform/Output；文件夹保留根目录结构；同名自动编号，原件不覆盖。
3. 文本转换：输入后防抖更新；复制和保存均需主动点击。关闭程序不保存正文。
4. 剪贴板：手动读取或明确授权监听；自动监听只更新预览。暂停保留缓存，停止清空。监听运行或暂停时关闭窗口会进入托盘。
5. 历史：搜索分页查看。重新转换使用当前配置并加入队列。替换导入需确认并自动备份 SQLite；删除记录不删除文件。
6. 设置：选择主题、输出目录、文件格式范围、并发、快捷键和启动方式。完整版内置 EPUBCheck 和 Java；精简版可另装离线 EPUB 组件。范围与规则包使用 JSON 编辑，验证通过后应用；规则列表可启用、禁用、排序和删除。

## 转换配置
s2t / t2s：标准简繁；s2tw / s2hk：台湾/香港字形；s2twp：台湾词汇；tw2sp：台湾词汇转大陆词汇；tw2s / hk2s：地区字形转简体。

## 规则与范围
新建规则默认禁用，Enabled=true 才执行；Stage=0 为预处理，1 为后处理；Kind=0 替换、1 保护、2 正则。Order 为顺序。Config、Direction、Locale、Format、Region 为可选精确条件。Direction 使用 s2t/t2s，Locale 使用 standard/tw/hk。Region 使用 body/header/footer/comments/code/key/navigation/metadata 等解析器区域名。选择规则后可按其 UUID 单条测试，也可留空 UUID 测试完整规则包。
正则替换支持 `$$`、`$&`、`$1` 和 `${name}`；不支持复制整个输入的前后文替换符，防止输出膨胀。
CSV 列从 0 开始，默认不转换表头。JSON 路径采用 JSON Pointer，如 /books/0/title；不是 JSONPath 通配表达式。高级范围不会允许对底层 XML 任意替换。

## 当前开发构建限制
完整验收以 DEVELOPMENT_PROGRESS.md 为准。不要将编译成功或单元测试通过理解为所有 Windows UI/部署验收完成。

## v0.2 PDF 与安装包

- PDF 提取可选择的文字后进行转换。默认输出可搜索的文字重排版 PDF；设置中可改为 UTF-8 TXT。两种输出均不保留原 PDF 页面版式、图像、批注或链接。
- PDF 重排版最多支持 20 万字符、1000 页；更长的内容请选择 TXT 输出。
- 扫描件或没有文字层的 PDF 会报错且不会生成输出；需先用其他工具进行 OCR。
- 精简版可转换 TXT、MD、SRT、ASS、CSV、JSON、DOCX 和 PDF。EPUB 需要另外安装同版本离线 EPUB 组件；完整版直接内置该组件。
- 卸载 EPUB 组件不会删除设置、历史或已转换文件。安装后重新打开设置页，可查看组件状态。
