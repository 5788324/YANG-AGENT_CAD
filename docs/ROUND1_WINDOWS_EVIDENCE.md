# TOOLBOX-001 Round 1 — Windows UI / Host Smoke Final Evidence

日期：2026-08-28  
结论：**Round 0 / Round 1 = CLOSED / GO / PASS**

## 输入 / 输出证据

Windows 实机候选输入：
`YangCAD_FULL_REPO_ROUND00_CLAUDE_GO_20260828.zip`

SHA-256：
`9838B979B8D71162E672985D415E2B484186637B317DFC2047C8E96081BB1FCC`

Windows 最终回传：
`YangCAD_FULL_REPO_ROUND01_WINDOWS_SMOKE_GO_20260828.zip`

SHA-256：
`CCFC2C50B6FCCC088822C457CB0ED4D858F09F80B4213D7216365EE6B7C459C0`

包内 `SHA256SUMS.txt`：
- 131/131 matched
- mismatch = 0

ChatGPT 独立复核：
- Outer SHA：MATCH
- internal SHA：MATCH
- static/policy gates：全 PASS
- 相对被测候选生产代码 diff：**仅 1 个文件**
  - `src/YangCAD.AutoCAD/Toolbox/ToolboxRegistry.cs`

## 唯一生产源码修复

原代码：

```csharp
Application.DocumentManager.MdiActiveDocument
```

由于同时引用：
- `Autodesk.AutoCAD.ApplicationServices.Core.Application`
- `System.Windows.Application`

Windows clean build 触发 `CS0104`。

修复为：

```csharp
Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument
```

只改 `CanExecute()` 与 `Execute()` 两处引用。

判定：
- 最小修复；
- 不改变业务语义；
- 不改变 Tool contract；
- 不改变 Runner / Transaction / MCP Core；
- Windows exact-year clean build 已证明有效。

## Static / Policy

- `verify_source.py`: cs=50 xaml=6 python=11 themes=3 tools=65 xml=12 errors=0
- `verify_release_source.py`: errors=0
- extension: 12/12 PASS
- quick install: 10/10 PASS
- Civil API compatibility: 8/8 PASS
- Toolbox foundation: 10/10 PASS

## Build Matrix

| Year | AutoCAD | Civil | Batch | external System.Text.Json.dll |
|---|---|---|---|---:|
| 2021 | PASS | N/A | PASS | 0 |
| 2022 | PASS | PASS | PASS | 0 |
| 2023 | PASS | PASS | PASS | 0 |
| 2024 | PASS | PASS | PASS | 0 |

全部 0 error / 0 warning。

三参数：
`_palette.AddVisual("工具箱", _view, true)`
在 exact-year Autodesk assemblies 全部编译 PASS。

## Runtime 2024 UI P0

PASS：
- Start Page 不自动弹 PaletteSet；
- stdio 65 / AutoCAD 28 / Civil 58；
- Toolbox open → close → reopen；
- Doc A → Doc B → close A；
- 七分类完整；
- Tool Card 非空；
- `system.mcp-status` 可执行；
- search 正常；
- Planned 不可执行；
- 墨曜 / 月瓷 / 雾蓝；
- Control Center MCP / 系统设置 / 关于；
- 复制版本信息 / 打开日志目录；
- unhandled exception = 0；
- token leak = 0。

## Runtime 2021

PASS：
- `/health=28`
- cad.status / document.info / entity.query
- `cad.line.create` real
- AutoCAD native `_.U`
- entity count baseline 1000 → real → Undo → 1000
- batch.status available
- batch.dwg.audit PASS
- source DWG SHA before=after
- Toolbox minimum UI PASS

## Runtime 2022 / 2023

PASS：
- AutoCAD / Civil exact-year host
- Toolbox open
- category + cards visible
- reopen
- 0 unhandled exception

## Final

> **TOOLBOX-001 Round 0 / Round 1 CLOSED / GO / PASS**

下一阶段：
**TOOLBOX-001 Round 2 — Interaction / ParameterPanel / Favorites / Recent / persistence**
