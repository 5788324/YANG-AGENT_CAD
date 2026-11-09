# YangCAD

> **AI 综合源码审查快照（2026-11-09）**：本分支 `review/full-source-20261109` 是 **RC1 main + PR #13/#14/#15/#16/#17 的源码汇总**，仅用于其他 AI 同时查看所有候选功能。它**不是已编译、已加载、已集成测试或可正式发布的版本**，不要向 main 合并它。请先阅读 [AI 审查指南](docs/AI_REVIEW_SNAPSHOT_20261109.md) 和独立 PR 的测试状态。下文保留 RC1 时期的历史描述，提到「功能未实现」「PR #11 待合并」等属于历史语境；以此摘要和各 PR HEAD 为准。


**面向个人工程图纸的 AutoCAD / Civil 3D 可定制工具箱，支持本地 MCP / AI 自动化。**

> **AutoCAD 2024 Quick：`0.10.0-rc.1` 验收范围已通过（2026-11-05）**。这是 **RC 发行管理标识**；当前 DLL / Python MCP / PackageContents 内实际产品版本仍为 **`0.10.0` / `0.10.0-dev`**，为保留已验收 DLL 的字节一致性，本次不修改编译版本或运行时代码。**Civil 3D、Revit 原生重建、其他 AutoCAD 年份未获得本轮运行验收，绝非通用稳定发布。**

## 快速进入

使用方式：原生 CAD Ribbon 的 **YangCAD** 页签、可停靠 **工具箱 Palette** 和 CAD 命令行。插件不应抢占 AutoCAD 默认“常用”标签。

| 领域 | 入口 | 说明 |
|---|---|---|
| 图层 | `YGLAYER`、`YGLMERGE`、`YGLONOFF`、`YGLFREEZE`、`YGLLOCK` | 合并、显隐、冻结、锁定；外参只调整宿主图层状态 |
| 大样提取 | `YGDETAIL` | **现有 V1 是矩形裁切**，复杂跨界对象仍有跳过策略 |
| 比例 | `YGSCALE` | 标准 1:1 / 缩尺图纸；对象级 Undo 证据，仅受测类型已验证 |
| 引线 | `YGLEADERFIX` | 旧引线或连续线条重建 MLeader，支持单独 TEXT/MTEXT |
| 快捷键 | `YGKEYS` | PGP 缩写；Ctrl/Shift 组合键仍交 CAD CUI |
| 图纸/引用 | 工具箱 → Revit 图纸预处理、图片/外参内嵌 | 一键或选择文件夹；原图保留副本策略 |
| 自动化 | `YANGCAD_MCP` | stdio 74 / AutoCAD live 37；Civil 67 仅合同，未本轮实机 |

**可预期的限制**：R07 文件夹内嵌现场结果为 **2 PASS / 1 SKIP / 1 FAIL（PARTIAL）**，并非全部图纸成功；R06 标准 1:1 模式安全跳过受检 INSERT；CAD Exchange 的 11/11 MAP/ReaderVerifier 通过，**不是 Revit 原生 ElementId 重建通过**。任意闭合多段线裁切、删除边界外、Z 折断线仍在需求池，**未实现**。

## 0.10.0 AutoCAD 2024 RC 证据与源码

- **统一集成候选**：[PR #11](https://github.com/5788324/YANG-AGENT_CAD/pull/11) · `release/autocad2024-0.10.0-rc1`；整合此前 PR #4、#5、#6、#7、#8、#10，目标为 `main`，**Draft，未合并**。
- **受测源码**：`b6ff1370b0ed48e5e50fec43d2a5a2e14ca5ec54`；**发布契约修正基线**：`d5200c57a75b1fd228713dc67b606126a5c04924`。
- **已核验 Quick 发布 ZIP**：SHA-256 `ACA88A9440968411A2D7A014800AC70FE8E60C314ECBE936BBBC4037C5C79692`。
- **已验收 AutoCAD 2024 DLL**：SHA-256 `880329CF7E5C464078686FE56B0BB7070B4C2F7B7F482A91771D9C8`（558,080 字节）。
- [完整 RC 范围、回滚和限制](docs/RC_AUTOCAD2024_0.10.0.md) · [集成合并策略](docs/RC_MERGE_PLAN.md) · [Issue #9 G5 审计](https://github.com/5788324/YANG-AGENT_CAD/issues/9#issuecomment-6646760886) · [已核验 Drive 证据](https://drive.google.com/drive/folders/1bUkqFnOBmG6j5vz5z0DogLN30xZcAYUs)。

> **Quick 为个人使用的未签名安装渠道**，已验证的是特定 AutoCAD 2024 场景。新 RC 分支只有文档、索引与 CI 管理变更，不是重新编译的发布工件，也不宣称代码在所有宿主通过。

## 给新 AI / 新对话

优先读 [`AGENTS.md`](AGENTS.md) → [`docs/AI_START_HERE.md`](docs/AI_START_HERE.md) → [`docs/CURRENT_STATE.md`](docs/CURRENT_STATE.md) → [`docs/HANDOFF.md`](docs/HANDOFF.md) → [`tasks/CURRENT_TASK.md`](tasks/CURRENT_TASK.md)。

主要目录：`src/YangCAD.AutoCAD`（CAD/WPF/命令）、`src/YangCAD.Civil3D`（Civil）、`mcp`（Python MCP）、`workers/ImageEmbed`（OLE Worker）、`tools`（构建/封包/静态验证）、`references`（合法第三方功能参考资料，不含原插件二进制）。

## 本地静态检查

```bash
python tools/verify_source_manifest.py --complete
python tools/run_static_checks.py --output artifacts/ci-static-report.json
```

静态 PASS 不代替 Windows Build/Loaded、真实 DWG 对象读回与 Undo；参见 [`docs/QUALITY_GATES.md`](docs/QUALITY_GATES.md)。项目为非商业的个人工程工具，与 Autodesk、源泉设计、贱人工具箱均无官方关联。
