## 2026-11-03 — YangCAD 自主功能库与快捷键面板（STATIC ONLY）
- 按个人使用需求参考源泉/贱人工具箱的操作体验，建立自主功能库，不照搬第三方源码/资产，也不一次复刻整个插件。
- 新增 `YGKEYS` 快捷键面板（控制中心 → 快捷键）：按当前 AutoCAD acad.pgp 写独立 YangCAD 标记区块，冲突检测/显式覆盖、原文件备份、REINIT 生效；组合键使用 CAD CUI。
- Ribbon 创建保留原有选中 Tab，不在插件加载时主动把 YangCAD 变为默认选项卡。
- 当前仅静态 PASS，Windows AutoCAD/Civil 3D 2024 PGP/REINIT/Ribbon 真实验证尚未进行。
- 功能规划：`docs/FEATURE_LIBRARY_YQARCH_JRCAD_20261103.md`；实机任务：`tasks/SHORTCUT_PANEL_NATIVE_RIBBON_WINDOWS_SMOKE_20261103.md`。


## 2026-11-03 — 图层工具 + 切割提取大样（静态候选）

- Ribbon 新增 `图层工具` 和 `提取大样`，操作入口尽量简单。
- LayerQuickService：模型/布局/本地块定义/块属性 LayerId 合并；显隐/冻结/锁定支持宿主 DWG 中的外参依赖层，但不改 Xref 源文件；当前层不可隐藏/冻结，外参依赖层不可直接合并。
- `YGLAYER/YGLMERGE/YGLONOFF/YGLFREEZE/YGLLOCK` + 一个 WPF 小窗；隐藏或冻结的层仍可在列表中恢复；嵌套点选区分内部对象层与块/外参插入层。
- `YGDETAIL`：矩形框+目标放置点+可选放大倍率；LINE/ARC/CIRCLE/普通 LWPOLYLINE 直线与弧段做精确裁切；复杂跨界对象及含属性/动态块/关联 Hatch 安全跳过；不删除原图对象。
- MCP 新增 `cad.layer.merge/visibility/freeze/lock` + `cad.detail.extract`，默认 dry-run；合约 74 / 37 / 67，仅静态。
- 旧的图片内嵌/比例转换/引线重建仍未 Windows 实机验收，不可宣称 Runtime GO。
- 限制：当前不能运行 dotnet / Autodesk SDK；Windows 2024 build、Xref nested pick、Undo 与裁切实体 readback 待 `tasks/LAYER_TOOLS_QG_WINDOWS_SMOKE_20261103.md`。

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

## 2026-10-26 Revit Prep + 引线修复 V1 (candidate)
- 简化 Revit 图纸预处理；一键输出，技术参数折叠；保留原 Exchange 1.0。
- 新增 YGLEADERFIX 和 cad.leader.rebuild；仅单条链路、用户显式配对文字，失败不擦除。
- 状态 STATIC ONLY / AUTOCAD 2024 RUNTIME NOT TESTED。详见 docs/REVIT_PREP_SIMPLE_UI_LEADER_REPAIR_V1_20261026.md。
