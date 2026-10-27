> **2026-09-08 Gemini 实机证据独立复核**：Gemini 的 `EVIDENCE COMPLETE` 不被接受；确认存在 PID/Python/SHA 不一致、未实际 GUI 却以 RPC 冒充 GUI、未测 PaperSpace 却宣称 PASS、Leader/MLeader 覆盖夸大、Undo/Revit E2E 证据不足等问题。当前改为 `PARTIAL RUNTIME EVIDENCE / TARGETED RETEST REQUIRED / FINAL NO-GO`。本候选从 2026-09-07 clean candidate 重建，只吸收经 ChatGPT 独立审查的必要 Windows 修复；Gemini post-fix ZIP 不作为新基线。详见 `docs/CHATGPT_GEMINI_EVIDENCE_AUDIT_20260908.md` 与 `docs/TARGETED_RUNTIME_FIX_REVIEW_20260908.md`。

> **2026-09-07 急用功能：图纸比例转换 V1（STATIC DEVELOPMENT）**：新增 Ribbon `比例转换` / 命令 `YGSCALE` / MCP `cad.scale.convert`。支持 `standard_1to1`（真实 1:1，几何不变、注释倍率 New/Old）与 `scaled_drawing`（非 1:1，几何倍率 Old/New、纸面注释恢复）两种模式。第一版对 Annotative/Xref/Underlay/Civil/AEC/Proxy 安全跳过；标准模式普通块因无法判定几何块/注释块而先跳过。当前 MCP additive contract 变为 stdio **68** / AutoCAD **31** / Civil 3D **61**。尚未 Windows Build/Runtime/Undo 实机证明。

> **2026-09-07 CADTool ↔ YangTools 业务对齐**：`project.revit.prepare` 与 `project.image.embed` 已补产品级 MCP Adapter，GUI/MCP 共用业务 Service/Request/Result；当前静态合同调整为 stdio **67** / AutoCAD live **30** / Civil 3D live **60**。Exchange 继续锁定 `yangcad-rvt-exchange/1.0`，`LWPOLYLINE vertices[].point + bulge` 不回退。Windows Build/Runtime/真实 11/11 E2E 尚需独立验收。

# YangCAD

**个人 AutoCAD / Civil 3D 工具 + MCP / AI 自动化核心**

当前阶段：`0.9.0-dev / UI + MCP Closeout`

## 当前产品入口

Ribbon 当前保留 4 个一级入口：

1. **MCP** — YangCAD AI/MCP 核心连接、诊断与权限；
2. **工具箱** — 只保留两个实际业务功能；
3. **比例转换** — 急用生产功能，支持 1:1 实尺寸 / 缩尺绘图双模式；
4. **控制中心** — 只负责系统主题与关于信息。

### 工具箱当前仅保留

- `project.revit.prepare` / **Revit 图纸预处理** — 历史核心闭环 `CLOSED / GO / PASS`；本轮 0.9 收尾 delta `STATIC PASS / WINDOWS REGRESSION REQUIRED`
- `project.image.embed` / **批量图片内嵌** — `STATIC HARDENED / WINDOWS 2024 OLE SMOKE PENDING`

旧 CAD/Civil 图形化卡片、系统菜单、规划菜单、参数面板自检等不再出现在 Toolbox UI。其底层 Service / MCP Tool 继续保留，因为 MCP 是产品核心能力，不能通过清理 GUI 破坏 MCP backend；本轮新增产品 MCP 后当前合同为 68/31/61。

## MCP Core

当前 additive Contract：

- MCP stdio：**68**
- AutoCAD live：**31**
- Civil 3D live：**61**

MCP 现在拥有独立 Ribbon 按钮和独立窗口。控制中心、Toolbox 不再重复提供 MCP 状态入口。

当前静态加固包括：

- `LocalGateway.IsRunning` 真运行状态，而不是仅凭 Port；
- AutoCAD/Civil 自检分别要求 31/61 live tools，并检查关键 Tool；
- MCP 配置继续 token-free + PID-bound；
- 安装目录完整 runtime 健康检查，不再只检查 launcher；
- launcher 存在但 Python runtime 残缺/陈旧时也会走 embedded self-heal；
- Python MCP runtime 要求 MCP package major version = 2。

Windows 复测 SOP：`tasks/FEATURE_INFRA_MCP_CONFIG_001_WINDOWS_RETEST.md`

## Revit 图纸预处理

RVT Exchange v1 的既有核心流程已完成 AutoCAD + Revit 2024 实机闭环；本轮又修改了图层状态与动态块报告，因此当前 0.9 candidate 仍需一次 Windows 定点回归后才能继承最终 GO。

```text
DWG (source read-only)
→ Wblock detached clone
→ *_RVT.dwg
→ *_RVT_MAP.json
→ *_RVT_REPORT.json
```

当前收口补强：

- **2026-09-01 YangTools 3.1.36 对接增量：STATIC PASS / AUTOCAD + REVIT E2E RETEST REQUIRED**；
- `DxfType` 锁为正式实体类型字段；MAP 新增 `units / origin / Traits / counts / unsupported`；
- Linear/Aligned Dimension 固定 `DimLinePoint / XLine1Point / XLine2Point`；
- MLeader/Leader 固定 `arrow_to_text` 顶点语义；Hatch 常用 Curve2d 输出稳定 LINE/ARC 字段；
- 新增 `tools/fixtures/RVT_EXCHANGE_V1_YANGTOOLS_FIXTURE.json`，用于 CAD 导出与 YangTools Reader 对账；
- Toolbox 状态由 Experimental 修正为 Available；
- 生成/复用的 RVT Exchange 图层在副本中强制 On / Thawed / Unlocked / Plottable；
- 动态块引用会被检测并写入报告警告，不假装匿名动态表示已被安全重写。

规范：`docs/RVT_EXCHANGE_V1.md`

## 批量图片内嵌

`project.image.embed` 使用独立 PowerShell/COM AutoCAD worker，将顶层外链 RasterImage 转成内嵌 OLE 输出副本。

当前状态仍是：

> **STATIC HARDENED / WINDOWS 2024 OLE SMOKE PENDING**

静态门禁已覆盖 source SHA、no-overwrite、PaperSpace、status replace、stray OLE 清理、fail-closed 等；真正 `PASTECLIP → AcDbOle2Frame → 移走原图后重开仍可见` 仍必须在 AutoCAD 2024 Windows 实机证明。

SOP：`tasks/FEATURE_PROJECT_IMAGE_EMBED_001_WINDOWS_SMOKE.md`

## 图纸比例转换（YGSCALE）

状态：**IMPLEMENTED / STATIC PASS / WINDOWS AUTOCAD 2024 TEST REQUIRED**

- `standard_1to1`：真实几何保持 1:1，只按 `New/Old` 调整非注释性制图显示尺度；
- `scaled_drawing`：几何按 `Old/New` 真缩放，文字/标注/引线/Hatch/线型纸面属性尽量恢复；
- 标准模式普通块、Annotative、Xref、Underlay、Civil/AEC/Proxy 首版安全跳过；
- GUI / MCP 共用 `ScaleConversionService`；MCP Tool：`cad.scale.convert`，默认 dry-run；
- 不修改全局 DimStyle / TextStyle / MLeaderStyle。

说明：`docs/SCALE_CONVERTER_V1_20260907.md`  
Windows 验收：`tasks/SCALE_CONVERTER_V1_WINDOWS_SMOKE_20260907.md`

## UI / Theme

三主题继续保留：

- 墨曜 · Obsidian
- 月瓷 · Porcelain
- 雾蓝 · Mist

Shared Styles 已补齐深色导航 ListBox/ListBoxItem、ComboBoxItem、Separator、ScrollBar、Result TextBox 等主题资源，修复 Obsidian 下默认 WPF 白底/黑字漏出的 UI 问题。

## 开发规则

所有 AI 开发继续遵循：`docs/AI_FEATURE_DEVELOPMENT_STANDARD.md`

项目在本轮 Windows 收口通过后回到 **ON-DEMAND DEVELOPMENT**：真实工作中出现需求时，一次增加 1–3 个功能，不为填满分类而批量堆工具。

### Revit 预处理与引线修复（待 Windows 实机验收）

工具箱 → Revit 图纸预处理：一键生成 RVT DWG/MAP/REPORT；高级参数默认折叠。

`YGLEADERFIX`：选择旧式 Leader / 炸开的连续 Line/开放 Polyline → 指定独立 TEXT/MTEXT（可选）→ 合成为原生 MLeader。`cad.leader.rebuild` 提供 MCP dry-run/real，共用 LeaderRepairService。参见 `docs/REVIT_PREP_SIMPLE_UI_LEADER_REPAIR_V1_20261026.md`。
