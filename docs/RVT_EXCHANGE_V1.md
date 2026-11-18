# YangCAD RVT Exchange v1

状态：**CONTRACT / V1 STATIC IMPLEMENTATION**

## 1. 目的

`project.revit.prepare` 不是普通“图层规范”工具。

它把当前 DWG 转换为一个面向 Revit 后续重建的中间交换包：

```text
当前 DWG（不修改）
        ↓
Database.Wblock() 内存克隆
        ↓
YangCAD RVT Exchange v1 分类
        ↓
*_RVT.dwg
*_RVT_MAP.json
*_RVT_REPORT.json
```

目标：

- 兼容现有 `Import CAD → Full Explode → 按图层替换` 工作流；
- 为未来 Revit 插件“不炸开、直接重建原生对象”保留机器语义；
- 项目标准由 Revit 侧 Mapping 决定，CAD 预处理器只负责稳定区分与记录。

## 2. Source Safety

- 当前 DWG 只读；
- 不对活动 Database 做写 Transaction；
- Export 通过 `Database.Wblock()` 克隆当前**内存状态**；
- 只修改 detached clone；
- 通过 `SaveAs` 写新 DWG；
- 若同名输出已存在，自动产生 `_1/_2/...`，不覆盖旧输出。

这类 output-copy 工具允许 Service 在 detached/output Database 上创建自己的 Transaction；活动文档仍必须由 Runner 提供只读 `CadExecutionContext`。

## 3. Output Contract

### `*_RVT.dwg`

给当前 Revit 工作流使用：

- Import CAD；
- Full Explode（需要时）；
- 依据交换图层替换 Revit Line Style / Text / Annotation / Dimension / FilledRegion。

### `*_RVT_MAP.json`

机器语义清单：

```json
{
  "schema_version": "yangcad-rvt-exchange/1.0",
  "layers": [],
  "entities": [],
  "warnings": []
}
```

每个 entity 至少包含：

- Handle
- DXF type
- category
- original layer
- exchange layer
- owner（layout/block）
- category-specific data

未来 Revit 自动重建优先读取 MAP，而不是从 explode 后的碎图元反推原语义。

### YangTools 3.1.36 原生重建对接契约（2026-09-01 锁定）

`yangcad-rvt-exchange/1.0` 现作为 **YangCAD → YangTools 3.1.36「CAD转Revit图纸」** 的正式协议。该协议继续 additive 演进，不破坏 2026-08-31 已证明的 DWG/GraphicsStyle 兼容工作流。

MAP 根节点固定/推荐：

```json
{
  "schema_version": "yangcad-rvt-exchange/1.0",
  "units": "mm",
  "origin": [0.0, 0.0, 0.0],
  "entities": [],
  "counts": {},
  "unsupported_count": 0,
  "warnings": []
}
```

- `DxfType` 是 **唯一正式实体类型字段**；新 YangCAD 导出不得用 `type/kind` 替代。YangTools 可保留旧别名读取仅用于兼容历史文件。
- 每个 entity 固定保留 `Handle / DxfType / ExchangeLayer / Traits / Data`；`OriginalLayer / Category / Owner` 继续作为追溯信息。
- `ExchangeLayer` 表达 CAD 预处理后的稳定类型键，例如 `#0.25 Continuous White`、`RVT-T H100 romans.shx`；最终 Revit 类型由 YangTools 映射，不在 CAD 端硬编码。
- `Traits` 只放样式/显示信息；`Data` 放几何、文字、引线、标注语义。
- `units` 按 drawing INSUNITS 输出 `mm/cm/m/in/ft`；INSUNITS 未指定时当前个人工作流回退 `mm` 并写 warning。
- 原始 V1 `origin=[0,0,0]` 表示保留 WCS。2026-10-21 的兼容扩展允许 `coordinate_origin_mode=local`：对已选单一空间求有效几何范围的最小 XY 点写入 MAP `origin`，**Data 仍存原始 WCS 坐标、不移动 DWG 几何**。YangTools 用 `Data坐标 - origin` 计算局部 Revit 视图坐标。旧文件 origin=0 原样支持。
- `normalize_geometry` 始终只表示几何外观属性的 ByLayer 归一化，**绝不是**坐标平移参数。
- 默认 MAP 仅发布 ModelSpace 对象；显式 `include_paperspace=true` 是兼容旧调用的危险混合模式，MAP 写明 `mixed_spaces`、模型/图纸计数和告警，混合时禁用局部基点平移。新 Revit 工程不能将混合文件视为单一平面图。
- `*_RVT.dwg` 仍是整张源 DWG 的克隆：`include_paperspace=false` **只筛选 MAP 分类对象，不过滤克隆 DWG 里的布局实体**。纯模型空间的 DWG 输出裁剪和纯 PaperSpace 模式须经单独实现和 AutoCAD 实测；不要误报已实现。

YangTools 3.1.36 V1 原生重建范围：

| YangCAD DxfType | YangTools Revit 对象 |
|---|---|
| `TEXT / MTEXT` | `TextNote` |
| `LEADER / MULTILEADER` | `TextNote + Leader` |
| `LINE / LWPOLYLINE / ARC / CIRCLE` | `DetailCurve` |
| `HATCH` | `FilledRegion` |
| `DIMENSION`（V1 线性/对齐） | 专用 `DetailCurve` Reference + 原生 `Dimension` |

`ELLIPSE / SPLINE` 仍可由 YangCAD V1 分类和保留语义，但 YangTools 3.1.36 V1 不承诺原生重建；不得把它们伪装成 LINE。

#### Leader 顶点顺序

新导出明确写：

```text
leader_vertex_order = arrow_to_text
vertices[0]          = 箭头/leader head
vertices[last]       = 文字/landing tail
```

该顺序与 AutoCAD MLeader 的 first vertex=head、last vertex=tail 语义一致。

#### Dimension 线性/对齐硬契约

`definition_points` 对 `RotatedDimension / AlignedDimension` 固定输出且角色名不可改：

```text
DimLinePoint
XLine1Point
XLine2Point
```

YangTools 使用两个 XLine 点生成独立 `YangTools-尺寸界线` DetailCurve Reference，再创建真正 Revit Dimension。其它 Dimension 类型可继续输出诊断语义，但 `revit_rebuild_supported=false`，等待后续版本支持。

#### Hatch 边界硬契约

`Data.boundary_loops` 必须存在。Curve loop 对常用边界固定使用：

```json
{"Type":"LINE","StartPoint":[0,0],"EndPoint":[100,0]}
```

或：

```json
{"Type":"ARC","StartPoint":[...],"EndPoint":[...],"Center":[...],"Radius":...,"StartAngleDeg":...,"EndAngleDeg":...,"Clockwise":false}
```

Polyline hatch 继续保留 `vertices + bulge` 旧语义以兼容已有数据。

#### 类型映射职责

例如：

```text
CAD RVT-T H100 romans.shx     → Revit 2.5mm
CAD #0.25 Continuous White    → Revit #0.25 Continuous
CAD ANSI31                    → Revit 项目已有/复制的 FilledRegionType
```

这些映射只由 YangTools/UI 决定。YangCAD 只负责准确输出 CAD 源标准，不猜 Revit 字高、类型名或项目标准。

固定回归 fixture：

```text
tools/fixtures/RVT_EXCHANGE_V1_YANGTOOLS_FIXTURE.json
```

它覆盖 LINE/LWPOLYLINE/ARC/CIRCLE/TEXT/MTEXT/LEADER/MULTILEADER/HATCH/DIMENSION。后续改 RVT Exchange 时必须保证 fixture 契约和 `test_revit_exchange_v1_policy.py` 同时通过。

### `*_RVT_REPORT.json`

运行报告与异常统计：

- scanned / classified / unsupported
- category counts
- generated layers
- Mixed MText
- Gradient Hatch
- normal blocks scanned
- anonymous blocks skipped
- xref blocks skipped
- unresolved ByBlock
- warnings

## 4. V1 Categories

### 4.1 Geometry

支持：

- Line
- Polyline
- Arc
- Circle
- Ellipse
- Spline

交换层名称保持当前 Revit 线型工作流兼容：

```text
#0.25 Continuous Red
#0.15 DASHED White
#0.35 Continuous Green
```

ACI 基础色：

- 1 = Red
- 2 = Yellow
- 3 = Green
- 4 = Cyan
- 5 = Blue
- 6 = Magenta
- 7 = White

其他 ACI：`ACI<n>`；TrueColor：`RGB-r-g-b`。

几何实体迁移后默认将：

- Color
- Linetype
- LineWeight

归一为 `ByLayer`，使交换层名与实际显示属性一致。

ByBlock 在 block definition 层面无法总是恢复到实例实际显示值；V1 使用来源 layer fallback，并明确写 Warning。

### 4.2 Text

支持：

- DBText
- MText

分类特征：

- text height
- font/typeface
- bold
- italic
- underline
- overline
- strike-through
- superscript
- subscript
- stacked text
- mixed inline formatting
- oblique
- width factor

示例：

```text
RVT-T H2.5 SimSun
RVT-T H2.5 SimSun B-U
RVT-T H3.5 Arial I
RVT-T H2.5 SimSun SUP-MIXED
```

MText 局部格式不会被强行 explode；若有 inline override，MAP 保留 raw text、inline font 列表并标记 `mixed_format`。不同于 base style 的 inline font 会进入 `MIXEDFONT` 分类，避免把混合字体误判为单一字体。

### 4.3 Leader

支持：

- MLeader
- Leader

交换层：

```text
RVT-ML H2.5 SimSun
RVT-L H2.5 SimSun
```

MAP 尽量保存：

- text / raw text
- text height/font/format flags
- MLeader leader line vertices
- text location
- arrow size
- dogleg length
- Leader vertices / arrow / spline

V1 不提前决定 Revit 最终是 TextNote Leader 还是项目自定义注释族。

### 4.4 Dimension

支持所有 `Dimension` 派生类型并按类别分类：

- LINEAR
- ALIGNED
- ANGULAR
- RADIUS
- DIAMETER
- ARC
- ORDINATE
- OTHER runtime type

交换层示例：

```text
RVT-D LINEAR H2.5 DIM-A
RVT-D ALIGNED H2.5 DIM-A
RVT-D ANGULAR H2.5 ANG-DIM
RVT-D RADIUS H2.5 R-DIM
```

MAP 保存：

- dimension kind
- dimension style
- measurement
- DimensionText override
- text height/position/rotation
- Dimscale
- arrow size
- type-specific definition points（通过稳定属性名反射提取）

关键原则：**不在 CAD 侧 explode Dimension**。未来 Revit 插件使用原始语义重建。

### 4.5 Hatch

支持：

- Solid / PreDefined / Custom / UserDefined Hatch
- Gradient（单独标记 Warning）

交换层：

```text
RVT-H SOLID S1 A0 White
RVT-H ANSI31 S1 A45 White
RVT-H AR-CONC S0.5 A0 ACI8
RVT-H GRADIENT S1 A0 White
```

MAP 保存：

- pattern
- pattern type
- scale
- normalized angle (degrees)
- loop count
- boundary loops（polyline vertex/bulge；curve loop 反射提取 Curve2d 可序列化属性）
- gradient flag
- extents

V1 不假设 Gradient 与 Revit FilledRegion 存在一一等价。

## 5. Block / Xref Boundary

### Normal block definitions

V1 **不发布普通 block definition 的子实体**。原因是 `yangcad-rvt-exchange/1.0` 没有定义 BlockReference 的插入点/旋转/缩放/实例变换；若只把 definition-space 子实体写入 MAP，YangTools 会在错误坐标重建。

`scan_blocks=true` 只做诊断统计；布局中的 `INSERT/BlockReference` 进入 `unsupported_items` 并记录 Handle / DxfType / reason。需要块原生重建时必须两端共同升级协议，不能在 V1 静默展开。

### Anonymous/system blocks

跳过并报告，例如 Dimension display anonymous blocks，防止把系统生成 display geometry 当业务对象重新分类。

### Xref / Overlay

不修改 Xref definition，明确报告。

### ByBlock

无法仅从 definition 100% 还原每个 instance 的实际外观。

V1：
- fallback 到来源 layer；
- `unresolved_byblock` 计数；
- 不伪造“已完全解析”。

若未来真实项目证明 ByBlock block instance 很多，再单独开发 `flatten-for-revit` 输出模式，不在 V1 提前实现。

## 6. Revit-side Future Modes

### Mode A — compatibility

```text
*_RVT.dwg
→ Import CAD
→ Full Explode
→ GraphicsStyle/layer mapping
→ 项目标准 Revit 类型
```

### Mode B — semantic rebuild（推荐长期方向）

```text
*_RVT.dwg + *_RVT_MAP.json
→ Revit 插件读取
→ 直接生成 DetailCurve/TextNote/Dimension/FilledRegion/Annotation
→ 删除或卸载 ImportInstance
```

不同项目只更换 Mapping，不重新开发 DWG parser。

## 7. V1 Known Limitations

- Ellipse / Spline：V1 会分层，但 MAP 只保证 extents + type，精确重建数据后续按真实需求补；
- MText inline mixed formatting：保留 raw text + flags，不拆成多个 Text entity；
- ByBlock：definition-level 不能完全解析 instance appearance；
- Xref：只报告，不递归改外部 DWG；
- anonymous/system blocks：跳过；
- Civil proxy/custom entities：不强制转换，计入 unsupported/warnings；
- Revit Full Explode 的具体元素数量可能大于 source entity count，REPORT 不把 source count 冒充 Revit explode count。

## 8. Stability Rule

`schema_version = yangcad-rvt-exchange/1.0` 一旦进入 Windows GO 后：

- 不随意改字段语义；
- 新字段优先 additive；
- 破坏性 schema change 必须升级版本；
- Revit YangTools/项目插件可依赖该版本做 Mapping。


## Runtime validation status

状态：**CLOSED / GO / PASS（2026-08-31）**

Revit 2024 实机已证明：
- Import CAD 可读取 RVT Exchange 图层为真实 Imported Objects Subcategory / GraphicsStyle；
- `#...` Geometry layer 可用于 Revit 线型筛选与替换；
- Text Full Explode → TextNote；
- MLeader Full Explode → DetailLine/箭头碎片 + TextNote，不生成原生 Revit Leader；
- Dimension Full Explode → DetailLine + TextNote，不生成原生 Revit Dimension；
- Hatch 的 Solid / ANSI31 实际导入与爆炸行为已记录；
- `_RVT_MAP.json` 保留 Leader / Dimension / Hatch 等后续原生重建语义。

因此 Exchange v1 的长期策略正式确定为：

```text
DWG GraphicsStyle / Layer = 人工兼容与定位通道
RVT_MAP.json              = 原生 Revit 自动重建语义通道
```

不要把 Full Explode 当作保留 CAD 注释语义的机制。


---

## 2026-09-07 实施锁定补充（优先于旧建议）

- schema 继续固定 `yangcad-rvt-exchange/1.0`。
- 新文件唯一正式类型字段为 `DxfType`。
- V1 正式 DxfType 集合：`LINE, LWPOLYLINE, ARC, CIRCLE, TEXT, MTEXT, LEADER, MULTILEADER, HATCH, DIMENSION`。Ellipse/Spline/Block/Table/OLE/Raster/3D 等进入 unsupported，不再用 extents-only 假装可重建。
- `LWPOLYLINE` 已验收合同固定为 `Data.vertices[].point + bulge`；非零 bulge 必须保留，禁止静默拉直。旧正文中“可拆 LINE/ARC”的历史建议不再作为当前实现要求。
- `LEADER/MULTILEADER` 正式输出 `leader_lines[].points`，点序固定 arrow/object → text；YangTools 3.1.36 Reader 仍兼容旧 `vertices`。
- Dimension V1 仅输出可准确表达的 linear/aligned 语义，至少包含 `DimLinePoint/XLine1Point/XLine2Point`；其它 Dimension 进入 unsupported。
- unsupported 除计数外增加 additive `unsupported_items[] = {Handle,DxfType,reason}`，不改变 schema_version。
- `project.revit.prepare` 与 GUI/MCP 共用同一 `RevitExchangeService.Request/Result`。
