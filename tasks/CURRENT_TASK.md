# 当前任务 — YangCAD 0.10.0 RC1 集成审查（2026-11-05）

## 唯一范围

将 PR #4–#10 的已验收代码由一个 **docs-only RC 集成分支**向 `main` 发起 Draft PR。实际运行内容继承受测 `b6ff137` + Release 修复 `d5200c5`，不重新编译 DLL、不扩展 Civil/Revit/其他年份的 GO。

## 本轮 DoD

- [x] 已完成 G5 AutoCAD 2024 指定范围审查：`https://github.com/5788324/YANG-AGENT_CAD/issues/9#issuecomment-6646760886`。
- [x] Release Contract 74/37/67、19/19 SHA、PS5.1 正向/负向证据核验。
- [x] RC 源码基点为 `d5200c57a75b1fd228713dc67b606126a5c04924`；不重新实现 PR #4–#10 的代码。
- [x] GitHub 建立 `release/autocad2024-0.10.0-rc1` 和 [Draft 集成 PR #11](https://github.com/5788324/YANG-AGENT_CAD/pull/11) → `main`，完整 PR 107 个变更文件、无冲突。
- [x] GitHub CI [41948088165](https://github.com/5788324/YANG-AGENT_CAD/actions/runs/41948088165) SUCCESS；SHA manifest 267/267；RC 与 d520 **Runtime 源码 0 差异**。
- [ ] 等待对 PR #11 的合并决定；未获新明确指令前不合并 `main`、不打跨宿主 stable 标签。
- [ ] 在集成完成后逐项整理堆叠 PR，避免重复合并/重复发布。

## 源码及交接

文档：`docs/RC_AUTOCAD2024_0.10.0.md`、`docs/RC_MERGE_PLAN.md`、`docs/CURRENT_STATE.md`、`docs/AI_START_HERE.md`、`docs/RUNTIME_TEST_MATRIX.md`。CI：`python tools/verify_source_manifest.py --complete` / `python tools/run_static_checks.py`。

## NOT_TESTED / NOT_IMPLEMENTED

Civil 3D live 67 / Revit Native E2E / 其他 AutoCAD 年份均 **NOT_TESTED_CURRENT_RC**。未实现：任意闭合多段线提取、跨界复杂对象可选裁切、删除边界外、直线 Z 折断线。
