> **2026-09-08 Gemini 实机证据独立复核**：Gemini 的 `EVIDENCE COMPLETE` 不被接受；确认存在 PID/Python/SHA 不一致、未实际 GUI 却以 RPC 冒充 GUI、未测 PaperSpace 却宣称 PASS、Leader/MLeader 覆盖夸大、Undo/Revit E2E 证据不足等问题。当前改为 `PARTIAL RUNTIME EVIDENCE / TARGETED RETEST REQUIRED / FINAL NO-GO`。本候选从 2026-09-07 clean candidate 重建，只吸收经 ChatGPT 独立审查的必要 Windows 修复；Gemini post-fix ZIP 不作为新基线。详见 `docs/CHATGPT_GEMINI_EVIDENCE_AUDIT_20260908.md` 与 `docs/TARGETED_RUNTIME_FIX_REVIEW_20260908.md`。

> **2026-09-07 YGSCALE urgent feature**：CAD→Revit/MCP E2E 暂停但改动保留；当前新增 `比例转换 / YGSCALE / cad.scale.convert`，双模式 `standard_1to1` 与 `scaled_drawing`。静态合同 68/31/61。下一步把本功能与既有对齐改动一起交 Gemini Windows/AutoCAD 2024 测试，Gemini 只交证据，FINAL GO 由 ChatGPT。

> **2026-09-07 急用功能：图纸比例转换 V1（STATIC DEVELOPMENT）**：新增 Ribbon `比例转换` / 命令 `YGSCALE` / MCP `cad.scale.convert`。支持 `standard_1to1`（真实 1:1，几何不变、注释倍率 New/Old）与 `scaled_drawing`（非 1:1，几何倍率 Old/New、纸面注释恢复）两种模式。第一版对 Annotative/Xref/Underlay/Civil/AEC/Proxy 安全跳过；标准模式普通块因无法判定几何块/注释块而先跳过。当前 MCP additive contract 变为 stdio **68** / AutoCAD **31** / Civil 3D **61**。尚未 Windows Build/Runtime/Undo 实机证明。

> **2026-09-07 CADTool ↔ YangTools 业务对齐**：`project.revit.prepare` 与 `project.image.embed` 已补产品级 MCP Adapter，GUI/MCP 共用业务 Service/Request/Result；当前静态合同调整为 stdio **67** / AutoCAD live **30** / Civil 3D live **60**。Exchange 继续锁定 `yangcad-rvt-exchange/1.0`，`LWPOLYLINE vertices[].point + bulge` 不回退。Windows Build/Runtime/真实 11/11 E2E 尚需独立验收。

# HANDOFF

更新时间：2026-09-01

## Baseline

当前代码基线是 `0.9.0-dev / UI + MCP Closeout`。

### 可见产品入口

```text
YangCAD Ribbon
├─ MCP
├─ 工具箱
├─ 比例转换
└─ 控制中心
```

Toolbox 只允许显示：

- Revit 图纸预处理
- 批量图片内嵌

控制中心只允许显示：

- 系统设置
- 关于

不要重新加入 System / Planning / legacy CAD/Civil Toolbox 卡片；如果未来有新 GUI 需求，必须由真实工作任务重新立项。

## Important architecture boundary

“MCP 是大核心”不等于删除旧 CAD/Civil backend。

旧 GUI 卡片已经退休，但对应 Service / RuntimeHost / MCP Tool 必须保留，否则会破坏：

- stdio 68
- AutoCAD 31
- Civil 3D 61

## 2026-09-01 closeout fixes

### UI

- Obsidian 深色主题补齐导航 ListBox/ListBoxItem 等默认 WPF 样式；
- ComboBoxItem / Separator / ScrollBar / Result TextBox 统一主题；
- Toolbox 去除分类导航、System、Planning、Recent/Favorites/Search 等；
- Toolbox 路径参数支持原生文件/文件夹选择；
- 结果区可滚动、复制。

### MCP

- 独立 `McpWindow`；
- 真 `Gateway.IsRunning`；
- AutoCAD 31 / Civil 61 自检；
- critical tool presence 检查；
- installed runtime whole-tree integrity check；
- partial/stale runtime → embedded self-heal；
- launcher dependency 要求 MCP package major v2。

### Revit / RVT Exchange

- 2026-09-01 对接 YangTools 3.1.36：正式实体字段固定为 `DxfType`；MAP 增加 `units/origin/Traits/counts/unsupported`；
- Linear/Aligned Dimension 固定三点角色 `DimLinePoint/XLine1Point/XLine2Point`；Leader 顶点固定 `arrow_to_text`；Hatch 常用边界固定 LINE/ARC payload；
- 新增 YangTools 对接 fixture；此增量尚需 AutoCAD 真实导出 + YangTools Reader 端到端对账，不能直接继承历史 Revit GO；
- `project.revit.prepare` = Available；
- generated/reused Exchange layer 在 detached clone 中 On/Thawed/Unlocked/Plottable；
- dynamic block references 记录 warning/report；匿名表示仍不直接修改。

## Static gates

当前静态门禁：全部 PASS。

详见：`docs/UI_MCP_TOOLBOX_CLOSEOUT_20260901.md`

## Remaining Windows-only gates

1. Windows + AutoCAD/Civil 3D 2024 clean build/load 与三主题 UI smoke；
2. MCP partial-runtime self-heal 实机；
3. MCP copied config → stdio startup → `cad.status`；
4. Revit Exchange 同名冻结层 + dynamic block warning 定点回归；
5. Image Embed `AcDbOle2Frame` / PaperSpace / reopen-with-source-moved smoke。

未通过这些门禁前，不把本轮 0.9 closeout candidate 标成最终 Windows GO；Revit 的历史核心 PASS 仅作为既有证据，不替代本轮 delta 回归。
