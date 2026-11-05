# YangCAD 功能目录与证据范围（2026-11-05）

版本：`0.10.0-rc.1`（**RC 文档标记**；实际 Assembly/MCP 仍 `0.10.0-dev`）；范围：AutoCAD 2024 Quick 特定样例 G5 GO。

| 功能 | CAD/GUI 入口 | MCP | AutoCAD 2024 本轮结论 |
|---|---|---|---|
| 工具箱/Ribbon/关于 | `YANGCAD_TOOLBOX` / YangCAD 面板 | — | R01 样例通过 |
| 图层合并/开关/冻结/锁定 | `YGLAYER` / `YGLMERGE` / `YGLONOFF` / `YGLFREEZE` / `YGLLOCK` | `cad.layer.*` | R03 样例与单步 Undo 通过 |
| **矩形** QG 风格提取大样 | `YGDETAIL` | `cad.detail.extract` | R04 样例通过；复杂对象仍可能跳过 |
| 比例转换双模式 | `YGSCALE` | `cad.scale.convert` | R06：标准 7/8（INSERT 跳过），缩尺 8/8；对象级 Undo |
| 引线重建/独立文字关联 | `YGLEADERFIX` | `cad.leader.rebuild` | R05 样例通过，复杂路径安全拒绝 |
| Revit 图纸预处理 | Toolbox 一键 | `project.revit.prepare` | R09 GUI/MCP MAP 11/11 与 ReaderVerifier 通过；**Revit Native NOT_TESTED** |
| 图片 OLE / DWG Xref Bind | Toolbox 当前图 / 选择文件夹 | `project.image.embed` | R07 内部化读回通过；文件夹仍 **PARTIAL 2/1/1** |
| 快捷键 | `YGKEYS`，PGP / CUI | — | R02 PGP/备份/REINIT 通过 |
| 本地 MCP | `YANGCAD_MCP` | stdio 74 / AutoCAD 37 | R08 当前 AutoCAD 样例通过；Civil live 67 **NOT_TESTED** |

**未实现（Backlog）**：按任意闭合多段线边界提取、跨边界复杂对象按选项裁切、删除闭合边界外全部图元、直线转 Z 形折断线。此处不可视为本版功能。

**其他年份和宿主**：Civil 3D live / Revit Native / 非 AutoCAD 2024 均不属于本轮 G5 通过范围。详细验收 SHA 与发布包见 `docs/RC_AUTOCAD2024_0.10.0.md`；单个 MCP tool 见 `docs/MCP_COVERAGE_MATRIX.md`。
