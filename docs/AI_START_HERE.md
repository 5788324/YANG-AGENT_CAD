# YangCAD — AI / 新对话 60 秒接手

**代码唯一位置**：https://github.com/5788324/YANG-AGENT_CAD

## 1. 找正确代码

- 稳定回退分支：`main`，落后于正在开发的 0.10.0-dev。
- 当前集成测试基线：`feature/product-foundation-v010-20261103`（PR #8）；必须重新读取实时 HEAD SHA。
- PR 分层依赖参见 `docs/BRANCH_DEPENDENCIES.md`，不要选择旧 ZIP 作为开发源。

## 2. 读状态，不靠旧记忆

1. `docs/FEATURE_CATALOG.md` — 哪些功能真实实现/哪些仅计划
2. `docs/CURRENT_STATE.md` — 当前技术状态
3. `docs/RUNTIME_TEST_MATRIX.md` — 真机测试缺口
4. `tasks/CURRENT_TASK.md` — 当前任务
5. `docs/HANDOFF.md` — 架构和注意事项
6. `docs/QUALITY_GATES.md` — 如何判 PASS 和可发布
7. `AGENTS.md` — 开发规则

## 3. 分工和交付

- ChatGPT：源码开发、版本/PR 管理、证据独立审查。
- Gemini：Windows/AutoCAD 实机执行，任务在 `tasks/GEMINI_WINDOWS_INTEGRATED_SMOKE_20261103.md`；不自行宣布 FINAL GO。
- GitHub：源码、Issue、PR、静态 CI；Google Drive：DWG、截图、binlog、需要限制共享的原始证据。

## 4. 开工门禁

```bash
python tools/verify_source_manifest.py
python tools/run_static_checks.py
```

成功只代表静态检查通过；AutoCAD API 仍必须 Windows 真编译/真执行。

## 5. 不要主动开发未实现需求

闭合多段线裁切、复杂对象可选裁切、删除边界外、Z 形折断线属于需求池，未获得本轮开发授权前不能当作当前候选功能。
