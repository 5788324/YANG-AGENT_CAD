## 2026-11-06 — YGDL/YGDLW 双线管道 + 中轴固定改宽（源码候选）

- 独立分支 feature/dual-line-width-v1-20261106，基于已验收 RC1 main@2f0980d4acd08b087d924b5c1f5a5bb18e8668a8；与尚待 G5 的 PR #13/#14/#15 没有依赖，主线未改。
- 新命令 YGDL：双线/管道三线绘制，首次 1000 mm，成功后记忆宽度，Center/Left/Right，Miter/Bevel/Round，连续多组，W 调整下一组。YGDLW：选本插件边线或中心线按保存中轴对称改宽；旧插件两条匹配 LINE 可人工配对改宽。示例 600→1000，左右各外移 200 mm。
- 限制：WCS XY、同 Z、开放路径；旧插件复杂 Polyline/Arc 暂不自动配对。中心线及双线成组 XData 保存/读回。保守拒绝不能安全偏移的路径。
- 文档 docs/DUAL_LINE_V1_20261106.md；静态检查 tools/test_dual_line_policy.py；Gemini Windows tasks/DUAL_LINE_WINDOWS_SMOKE_20261106.md。
- 状态：STATIC ONLY，AutoCAD 2024 实际 Build/NETLOAD/绘图/原生 _.U 均 WINDOWS_NOT_TESTED，不得继承其他 PR 的 DLL/验证结果；不合并 Draft PR。

---

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
