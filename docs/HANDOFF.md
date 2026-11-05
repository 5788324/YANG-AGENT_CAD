## 2026-11-05 最新接手事实

- `main` 已通过 PR #11 merge SHA `2f0980d4acd08b087d924b5c1f5a5bb18e8668a8` 包含 0.10.0 AutoCAD 2024 RC1；旧 PR #4–#10 归档，Issue #9 测试收口。
- 新功能从 `main` 开独立 `feature/geometry-edit-v2-20261105`（Issue #12），首个源码增量是 `YGBREAKLINE`，**Windows 未测试**。运行时代码已改变，之前 0.10.0 的 DLL SHA/Runtime PASS 不能迁移到这个分支。
- 本轮新功能文件：`BreaklineService.cs`、`BreaklineCommands.cs`、`docs/BREAKLINE_V1_20261105.md`；随后另行开发 Polygon QG 与 YGOUTSIDE。

# YangCAD 接手与工程边界（2026-11-05）

**当前管理版本**：`0.10.0-rc.1` / **AutoCAD 2024 Quick 特定范围 G5 GO**。现有程序集版本仍为 `0.10.0-dev`，这是为了保留与已验收 DLL 的一致性，**不是遗漏更新 DLL**。

## 唯一当前任务

查看 `tasks/CURRENT_TASK.md`：已将叠加 PR #4–#10 统一到 **[RC Draft PR #11](https://github.com/5788324/YANG-AGENT_CAD/pull/11)**，完整 diff/静态 CI/受测 Runtime 一致性已核；不自动 merge `main`，不新开发功能。

## 从哪里读代码

- Ribbon / Palette / ControlCenter：`src/YangCAD.AutoCAD/UI/`；用户拾取命令：`src/YangCAD.AutoCAD/Commands/`。
- 真实业务 Service：`src/YangCAD.AutoCAD/Services/Cad/`，GUI / 命令 / MCP 薄适配器共用。
- CAD document/transaction：`Infrastructure/CadTransactionRunner.cs`。
- MCP：`src/YangCAD.AutoCAD/Mcp/`、`mcp/yangcad_mcp/`；不能跨线程直接进 Autodesk API。
- Image OLE worker：`workers/ImageEmbed/`；Windows 打包：`tools/Build.ps1`、`tools/Package-Release.ps1`、`tools/Verify-Release.ps1`。
- 历史旧实现、协议：相应 `docs/*`；第三方参考 `references/*` 不允许混入未经授权 VLX 等。

## 完整证据索引

- 旧版本问题审查与实际修复：`https://github.com/5788324/YANG-AGENT_CAD/issues/9`。
- 已验收 AutoCAD 2024 源码：`b6ff1370b0ed48e5e50fec43d2a5a2e14ca5ec54`；Release 合同修复：`d5200c57a75b1fd228713dc67b606126a5c04924`。
- 已验收发行 ZIP SHA256 `ACA88A9440968411A2D7A014800AC70FE8E60C314ECBE936BBBC4037C5C79692`；Loaded/发行 DLL SHA256 `880329CF7E5C464078686FE56B0BB7070B4C2F7B7F482A91771D9C8`。
- CAD 原始测试证据 `https://drive.google.com/drive/folders/1AJI15SxQCXlPUXVXOMLg3-jN-wuOkMm0`；PR10 正反向 `https://drive.google.com/drive/folders/1bUkqFnOBmG6j5vz5z0DogLN30xZcAYUs`。

## 不能越界声明的事

- Civil 3D live 67 / Revit Native E2E 仍 NOT_TESTED；MAP + ReaderVerifier 11/11 不是 Revit native ElementId 实测。
- R06 对象级 Undo 恢复与 DWG 二进制恢复不同；R07 批处理是 2 通过 / 1 跳过 / 1 失败。
- `main` 已完成 0.10.0 AutoCAD 2024 范围合并，但不表示多宿主产品正式稳定发布。PR 合并策略见 `docs/RC_MERGE_PLAN.md`。
- 当前 AUTO CAD 2024 RC 不表示 2021/2022/2023/2025/2026/2027 均通过。

## 交接规则

每次提交后刷新 GitHub HEAD、提交内容、PR 状态与 CI；新功能改动要追加功能测试，不要混在已验收的 RC 里。需静态验证：`python tools/verify_source_manifest.py --complete`、`python tools/run_static_checks.py`，仍要区分 STATIC/WINDOWS。
