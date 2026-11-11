# catTransform 架构

依赖方向：App → Application / Conversion / Documents / Infrastructure → Core。
Core 定义不可变配置记录、任务状态和接口，不依赖平台。App 在组合根注册依赖注入。

- Conversion：OpenccNetLib 实例按配置缓存，锁定转换避免共享诊断状态竞争；规则按阶段、顺序和 UUID 确定性排序。保护词从流水线分割，无占位符污染。正则优先 NonBacktracking，显式允许时才回退，150ms 超时。
- Documents：Markdig 源位置编辑；CSV 正式解析重写；JSON token 树；SRT/ASS 控制语法分段；DOCX Open XML SDK；EPUB ZIP+受限 XML+EPUBCheck。
- Application：路径去重、冻结批次配置、有限并发、失败隔离、取消、同目录临时文件、验证后无覆盖移动、编号重试。预览走相同格式转换但仅写临时目录。
- Infrastructure：当前用户设置、Windows 已知文件夹、SQLite WAL/事务/在线备份、当前用户启动项。
- App：WinUI 3 控件、MVVM 会话文本、原生托盘、事件驱动剪贴板、UI 调度。

## 数据边界
正文只进入内存、源文件或用户明确保存的输出。SQLite 保存路径、规则快照、状态和错误类型；解析异常不直接记录 Message，防止异常携带正文。历史导出默认移除完整路径。

## 限制
当前解析使用有限大小的内存缓冲，不是无限大文件流式转换。拒绝无法可靠识别的非 Unicode 编码。复杂 DOCX 跨 run 长度变化失败而不猜测格式映射。EPUB 加密声明（包括字体混淆）拒绝。路径检查防止普通链接输入，但不声称抵抗恶意本机进程在检查与写入间替换目录的攻击。
