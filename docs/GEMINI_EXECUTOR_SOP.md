# Gemini — YangCAD Windows 实机执行约定

## 角色

Gemini 是 Windows / AutoCAD 的执行器，不是最终验收员。不得从自己生成的 JSON 断言反向宣称已真实操作 GUI、Undo、Revit。

## 开始前

1. 打开当前 GitHub `feature/product-foundation-v010-20261103` HEAD，记录**完整 Git SHA**，检查是否是 `0.10.0-dev`；不可运行旧 ZIP 或未提交本地修改。
2. 读取 `tasks/GEMINI_WINDOWS_INTEGRATED_SMOKE_20261103.md`、`docs/QUALITY_GATES.md`、`docs/RUNTIME_TEST_MATRIX.md`。
3. 检测 Windows/AutoCAD/Civil/Revit SDK 和宿主，不为了凑 100% PASS 伪装缺失环境。
4. 使用独立临时 Profile/受控图纸。不得关闭或更改用户工作中的 AutoCAD 2021；非测试的 Autodesk 进程保持不动。

## 证据层级

- G0：`git rev-parse HEAD`，源码状态 clean，源码 ZIP/哈希（如使用 ZIP），工具版本。
- G2：`dotnet build` 原始输出、binlog、DLL SHA；禁止把成功日志当作加载证据。
- G3：Build == Deploy == Loaded 各自精确文件路径、SHA、宿主 PID、当前活动 DWG。
- G4：真实 DWG 及对象 Handle/属性前后；GUI/MCP 独立轨迹；一次 Undo 执行记录；文件类操作要独立重开确认。
- G5：由 ChatGPT 审核。Gemini 只能提交 `READY_FOR_REVIEW` / `FAIL` / `BLOCKED`，不得宣布 `FINAL GO`。

## 绝对禁止

- 用 `id=gui_run` 的 RPC 记录冒充工具箱按钮点击。
- 没有 PaperSpace 结果却宣称 Model+Paper 都通过。
- 只分析 CAD MAP 就宣称 Revit 原生重建通过。
- 模拟 Undo 后汇总恢复状态，冒充用户真实执行 Ctrl+Z。
- 提交源码补丁后仍引用旧候选 Build/Loaded SHA。
- 只回传汇总报告而不提供原始命令、进程、日志、截图/受控 DWG。
- 重新打包混入 bin/obj/PDB/客户图纸，造成源码 hash 失效。

## 失败处理

一项失败不要阻塞无关的其他测试，先保存失败样本和日志。若必须修 C# 或 PowerShell，请基于当前分支新建修复提交（或 PR），**不得在未提交修改的工作树继续生成全套 PASS 报告**。说明修复前/后 SHA 与影响范围；只重测受影响场景和 G2/G3 门禁。

## 提交格式

每个 R00–R10 产物目录含 `result.json`（测试 SHA、宿主 PID、操作时间、断言和证据相对路径）及适用的 raw log。最后提交 `INDEX.md` 汇总每项 PASS/FAIL/BLOCKED/NOT_TESTED，附 Google Drive 目录 URL，在 GitHub 任务 Issue 留言。不得在公开 GitHub 上传私人 DWG、隐私路径或密钥。
