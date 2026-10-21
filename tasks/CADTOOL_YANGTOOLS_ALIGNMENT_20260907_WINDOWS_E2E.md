# CADTool / YangCAD ↔ YangTools 业务对齐 Windows E2E

日期：2026-09-07  
执行器：Gemini（仅执行，不作 FINAL GO）  
最终验收：ChatGPT

## 0. 输入与禁止项

- 只使用本候选源码 ZIP；先核对用户/ChatGPT 提供的外层 SHA-256。
- AutoCAD / Civil 3D 主测 2024；YangTools Reader 使用正式稳定基线 `YangTools_Project_3.1.36_Source_CAD_TO_REVIT_FINAL_E2E_20260901.zip`，不得换成 3.1.38。
- Exchange schema 必须仍为 `yangcad-rvt-exchange/1.0`。
- 不允许为了让测试通过而改验收脚本、改计数、硬编码 PASS、伪造 JSON/截图。
- Build PASS 不能写 Runtime PASS；直接 AutoCAD/Revit API primitive 不能冒充产品链。
- 如果必须修改源码，停止当前证据链，返回 changed-files + 新完整源码 ZIP + 新 SHA；旧 DLL/证据不得移植到新 SHA。
- 不做 Git push / PR。

## 1. Build / Deploy / Loaded

必须记录：

- Windows 版本；AutoCAD/Civil 3D 2024 版本；
- build command；
- `YangCAD.AutoCAD.dll` / Civil DLL 路径、bytes、SHA-256；
- deploy path 与部署后 SHA；
- acad.exe / Civil process PID、start time；
- 实际 loaded assembly path + SHA。

硬关系：

`Build SHA == Deploy SHA == Loaded SHA`

否则 FAIL。

## 2. MCP 合同

实机调用/记录原始返回：

- MCP stdio tools/list = **67**；
- AutoCAD live Gateway tools = **30**；
- Civil 3D live Gateway tools = **60**；
- `project.revit.prepare` 存在；
- `project.image.embed` 存在；
- 两个产品 Tool 均能 dry_run 返回结构化 Result。

至少保留 initialize / tools-list / tools-call / error 的原始 stdio 记录。不能只截图 UI 数字。

## 3. 创建受控真实 DWG（必须 AutoCAD 原生对象）

在 AutoCAD 2024 创建一个新的、可复现的 `CADTOOL_RVT_E2E_11.dwg`，INSUNITS=Millimeters，ModelSpace 只包含下列 11 个业务对象：

- 3 × LWPOLYLINE
- 1 × LINE
- 1 × ARC
- 1 × CIRCLE
- 1 × TEXT
- 1 × MTEXT
- 1 × MULTILEADER
- 1 × HATCH
- 1 × DIMENSION（Linear/Rotated）

要求：

- 至少一条 LWPOLYLINE 的一个起始 vertex `bulge=0.5`；
- MLeader 文本必须精确为 `MLeader E2E Text`；
- MLeader 至少一条真实 leader line；
- HATCH 使用可稳定重建的普通边界；
- 不加入 Block/Table/OLE/Raster/Xref 等 unsupported 对象；
- 保存 DWG 并记录 bytes / SHA。

必须额外用 AutoCAD API/命令读回 11 个源对象的 Handle、DxfType；对 bulge 和 MLeader 文本/leader 顶点做源端读回，保存为 `01_SOURCE_OBJECTS.json`。禁止手写硬编码源读回结果。

## 4. GUI 产品链：Revit 图纸预处理

从 YangCAD **工具箱 → Revit 图纸预处理** 对受控 DWG执行：

1. dry-run；
2. real export。

必须保留：

- GUI 可见窗口截图；
- 产品运行日志；
- Result 文本/JSON；
- 实际 `_RVT.dwg`；
- 实际 `_RVT_MAP.json`；
- 实际 `_RVT_REPORT.json`；
- 每个输出 bytes + SHA。

硬门禁：

- `schema_version = yangcad-rvt-exchange/1.0`
- `units = mm`
- `source_insunits = Millimeters`
- `scanned_entities = 11`
- `classified_entities = 11`
- `MAP entities = 11`
- `unsupported_count = 0`
- `warnings = []`

MAP 类型计数：

- LWPOLYLINE=3
- LINE=1
- ARC=1
- CIRCLE=1
- TEXT=1
- MTEXT=1
- MULTILEADER=1
- HATCH=1
- DIMENSION=1

专项：

- 源 bulge=0.5 → MAP `vertices[].point + bulge=0.5`；
- MLeader `Data.text == "MLeader E2E Text"`；
- `leader_lines[0].points` 至少 2 点，方向为 arrow→text；
- HATCH `boundary_loops` 存在并含 outer/hole 标记；
- DIMENSION definition_points 包含 `DimLinePoint / XLine1Point / XLine2Point`。

## 5. MCP 产品链：同一 DWG

保持同一活动受控 DWG，先调用读操作建立 PID/document binding，再调用：

- `project.revit.prepare(dry_run=true)`
- `project.revit.prepare(dry_run=false)`

保存完整 MCP request/response 和另一个实际 MAP/REPORT/DWG triplet。

不得调用 generic `cad.command.run` / Python / 自写 API 来代替 `project.revit.prepare`。

## 6. GUI ↔ MCP 对账

对 GUI MAP 与 MCP MAP 做机器可复现比较，忽略：

- 输出路径；
- generated timestamp。

必须相等：

- schema_version
- units
- origin
- source_insunits
- entities count
- per-DxfType counts
- Handles 集合
- 每个 Handle 的 DxfType / ExchangeLayer / Category / Traits / Data
- unsupported_count
- warnings

生成 `02_GUI_MCP_COMPARE.json`，至少包含 `equal=true/false` 和每个字段的 diff。不能只写文字“相同”。

## 7. YangTools 3.1.36 Reader 真实对账

使用正式 `YangTools 3.1.36 FINAL_E2E`，让**产品 Reader**读取本轮真实 YangCAD MAP，不准脚本伪造 Reader 结果。

硬门禁：

- Curves = **6**
- Texts = **3**
- Fills = **1**
- Dimensions = **1**
- TotalCount = **11**
- Warnings = **0**

专项必须证明：

- CAD bulge=0.5 → MAP bulge=0.5 → YangTools Reader → Revit Arc；
- CAD MLeader → MAP text+leader_lines → YangTools → TextNote + Leader。

如果还执行原生 Rebuild，必须记录实际 Revit ElementId / Category / Class；但本任务最低门禁是 Reader 11/11 + 上述两项产品链专项。

## 8. Unsupported fail-closed 附加样本

另建一个小 DWG，至少包含：

- 1 × BlockReference；
- 1 × 非 V1 对象（例如 Ellipse 或 Spline）。

执行 `project.revit.prepare`，确认：

- 不把 block definition 子实体当作正常 MAP entity 发布；
- unsupported_count 正确增加；
- `unsupported_items[]` 有真实 Handle / DxfType / reason；
- 不把 Ellipse/Spline 伪装成可重建 Curve；
- 源 DWG SHA before/after 不变。

## 9. `project.image.embed` MCP 对齐 Smoke

至少验证：

- GUI dry_run 与 MCP dry_run 对同一输入范围返回相同 file_count / output plan；
- MCP real 走 `project.image.embed` 产品 Tool，而不是 command fallback；
- 复用既有图片内嵌安全门禁：输入只读、no-overwrite、status.json、真实 OLE、移走源图后重开仍可见。

如果本轮没有可用受控图片 DWG，写 `NOT_TESTED`，不能写 PASS。

## 10. 证据目录

建议：

```text
CADTOOL_YANGTOOLS_ALIGNMENT_E2E_20260907/
  00_ENV/
  01_BUILD_DEPLOY_LOADED/
  02_MCP_RAW/
  03_INPUT/
  04_GUI_RVT_PREPARE/
  05_MCP_RVT_PREPARE/
  06_COMPARE/
  07_YANGTOOLS_READER/
  08_UNSUPPORTED/
  09_IMAGE_EMBED/
  10_SCREENSHOTS/
  11_LOGS/
  FINAL_EVIDENCE_REPORT.md
```

所有真实文件都计算 SHA-256。

## 11. 允许的最终执行器结论

Gemini 只能返回其一：

- `EVIDENCE COMPLETE — SUBMIT TO CHATGPT FOR FINAL REVIEW`
- `NO-GO — FAILURE EVIDENCE ATTACHED`
- `INCOMPLETE — UNPROVEN ITEMS REMAIN`

**禁止 Gemini 写 FINAL GO。**
