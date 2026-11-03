# 当前任务 — 0.10.0-dev 统一 Windows 实机验收

更新时间：2026-11-03；GitHub PR：[#8](https://github.com/5788324/YANG-AGENT_CAD/pull/8)。

## 当前状态

- 本轮已完成项目文档整理、第三方合法参考目录、Ribbon/Palette/关于页和跨语言版本统一。
- 本轮补充静态 GitHub CI、PR/Issue 模板、SHA manifest 校验、Gemini 统一验收流程和 R00–R10 业务验证矩阵。
- **STATIC_ONLY / WINDOWS NOT_TESTED**；不能因为静态 0 errors 或 CI PASS 自行宣布正式发布。
- `main` 仍是旧稳定基线；**当前候选在 `feature/product-foundation-v010-20261103` 的执行时最新完整 SHA**。
- PR #4→#8 是有序叠加的开发分支，父依赖详见 `docs/BRANCH_DEPENDENCIES.md`。

## Gemini 唯一任务

`tasks/GEMINI_WINDOWS_INTEGRATED_SMOKE_20261103.md`；先读 `docs/GEMINI_EXECUTOR_SOP.md`，再按 `docs/QUALITY_GATES.md` G0–G5 和 `docs/RUNTIME_TEST_MATRIX.md` R00–R10 分阶段测试。

- 首先 G0 基线、G2 Windows clean build 和 G3 Build/Deploy/Loaded；这是阻断型测试。
- 然后 Ribbon/Palette/关于、PGP 快捷键、图层/QG、引线、比例转换、图片及 Xref、MCP 和 Revit 链。
- 缺少 Civil 3D/Revit 环境要写 `NOT_TESTED`，不要伪造设备/结果。
- Gemini 上传 Google Drive 原始证据并在 GitHub Issue 留言；ChatGPT 独立复核，再决定是否修复、合并及发布。

## 不属于本轮已实现的需求

任意闭合多段线大样裁切、复杂对象可选裁切、删除边界外、Z 形折断线。只保留在产品 Backlog，不得计为本版验收 PASS。

## 常用检查

```bash
python tools/verify_source_manifest.py --complete
python tools/run_static_checks.py --output artifacts/ci-static-report.json
```

CI 检查未使用 Autodesk SDK，不会产生真实 CAD 加载或 Undo 证据。
