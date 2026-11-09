> **审查快照提示（2026-11-09）**：本 `review/full-source-20261109` 工作树同时包含 [PR #13](https://github.com/5788324/YANG-AGENT_CAD/pull/13)、[#14](https://github.com/5788324/YANG-AGENT_CAD/pull/14)、[#15](https://github.com/5788324/YANG-AGENT_CAD/pull/15)、[#16](https://github.com/5788324/YANG-AGENT_CAD/pull/16)、[#17](https://github.com/5788324/YANG-AGENT_CAD/pull/17) 的源码。它们全部是尚未合并的 Draft。请先看 `docs/AI_REVIEW_SNAPSHOT_20261109.md`，不要将此快照当成可发行 DLL，本文下方若有“当前任务仅 PR #17”等内容属于原分支交接历史。

## 2026-11-09 — 新增标注文字工具三功能（独立源码候选）

- 独立分支 feature/annotation-utilities-v1-20261109 基于 main@2f0980d4acd08b087d924b5c1f5a5bb18e8668a8，与 Gemini 测试中的 Draft PR #16 完全独立。
- YGDIMTXT：只选择当前模型/布局空间 Dimension.DimensionText 存在覆盖内容的标注（非默认空白/仅 <>），不修改标注测量或文字。
- YGSLOPE：Section=|ΔY|/|ΔX| 或 Plan3D=|ΔZ|/XY 水平距离，两点/LINE，Percent、Degrees、Ratio 三种文字，独立短坡度指示线+低端箭头+原生 MText。
- YGTBOX：针对 TEXT/MTEXT 文字显示范围，生成原生矩形 Polyline 或 Circle，按 mm 设置偏移留白；文字不变。源自用户参考贱人工具箱/源泉，附件 column_mark / beam_mark (实际 CMK+CMKD 柱标注) / 双线 LISP 不直接包含上述功能，不改现有命令。
- 新增 tests/tools/test_annotation_utilities_policy.py（实际路径 tools/test_annotation_utilities_policy.py）、docs/ANNOTATION_UTILITIES_V1_20261109.md、tasks/ANNOTATION_UTILITIES_WINDOWS_SMOKE_20261109.md；存储用户上次参数并提供中文 Toolbox/PGP 入口。
- STATUS=STATIC_ONLY / Windows AutoCAD 2024 Build, NETLOAD, RealDWG, Native _.U 均 NOT_TESTED。不得合并/发布，待 G1 CI、Gemini G2–G4 和 ChatGPT G5 独立审核。

---

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
