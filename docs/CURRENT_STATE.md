> **2026-09-08 Gemini 实机证据独立复核**：Gemini 的 `EVIDENCE COMPLETE` 不被接受；确认存在 PID/Python/SHA 不一致、未实际 GUI 却以 RPC 冒充 GUI、未测 PaperSpace 却宣称 PASS、Leader/MLeader 覆盖夸大、Undo/Revit E2E 证据不足等问题。当前改为 `PARTIAL RUNTIME EVIDENCE / TARGETED RETEST REQUIRED / FINAL NO-GO`。本候选从 2026-09-07 clean candidate 重建，只吸收经 ChatGPT 独立审查的必要 Windows 修复；Gemini post-fix ZIP 不作为新基线。详见 `docs/CHATGPT_GEMINI_EVIDENCE_AUDIT_20260908.md` 与 `docs/TARGETED_RUNTIME_FIX_REVIEW_20260908.md`。

> **2026-09-07 急用功能：图纸比例转换 V1（STATIC DEVELOPMENT）**：新增 Ribbon `比例转换` / 命令 `YGSCALE` / MCP `cad.scale.convert`。支持 `standard_1to1`（真实 1:1，几何不变、注释倍率 New/Old）与 `scaled_drawing`（非 1:1，几何倍率 Old/New、纸面注释恢复）两种模式。第一版对 Annotative/Xref/Underlay/Civil/AEC/Proxy 安全跳过；标准模式普通块因无法判定几何块/注释块而先跳过。当前 MCP additive contract 变为 stdio **68** / AutoCAD **31** / Civil 3D **61**。尚未 Windows Build/Runtime/Undo 实机证明。

> **2026-09-07 CADTool ↔ YangTools 业务对齐**：`project.revit.prepare` 与 `project.image.embed` 已补产品级 MCP Adapter，GUI/MCP 共用业务 Service/Request/Result；当前静态合同调整为 stdio **67** / AutoCAD live **30** / Civil 3D live **60**。Exchange 继续锁定 `yangcad-rvt-exchange/1.0`，`LWPOLYLINE vertices[].point + bulge` 不回退。Windows Build/Runtime/真实 11/11 E2E 尚需独立验收。

# CURRENT STATE

更新时间：2026-09-01

## Product Surface

Ribbon：

- **MCP**
- **工具箱**
- **比例转换**
- **控制中心**

Toolbox GUI 仅：

- `project.revit.prepare` / Revit 图纸预处理
- `project.image.embed` / 批量图片内嵌

控制中心仅：

- 系统设置（主题）
- 关于

System / Planning / legacy CAD/Civil GUI cards 已从可见 Toolbox 移除。对应 backend/MCP Tool 保留，以维护 MCP core contract。

## Contract

- MCP stdio：**68**
- AutoCAD live：**31**
- Civil 3D live：**61**

## Current static validation — 2026-09-07

- `verify_source.py`：`cs=67 xaml=9 python=21 themes=3 tools=68 xml=15 errors=0`
- `verify_release_source.py`：`errors=0`
- Scale Converter：`36/36 PASS`
- MCP Product Coverage：`25/25 PASS`
- RVT Exchange v1 / YangTools 3.1.36 interop：`56/56 PASS`
- MCP config/self-heal：`28/28 PASS`
- Image Embed：`31/31 PASS`
- Extension regression：`12/12 PASS`
- Civil API compatibility：`8/8 PASS`
- Quick installer：`10/10 PASS`
- Toolbox Foundation：`16/16 PASS`
- Toolbox Interaction：`13/13 PASS`
- UI Surface：`13/13 PASS`
- Theme UI：`13/13 PASS`
- Python compileall：PASS
- Windows Build/Runtime/Undo：NOT_TESTED

## MCP

**STATIC CLOSEOUT PASS / WINDOWS TARGETED RETEST REQUIRED**

已修：

- 独立 Ribbon MCP Window；
- Control Center / Toolbox 不再重复显示 MCP；
- Gateway 状态使用 `IsRunning`；
- 31/61 host-specific self-check + critical tools；
- launcher-only 检查升级为 whole-runtime health check；
- partial/stale runtime 触发 embedded self-heal；
- MCP Python dependency 要求 major v2。

SOP：`tasks/FEATURE_INFRA_MCP_CONFIG_001_WINDOWS_RETEST.md`

## Revit 图纸预处理

既有核心闭环：**CLOSED / GO / PASS**

当前 0.9 closeout delta：**STATIC PASS / WINDOWS REGRESSION REQUIRED**

本轮补强：

- YangTools 3.1.36 原生重建对接：`DxfType` 正式字段、`units/origin/Traits/counts/unsupported`、稳定 Dimension/Hatch/Leader 语义；
- 固定 fixture：`tools/fixtures/RVT_EXCHANGE_V1_YANGTOOLS_FIXTURE.json`；
- 当前该 interop delta 仅 STATIC PASS，必须重新 AutoCAD 导出并由 YangTools Reader 做数量对账后才能继承最终 GO；
- Available 状态；
- Exchange layer On / Thawed / Unlocked / Plottable；
- 动态块引用计数和 warning/report；
- 不修改匿名动态表示，不夸大支持边界。

## 批量图片内嵌

**STATIC HARDENED / WINDOWS 2024 OLE SMOKE PENDING**

静态门禁已通过；最终必须在 AutoCAD 2024 证明：

- `PASTECLIP` 稳定产生 `AcDbOle2Frame`；
- ModelSpace/PaperSpace 落点正确；
- 移走源图后关闭/重开 DWG 仍可见；
- source SHA 不变且 output 不覆盖。

SOP：`tasks/FEATURE_PROJECT_IMAGE_EMBED_001_WINDOWS_SMOKE.md`

## 图纸比例转换 V1

**IMPLEMENTED / STATIC PASS / WINDOWS AUTOCAD 2024 TEST REQUIRED**

- Ribbon：比例转换；命令：`YGSCALE`；MCP：`cad.scale.convert`；
- `standard_1to1`：几何不变，注释倍率 `New/Old`；
- `scaled_drawing`：几何倍率 `Old/New`，纸面注释属性恢复；
- 安全跳过 Annotative/Xref/Underlay/Civil/AEC/Proxy；标准模式普通块首版跳过；
- 不改全局 DimStyle/TextStyle/MLeaderStyle；
- 目标一个外层事务 / 一次 Ctrl+Z，必须由 Windows 实机证明。

详见 `docs/SCALE_CONVERTER_V1_20260907.md`。

## Next

使用 2026-09-08 clean targeted-retest candidate，只补 Leader/MLeader + 真 Ctrl+Z、真实 GUI↔MCP、Image Model+PaperSpace+移源重开、MCP self-heal raw evidence；YangTools/Revit 若无真实 Revit 证据则保持 NOT_TESTED。


2026-10-26: Reference Embed V2 static candidate; see docs/REFERENCE_EMBED_V2_REVIEW_20261026.md. Windows runtime still required.
