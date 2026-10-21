# YangCAD 图纸比例转换 V1

日期：2026-09-07  
状态：`IMPLEMENTED / STATIC PASS / WINDOWS AUTOCAD 2024 RUNTIME REQUIRED`

## 1. 产品入口

- Ribbon：`比例转换`
- AutoCAD 命令：`YGSCALE`
- MCP：`cad.scale.convert`
- MCP 默认：`dry_run=true`

本功能来自真实工程图比例转换需求。原始需求文档较完整；V1 以“急用、正确、可撤销、复杂对象安全跳过”为优先，不把首版扩成大型比例管理系统。

## 2. 两种模式

### 2.1 标准模式 `standard_1to1`

适用于模型空间按真实 1:1 尺寸绘制的图。

例如真实长度 1000 mm，CAD 中也是 1000。若出图比例从 1:5 改为 1:10：

```text
geometry_factor   = 1.0
annotation_factor = new_scale / old_scale = 10 / 5 = 2.0
```

规则：

- 几何位置、长度、半径不变；
- DBText / MText 字高按 `annotation_factor` 调整；
- Dimension 的纸面视觉参数按 `annotation_factor` 调整，但 `Dimlfac` 保持；
- Leader / MLeader 的文字、箭头、Dogleg 等纸面参数按 `annotation_factor` 调整；
- Hatch PatternScale 按 `annotation_factor` 调整；
- Entity LinetypeScale 按 `annotation_factor` 调整；
- Annotative 对象首版跳过，避免重复注释缩放；
- 普通 BlockReference 首版在标准模式跳过，因为无法安全判断它是实尺寸部件还是注释符号。

### 2.2 缩尺绘图模式 `scaled_drawing`

适用于历史详图/节点图：模型空间本身已经按出图比例缩小绘制。

例如实际 1000，在 1:5 图中画成 200；改为 1:10 时：

```text
geometry_factor   = old_scale / new_scale = 5 / 10 = 0.5
annotation_factor = 1.0
```

规则：

- 几何围绕用户基点按 `geometry_factor` 真正缩放；
- DBText / MText 字高恢复为转换前值；
- Dimension 定义几何跟随缩放；纸面视觉参数恢复；`NewDimlfac = OldDimlfac / geometry_factor`；
- Leader / MLeader 随几何缩放后恢复纸面文字/箭头等尺寸；
- Hatch PatternScale 恢复为转换前值；
- Entity LinetypeScale 恢复为转换前值；
- 普通 BlockReference 整体缩放，AttributeReference 字高尽量恢复。

## 3. V1 支持范围

### 直接几何缩放/标准模式保持几何

- LINE
- LWPOLYLINE
- POLYLINE（含 2D/3D Polyline）
- ARC
- CIRCLE
- ELLIPSE
- SPLINE
- REGION
- SOLID
- 3DSOLID

### 注释/制图对象

- DBText
- MText
- Dimension 派生对象（普通线性/对齐/半径/直径等由 AutoCAD 原生 Transform + 对象级 override 处理）
- Leader
- MLeader
- Hatch
- BlockReference（仅 `scaled_drawing`）
- AttributeReference（块内字高恢复）

## 4. V1 安全跳过

- PaperSpace / 非模型空间顶层对象
- Annotative 对象
- Xref
- RasterImage / PDF / DGN / DWF Underlay
- Civil 3D 原生智能对象
- AEC 对象
- Proxy / 第三方对象
- 标准 1:1 模式下的 BlockReference
- 其它未明确支持对象

一个对象失败或不支持不会要求整个任务失败；每个对象使用嵌套事务隔离，外层业务操作仍由一个 runner transaction 包住。

## 5. Undo 语义

目标：

```text
一次 YGSCALE / 一次 cad.scale.convert(real)
= 一个外层写事务
= 目标一次 Ctrl+Z 完整恢复
```

AutoCAD 官方支持嵌套 Transaction：内层可单独 Abort，最外层 Abort 会撤回全部内层已 Commit 修改。Windows 实机仍必须验证 Ctrl+Z 的实际命令栈行为。

## 6. UI 流程

```text
点击「比例转换」/ YGSCALE
→ 使用预选对象或框选对象
→ 弹出简洁窗口
→ 选择：标准模式 / 缩尺绘图模式
→ 当前比例 / 目标比例
→ 缩尺模式选择基点（图中拾取或 0,0,0）
→ 开始转换
```

预设比例：

`1:1 / 1:2 / 1:5 / 1:10 / 1:20 / 1:25 / 1:50 / 1:100 / 1:200 / 1:250 / 1:500`

下拉框可直接输入 `1:30`、`1:75` 或分母 `30`、`75`。

## 7. MCP 结构化契约

Tool：`cad.scale.convert`

主要输入：

```json
{
  "handles": ["1A2", "1A3"],
  "old_scale": 5,
  "new_scale": 10,
  "mode": "scaled_drawing",
  "base_point": [0, 0, 0],
  "preserve_text_paper_size": true,
  "preserve_dimension_paper_size": true,
  "preserve_leader_paper_size": true,
  "preserve_hatch_paper_scale": true,
  "preserve_linetype_paper_scale": true,
  "dry_run": true
}
```

Result 包含：

- success / partial_success / dry_run
- source_document
- mode
- old_scale / new_scale
- geometry_factor / annotation_factor
- base_point
- requested / processed / skipped / failed
- processed_by_type
- skipped_by_reason
- processed_handles / changed_handles
- issues / warnings
- undo_semantics

GUI 与 MCP 共用 `ScaleConversionService`。

## 8. 不修改全局样式

V1 明确不修改：

- DimStyleTableRecord
- TextStyleTableRecord
- MLeaderStyleTableRecord

Dimension 等使用对象级有效值/override；避免本次选择影响 DWG 其它未选对象。

## 9. 当前静态门禁

- `tools/test_scale_converter_policy.py`：36/36 PASS
- 两模式公式锁定
- Ribbon / YGSCALE / MCP Tool 存在
- GUI/MCP 共用 Service
- 不修改全局样式表
- Annotative / Xref / Underlay / Civil / AEC / Proxy fail-closed
- MCP write 仍走 PID/document binding
- WPF RadioButton 使用匹配的 `ElegantRadioButton` 样式，避免运行时 Style TargetType 冲突

## 10. 仍需 Windows 实机证明

静态环境不能证明以下 AutoCAD Runtime 行为：

- AutoCAD 2024 编译与加载；
- Dimension 各具体派生类的 Transform / override 实际显示；
- Hatch `PatternScale + SetHatchPattern + EvaluateHatch` 的实际表现；
- MLeader / Leader 各样式的实际纸面视觉；
- 带 Attribute 的 BlockReference Transform；
- 一次 Ctrl+Z；
- MCP real 与 GUI 对账。

这些全部进入 `tasks/SCALE_CONVERTER_V1_WINDOWS_SMOKE_20260907.md`，由 Windows 执行器提供原始证据后再判 GO。
