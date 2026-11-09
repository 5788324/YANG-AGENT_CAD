# 当前任务 — AI 综合代码审查快照（2026-11-09）

当前分支 `review/full-source-20261109`（**只读审查源码，非 release**）。
- 来源基线 main `2f0980d4acd08b087d924b5c1f5a5bb18e8668a8`。
- 汇集 PR #13/#14/#15/#16/#17 的候选源代码，五个原 PR 仍 Draft。Gemini 测试 PR #16 时不要从本分支构建或污染其隔离测试。
- 优先读 `docs/AI_REVIEW_SNAPSHOT_20261109.md`，那里包含各 PR SHA、已解决的共享文件及静态/实机区分。
- 不允许默认推送 `main` 或将审查快照合并到稳定分支。发现问题按文件/行号写入审查报告。
- G1 静态扫描需要独立验证，AutoCAD 2024 本聚合程序集的 Build/Loaded/DWG/Undo 均 NOT_TESTED。
