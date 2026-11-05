# YangCAD 0.10.0 RC1 — 运行验收矩阵（2026-11-05）

**范围**：Windows 10 x64 + AutoCAD 2024 Quick。R00–R10 的现场日志与 ChatGPT G5 审核：`https://github.com/5788324/YANG-AGENT_CAD/issues/9#issuecomment-6646760886`。下面的 PASS 是指**所列样例**；并非所有输入组合均成功。

| ID | 领域 | 已验范围 | 当前审查状态 |
|---|---|---|---|
| R00 | Git/Build/Deploy/Loaded | b6ff137；DLL SHA 880329CF... | **PASS** |
| R01 | Ribbon / Palette / About | 真实 GUI 截图、PID、路径 | **PASS** |
| R02 | PGP 快捷键 | 备份、冲突/编码、REINIT | **PASS** |
| R03 | 图层工具 | 合并、状态、读回、一次 Undo | **PASS（指定样例）** |
| R04 | 矩形大样 | 1×/2×、原图不改、一次 Undo | **PASS（指定样例）** |
| R05 | 引线重建 | MLeader + TEXT/MTEXT、拒绝不安全路径、Undo | **PASS（指定样例）** |
| R06 | 比例转换 | 标准 7/8、缩尺 8/8、对象属性 Undo | **PASS_WITH_SKIPS**；不是 DWG 字节级恢复 |
| R07 | 图片/Xref | OLE Model/Paper、删除图片后重开、Bind；文件夹 2/1/1 | **PASS_WITH_PARTIAL**；不是全量文件成功 |
| R08 | MCP | stdio 74、AutoCAD 37 与自愈；Civil 67 未运行 | **AUTOCAD_PASS / CIVIL_NOT_TESTED** |
| R09 | CAD→Revit Exchange | GUI / MCP MAP 11/11 + ReaderVerifier 11/11 | **CAD_SIDE_PASS / REVIT_NATIVE_NOT_TESTED** |
| R10 | Quick 安装与隔离 | 目标宿主活跃时阻断与非目标保护 | **PASS（指定样例）** |
| G4 | 发布包 Release Contract | 74/37/67、19/19 SHA、PS5.1 正向和 3 组负向 | **PASS**（d5200c5 + 最终 ZIP） |
| G5 | 独立证据复核 | 来源、SHA、真实原始证据 | **AUTOCAD2024_SCOPE_PASS** |

> 历史测试状态曾记作 `PARTIAL_OLD_EVIDENCE / RETEST`。该表不是这些历史证据的无条件继承；本次以 2026-11-04 独立核实的 R00–R10 原始证据为准。

## 明确未测试

- **Civil 3D live 67**：NOT_TESTED（Object Enabler 不等于 Civil 宿主）。
- **Revit Native E2E**：NOT_TESTED（不曾证明加载 Revit Addin 并返回 ElementId）。
- 其他 AutoCAD 年份：NOT_TESTED_CURRENT_RC。
- 闭合多段线任意边界、删除边界外、Z 折断线：NOT_IMPLEMENTED。

## 对新变更的要求

RC 分支仅文档/CI 增量不必重跑 R01–R10；**运行时代码、Assembly 或 MCP 包一旦改变，就必须依影响范围重新 Build/Loaded 并核对 DWG 与 Undo**。Gemini 只能提交原始证据，ChatGPT 进行 G5 独立判定。
