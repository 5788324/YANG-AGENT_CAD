# 当前任务 — G4 Release Contract 定点修复与 Windows 复核（2026-11-04）

**测试基线**：`b6ff1370b0ed48e5e50fec43d2a5a2e14ca5ec54`（真实 AutoCAD 2024 11 阶段测试）；本分支是其上追加的发行元数据修复。

## 修复点

1. `Package-Release.ps1` 内 `release-manifest.json.contract` 旧值 `68/31/61`，应为 `74/37/67`。
2. `Verify-Release.ps1` 此前只校验文件哈希/Bundle 结构，未校验上述契约与打包后 Python MCP 工具数量。
3. `verify_release_source.py` 需要在 Linux CI 即阻断旧值回归。

## Gemini 定点验收（无需重跑 R01–R10）

- 从本 PR 的**最终精确 Git SHA**重新检出源码并记录 `git status`、`git show`。
- Windows PowerShell 5.1：`Package-Release.ps1 -Years 2024 -Mode Quick`；若沿用旧 `b6ff137` 的经验证 DLL，应先核验该 DLL SHA 仍为 `880329CF7E5C464078686FE56B0BB7070B4C2F7B0CDA98B7B7F482A91771D9C8`，使用 `-SkipBuild`，且对新 ZIP 单独计算 SHA。
- 检查新包内 `release-manifest.json.contract == {stdio:74, plain:37, civil:67}`；实际包内 `server.py` 必须存在 74 个不同 Tool。
- 对新包运行 `Verify-Release.ps1 -PackageRoot <解压目录>`，记录 ExitCode=0；手动篡改 manifest 三个计数中的任意一个，再运行校验，应非零退出并明确拒绝（负向测试）。
- 如编译/部署 DLL 字节有变化，重做 R00 Build/Deploy/Loaded 定点复验。若仅 manifest / Verify 脚本变化，既有 R01–R10 数据不必重复。
- 只向 Issue #9 提交本次额外验证的最小原始日志、新旧工件 SHA、Git SHA。不要覆盖旧证据 ZIP，禁止自行合并 PR。

## 已确认不属于本轮

Civil 3D live 67 与 Revit Native E2E 两项仍为 `NOT_TESTED`。任意多段线裁切、删除边界外、折断线仍在 Backlog。

---

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
