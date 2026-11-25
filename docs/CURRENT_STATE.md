# YangCAD 当前状态（2026-11-20）

[PR #20](https://github.com/5788324/YANG-AGENT_CAD/pull/20) 汇总当前整合源码。DLL、MCP 与安装包使用 0.10.0-dev 对应的版本体系。验收以 PR HEAD 完整 SHA 为准。

| 领域 | 状态 |
|---|---|
| 源码、入口和测试契约 | STATIC，以 PR 最新 CI 为准 |
| AutoCAD 2024 | Gemini 在 `1120e805` 通过构建、板符号 22/22、YGHSPLIT、YGINC 8/8、封包、Ribbon 六组和双列工具箱停靠与主题同步。SLBP 验证进入预览及超范围拦截，Live MCP 验证 37 个工具及读写清理。 |
| 图片内嵌 | `1120e805` 的普通 PNG 真实任务误判为已裁剪，源 DWG 哈希保持一致。后续候选修正裁剪识别与 worker 版本选择，增加裁剪判定等待和整批跳过状态。新改动校正未旋转图像的 OLE 锚点，并明确拒绝旋转图片。AutoCAD 2024 真实内嵌及移走源图后的重开仍待复验。 |
| 文字、两点拆分与工具箱 | 文字大小写及标点批改保留常用内联格式，格式化多行块属性与文字引线共用同一处理。含字段的对象保持原样。待 WINDOWS AutoCAD 2024 图纸复验。 |
| Revit 原生重建、Civil 3D GUI/MCP、安装实装 | 未验，本轮范围外 |
| 工具箱和 Ribbon | 工具箱从命令目录生成双列，支持搜索、常用、最近使用及记忆折叠。工具箱默认显示 30 个入口，变体可搜索；高频命令直达，界面和合并命令待 AutoCAD 实测。 |
| 圆弧裁切与图纸体检 | YGDETAILPOLY／YGOUTSIDE 支持圆弧边界及直线／圆弧环的非关联填充裁切。大样可复制完整入框的含属性普通块。MCP 图纸体检可检查标注和引用。AutoCAD 2024 实机待复验。 |
| 发布 | 尚未发布 |

`1120e80520718d8b8084fb511ed9d316629d0f4f` 复验时加载的 AutoCAD DLL SHA-256 为 `88F4C5909D8BC32F552D7B18ED49B4E1748BC603AC55B3E0E34E1B8079071272`。

整合范围见 [功能目录](FEATURE_CATALOG.md)。折断、多边形大样、边界外裁切纳入本候选，多边形使用 YGDETAILPOLY，YGDETAIL 和原 MCP 矩形入口继续可用。

[当前验收任务](../tasks/CURRENT_TASK.md) · [RC1 历史记录](RC_AUTOCAD2024_0.10.0.md) · [历史实机矩阵](RUNTIME_TEST_MATRIX.md) · [项目日志](../WORKLOG.md)
