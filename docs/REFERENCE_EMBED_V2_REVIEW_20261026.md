# YangCAD 引用内嵌 V2 — 静态审查与功能变更

日期：2026-10-26
状态：IMPLEMENTED / STATIC VALIDATION; Windows AutoCAD 2024 REQUIRED, NOT RELEASE GO
基线：GitHub main `396a79408bfb38d374871fb030b54012748be8b6` / 2026-09-08 clean candidate

## 用户工作流

- 工具箱 → 引用内嵌（当前图纸 / 批量）→ **当前图纸一键内嵌**：使用当前已保存且没有未保存变更的 DWG，立即启动独立 Worker。自动选择输入、输出目录；无需填参数。
- **选择文件夹 · 批量内嵌**：选择目录，自动递归处理该目录的所有 DWG；保持相对目录结构。
- 仍保留高级参数和原 `project.image.embed` MCP Tool，缺少 `input_path` 时默认读取当前打开的 DWG 路径。
- 工具箱每 2 秒读取 Worker `status.json` 更新状态，显示 PASS/PARTIAL/FAIL，提供“打开输出目录”按钮。
- **注意：第一阶段绝不原位覆盖打开的 DWG**。生成旁路 `*_Embedded` 副本；Worker 异步执行，必须读取 `status.json` 的最终状态。用户要求的“直接改变当前打开的图纸本身”尚未实现（自动切换输出也未实现）。

## 支持边界

1. 图片 RasterImage：非裁剪、默认图像效果、非透明且可找到源文件的顶层图片转为 OLE。相对路径根据 **原始 DWG 路径**解析，而不是移动后的 Worker 临时副本。
2. DWG Xref：增加 `bind_dwg_xrefs`；已加载且可解析的 Xref 用 COM Block.Bind(false) 绑定为内部块，符号名称使用前缀以降低同名覆盖。嵌套和失效 Xref 必须按真实结果报告，未经 Windows 实测不宣称 GO。
3. PDF/DGN/DWF Underlay：**不可无损 Bind**，只报告（保留原引用、不拆解、不删除）。
4. Excel/CSV 数据链接：未实现自动断开，避免数据丢失、格式漂移。
5. 块定义内图片：目前不是可靠的 OLE 映射路径，记录为残余外链，PARTIAL；不冒充已内嵌。

## 已检查并修复/收紧

| 问题 | 处理 |
| --- | --- |
| 原 DWG 相对图片路径在输出目录工作副本中失效 | Snapshot 以原始输入路径解析 |
| 异步 Job 只启动却显示执行完成 | UI 明确显示“任务已启动（尚未完成）”；状态文件指向最终状态 |
| 批处理中一个文件在外层抛异常终止后续文件 | 每文件异常隔离成 FAIL，继续下一份 |
| COM 可能复用已有 AutoCAD 宿主并在 finally Quit | 新增 HWND/PID 原进程集校验；不拥有时拒绝处理，绝不 Quit 已存在进程 |
| Xref 目录重定位后相对路径不可绑定 | Bind 前尝试按原始输入 DWG 路径修复为绝对路径 |
| 已绑定 Xref 中仍有嵌套图片/其他外链 | 绑定后再审计残余 Xref 与块内图片，PARTIAL |
| 图片和 Xref 同时存在 | 同一 Worker 先转换顶层图片，再 Bind Xref，分别报告成果和失败 |
| Dynamic Block 在 YGSCALE 可能破坏状态 | V1 安全跳过；待实机专测后再开放 |
| 标注对象级 DIMLFAC 读写失败可能误报成功 | 失败抛出并回滚对象，不再默默成功 |

## 限制与未解决项

- OLE Clipboard/PASTECLIP 依赖 Windows UI、全局剪贴板、COM 进程状态，静态测试无法证明实际图片保真。
- 原图有 Clipping、Brightness/Contrast/Fade、Transparency、旋转、比例和 PaperSpace 时需真实图纸回归。
- 绑定 DWG Xref 后的嵌套图片和 PDF/DGN/DWF 仍可能是外部依赖，不能标为 fully self-contained。
- 某些保存到不支持写入的路径或正在被其它进程锁定的文件会 FAIL，必须明确报告。
- 未实现“所有外部依赖全部自动嵌入”或无损地变为 DWG 内部实体；要达到可交付单文件的目标需逐类型处理，部分必须采用 eTransmit 依赖打包。
- 未进行 Windows / AutoCAD 2024 编译/部署/人工可见性测试，状态 STATIC ONLY。

## 比例转换需继续验收

- Standard1To1 几何不变；ScaledDrawing 几何旧/新倍数，DIMLFAC 反向补偿。
- 需要重新验证 Leader/MLeader 的文字、箭头和落点不漂移。
- 需要一条真正用户可见的单次 Undo/Ctrl+Z 证据；静态事务结构不是证明。
- 多尺度、注释性尺寸、动态块、Civil 3D/AEC/Proxy 均不应一股脑 TransformBy。

## 验收要求

请执行 `tasks/REFERENCE_EMBED_V2_WINDOWS_SMOKE_20261026.md`。Gemini/Codex 只能提供 raw evidence，不能 FINAL GO。
