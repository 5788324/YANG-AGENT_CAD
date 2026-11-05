> **开发分支新增说明（2026-11-05）**：`feature/detail-polygon-v1-20261105` 实现 `YGDETAIL` 的闭合直边多边形源码，基于已经合并 PR #11 的 `main@2f0980d`。原本文的“PR #11 尚待合并”叙述是历史快照，不能当作当前任务。
> 新代码标记 STATIC_ONLY / WINDOWS_NOT_TESTED；Windows 执行 `tasks/DETAIL_POLYGON_WINDOWS_SMOKE_20261105.md`。折断线 Draft PR #13 为**独立分支**，不可合并其运行时证据。

# YangCAD — 新 AI / 新对话 60 秒接手（2026-11-05）

仓库：`https://github.com/5788324/YANG-AGENT_CAD`。

## 先确认当前分支，不要测错版本

- **唯一 RC 集成入口**：[PR #11](https://github.com/5788324/YANG-AGENT_CAD/pull/11) · `release/autocad2024-0.10.0-rc1`；它是从 `d5200c57...` 派生的 docs-only 版本，用于审查全部叠加功能如何进入 `main`。
- 当前旧开发父链末端：`feature/product-foundation-v010-20261103` / PR #8，受测 `b6ff137...`。原先的 `feature/product-foundation-v010-20261103` 是 0.10.0-dev 测试基线，不是 RC 合并入口。
- 原始 G5 审核：`https://github.com/5788324/YANG-AGENT_CAD/issues/9#issuecomment-6646760886`；后续测试以 **完整 Git SHA** 为准。

## 快速阅读顺序

1. `docs/CURRENT_STATE.md` — 最新分层事实
2. `docs/RC_AUTOCAD2024_0.10.0.md` — 本 RC 的唯一可宣称范围与 SHA
3. `docs/RC_MERGE_PLAN.md` — PR #4→#10 依赖/合并纪律
4. `docs/FEATURE_CATALOG.md` — 哪些功能做了、哪些未实现
5. `docs/HANDOFF.md` — 工程代码入口和执行者分工
6. `docs/RUNTIME_TEST_MATRIX.md` — R00–R10 独立证据状态（保留历史标记 `PARTIAL_OLD_EVIDENCE` 作溯源，不得解读为当前没测）
7. `tasks/CURRENT_TASK.md` — 唯一当前任务
8. `AGENTS.md` / `docs/QUALITY_GATES.md` — 工程与审核硬门禁

## 保持证据与运行范围边界

- AutoCAD 2024 Quick：**G5 scope GO**，发布 ZIP SHA `ACA88A...`；DLL SHA `880329CF...`。
- Civil 3D 67 live Tools、Revit Native E2E、其他 AutoCAD 年份：**NOT_TESTED_CURRENT_RC**。
- GUI、MCP、批处理共用业务逻辑；不要从 MCP 后台线程直接修改 Autodesk DB。
- 不因 docs-only RC 分支多一个提交就宣称新 DLL 已重新构建。若改业务代码须重新 Build/Loaded/Undo。

## 任务与开发范围

本轮是**合并审查/文档管理**，不开发 Backlog：任意多段线裁切、复杂对象裁切开关、删除边界外、Z 折断线。Gemini 若需要复测按任务 Issue，ChatGPT 最终独立审核；勿自行把 RC 升级为多宿主正式发布。

静态命令：`python tools/verify_source_manifest.py --complete` 和 `python tools/run_static_checks.py`。
