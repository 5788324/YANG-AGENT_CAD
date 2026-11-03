# YangCAD 质量门禁（开发候选与发布）

2026-11-03 · 当前主验证对象：`feature/product-foundation-v010-20261103`，**以执行时 Git SHA 为唯一标识**。

| 门禁 | 目的 | 需要的证据 | 通过意味着什么 |
|---|---|---|---|
| G0 基线 | 不拼接新旧源码、不测错包 | 分支、完整 commit、源码哈希、基线差异 | 测试输入唯一 |
| G1 静态 | 代码/接口/文档格式和契约检查 | GitHub Actions、`run_static_checks.py` 输出、manifest | 仅 STATIC PASS |
| G2 Windows Build | Autodesk SDK 真编译 | 清理/restore/build 命令、binlog、版本、DLL SHA | 当前源码可编译 |
| G3 部署加载 | 确认执行的是本次编译的代码 | Deploy SHA、AutoCAD PID、Loaded DLL Path/SHA、CAD 版本 | 版本一致且宿主加载 |
| G4 业务实测 | 真正完成 CAD 操作 | 受控 DWG、对象 Handle、before/after、截图、Undo、必要的 GUI/MCP 记录 | 指定场景通过 |
| G5 独立审查 | 防止不实 PASS 和隐藏修改 | 逐项引用 G0–G4 原始证据、未测项及失败项 | 可讨论合并/RC |

## 强制规则

1. CI 的绿色勾只证明 G1；不能自动把 `STATIC_ONLY` 改为 `RUNTIME_PASS`。
2. 任一源码修改必须产生新 Git SHA，Build/Deploy/Loaded/运行证据都绑定新 SHA；不能复用旧候选的加载证据。
3. 同一项目已有历史 GO 不等于当前 0.10.0-dev 新增业务 GO。
4. 保留 `NOT_TESTED`、`BLOCKED`、`FAIL`，未执行不允许写 `PASS`。
5. Native Undo 要实际执行一次 AutoCAD `U`/Ctrl+Z 并验证对象 readback；仅用事务 abort 不是 Undo 测试。
6. GUI 调用必须有 GUI 自身的动作记录；把 RPC request id 写成 `gui_run` 不是 GUI 测试。
7. 文件处理：DWG 输入 SHA 变化即 FAIL；外参源、原图、布局不可被无意改写。
8. Revit E2E 必须提供 Revit 运行环境、YangTools 加载身份、API 结果/ElementId 等，CAD MAP 数量相等不能替代 Revit E2E。
9. Git 主分支只接受 G5 审核过的范围；部分通过时可拆 PR，不为凑版本强行全部放行。
10. 执行者（Gemini）可报告 `READY_FOR_REVIEW`，不可自行声明 `FINAL GO`。

## 证据落点

- GitHub：任务 Issue、开发 PR、源码、静态 CI、状态和审核意见。
- Google Drive：经脱敏的真实 DWG、截图、binlog、日志、重开验证文件及较大的原始证据。
- 在 Issue/PR 引用 `证据 URL + Git SHA + PID + 验收断言`，不把私人图纸提交公开仓库。
