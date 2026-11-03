# 0.10.0-dev Windows 验收矩阵

更新时间 2026-11-03；此文件是**待测清单**，不是运行结果。每一项的 PASS 必须关联对应 commit 和原始证据。

| ID | 领域 | 重点场景 | 当前状态 | 关联旧任务 |
|---|---|---|---|---|
| R00 | 基线/构建/加载 | 唯一 SHA、AutoCAD 2024 build/deploy/loaded | NOT_TESTED | `tasks/CURRENT_TASK.md` |
| R01 | 桌面 UI | 原生 Ribbon 保持、可停靠 Palette、关于页 PID/版本/路径 | NOT_TESTED | `tasks/CURRENT_TASK.md` |
| R02 | 快捷键 | PGP 冲突、备份、REINIT、CUI 不受影响 | NOT_TESTED | `tasks/SHORTCUT_PANEL_NATIVE_RIBBON_WINDOWS_SMOKE_20261103.md` |
| R03 | 图层管理 | 合并/开关/冻结/锁定、块定义、嵌套拾取、外参依赖层、Undo | NOT_TESTED | `tasks/LAYER_TOOLS_QG_WINDOWS_SMOKE_20261103.md` |
| R04 | 提取大样 | 矩形边界、几何裁切、放置、倍率、保留原图、Undo | NOT_TESTED | `tasks/LAYER_TOOLS_QG_WINDOWS_SMOKE_20261103.md` |
| R05 | 引线 | LINE/Polyline/Leader → MLeader；中文 MTEXT、箭头、Undo | NOT_TESTED | `tasks/REVIT_PREP_LEADER_REPAIR_WINDOWS_SMOKE_20261026.md` |
| R06 | 比例转换 | 两种模式、块/尺寸/引线、实际一次 Undo | PARTIAL_OLD_EVIDENCE / RETEST | `tasks/TARGETED_RETEST_AFTER_GEMINI_AUDIT_20260908.md` |
| R07 | 图片/外参 | Model/Paper OLE、删原图后重开、DWG Xref Bind、文件夹多 DWG | PARTIAL_OLD_EVIDENCE / RETEST | `tasks/REFERENCE_EMBED_V2_WINDOWS_SMOKE_20261026.md` |
| R08 | MCP | stdio 74 / AutoCAD 37 / Civil 67（Civil 缺环境写 NOT_TESTED）、自愈 | NOT_TESTED_CURRENT_DELTA | `tasks/TARGETED_RETEST_AFTER_GEMINI_AUDIT_20260908.md` |
| R09 | Revit Exchange | 真实 GUI vs MCP、MAP 11/11、可选 YangTools 真 Revit 重建 | CAD_MAP_OLD_EVIDENCE / RETEST | `tasks/CADTOOL_YANGTOOLS_ALIGNMENT_20260907_WINDOWS_E2E.md` |
| R10 | 安装/隔离 | 2024 安装隔离，正在运行的 2021 不受影响；Rollback | NOT_TESTED_CURRENT_DELTA | `docs/RELEASE_CHECKLIST.md` |

## 范围边界

- 任意闭合多段线大样裁剪、复杂块可选裁剪、删除边界外、直线变 Z 折断线属于**未实现需求**，不得在本轮表里默认 PASS。
- 没有 Civil 3D/Revit 宿主的测试机，对应项目只能写 `NOT_TESTED`，不得用静态 mock 替代。
- 某项失败后保存原样 evidence，不覆盖旧证据，也不要求整个矩阵从头复测；修复影响域重新执行。

## 状态更新格式

只追加有证据的条目：`ID | 测试 SHA | 主机/PID | PASS/FAIL/BLOCKED/NOT_TESTED | 证据 URL | 审核者`。执行者不要直接修改本表中的正式审核状态，提交到 Issue 留待审查。
