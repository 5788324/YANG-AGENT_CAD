# YangCAD ↔ YangTools 3.1.36｜CAD→Revit 图纸对接规范 V1

日期：2026-09-01  
> **直接源码对接实施注记（2026-09-01）**：本规范已在 YangCAD 0.9 当前源码中落地。实际导出继续使用 `yangcad-rvt-exchange/1.0`，不升级 schema；`DxfType` 为唯一正式类型字段。`units` 对 mm/cm/m/in/ft 按 CAD `INSUNITS` 原样标识，数值不隐式缩放；`INSUNITS=Undefined` 回退 mm 并 warning；其它显式单位 fail-closed，避免误倍率。当前增量仅 STATIC PASS，仍需 AutoCAD 真导出 + YangTools 3.1.36 Reader E2E 对账。

用途：交给 YangCAD 插件开发 AI / YangTools Revit 端共同执行  
协议版本：`yangcad-rvt-exchange/1.0`

---

## 1. 目标

YangCAD 负责把二维 CAD 图纸预处理并输出结构化 RVT Exchange JSON；YangTools 负责读取 JSON，在当前 Revit 视图中按用户映射重建为可编辑的 Revit 原生图元。

V1 不追求 CAD 与 Revit 制图标准完全一致，目标是：

1. 几何位置、尺寸、方向正确；
2. CAD 对象类型和样式信息完整；
3. Revit 端由用户决定使用哪个 Revit 类型；
4. 转换后得到真正的 Revit 原生对象；
5. 整次转换可一次 Ctrl+Z；
6. 不支持的 CAD 对象明确跳过，不静默伪造。

支持范围：

| CAD | Revit |
|---|---|
| TEXT / MTEXT | TextNote |
| LEADER / MULTILEADER | TextNote + Leader |
| LINE / LWPOLYLINE / ARC / CIRCLE | DetailCurve |
| HATCH | FilledRegion |
| Linear DIMENSION | Dimension + 专用 DetailCurve Reference |

V1 不处理：

- Block → Revit Family
- Table
- OLE
- Raster
- 3D CAD
- 复杂 PAT 的自动完整翻译
- CAD Dimension 关联真实 Revit 墙/梁/板/柱

---

# 2. 两端职责

## YangCAD

YangCAD 只负责：

- 识别 CAD 原生对象；
- 预处理图层、文字、线宽、线型等标准；
- 保留对象语义；
- 输出统一 JSON；
- 保留足够几何数据供 Revit 重建。

YangCAD **不要**决定最终 Revit TextNoteType / LineStyle / FilledRegionType / DimensionType。

例如：

```text
CAD：RVT-T H100 romans.shx
```

YangCAD 只需要把该类型完整输出。

最终：

```text
RVT-T H100 romans.shx → Revit 2.5mm
```

由 YangTools 的映射窗口决定。

## YangTools

YangTools 负责：

- 读取 Exchange；
- 左侧聚合 CAD 类型；
- 右侧选择 Revit 类型；
- 使用现有 / 复制现有 / 自动新建 / CAD炸开参照 / 跳过；
- 创建原生 Revit 对象；
- 保存映射；
- 事务、失败隔离和 Ctrl+Z。

---

# 3. 根 JSON 结构

推荐且锁定为：

```json
{
  "schema_version": "yangcad-rvt-exchange/1.0",
  "units": "mm",
  "origin": [0.0, 0.0, 0.0],
  "entities": []
}
```

## 必填

### `schema_version`

固定：

```text
yangcad-rvt-exchange/1.0
```

### `units`

推荐固定：

```text
mm
```

YangTools 兼容：

```text
mm
cm
m
in
ft
```

但 YangCAD 当前导出建议统一使用 `mm`，不要不同文件随机切换。

### `origin`

格式：

```json
[0.0, 0.0, 0.0]
```

所有实体坐标与该 Origin 使用相同 CAD 坐标系和单位。

Revit 端计算：

```text
Relative = EntityCoordinate - ExchangeOrigin
```

然后：

```text
CAD X → Revit View.RightDirection
CAD Y → Revit View.UpDirection
```

### `entities`

所有 V1 图元统一放在一个数组里。

禁止再次分裂成多个互不兼容的 `texts / curves / hatches / dimensions` schema。

---

# 4. 每个实体的通用字段

每个对象建议统一：

```json
{
  "Handle": "171",
  "DxfType": "LINE",
  "ExchangeLayer": "#0.25 Continuous White",
  "OriginalLayer": "A-WALL",
  "Category": "Curve",
  "Traits": {},
  "Data": {}
}
```

## `Handle` — 必填

CAD 对象唯一 Handle。

要求：

- 一个 Exchange 文件内唯一；
- 原样字符串保存；
- Revit 日志/失败报告用它追踪来源。

## `DxfType` — 必填、正式协议字段

**这是 YangCAD ↔ YangTools 的正式对象类型字段。**

必须使用：

```text
DxfType
```

允许值 V1：

```text
LINE
LWPOLYLINE
ARC
CIRCLE
TEXT
MTEXT
LEADER
MULTILEADER
HATCH
DIMENSION
```

YangTools 为旧文件兼容仍可读取：

```text
kind
type
entity_type
entityType
object_type
objectType
```

但 YangCAD 新导出 **禁止继续使用这些字段替代 `DxfType`**。

之前出现“扫描 0 个对象”的原因就是 YangCAD 输出 `DxfType`，旧 Reader 只寻找 `type/kind`。此问题已经修复，因此本协议正式锁定 `DxfType`。

## `ExchangeLayer` — 强烈要求

这是 **Revit 左侧映射表的主要 CAD 类型键**。

它不是必须等于 CAD 原始 Layer Name，而应优先使用 YangCAD 预处理后的规范名称。

例如：

```text
#0.25 Continuous White
RVT-T H100 romans.shx
RVT-H SOLID S1 A0 White
RVT-D LINEAR H0.18 Standard
```

这样 Revit 端可以：

```text
#0.25 Continuous White → #0.25 Continuous
RVT-T H100 romans.shx → 2.5mm
```

## `OriginalLayer` — 可选

保存预处理前原始图层名，仅用于诊断/追溯。

不应作为 Revit 默认映射主键。

## `Category` — 可选

推荐：

```text
Curve
Text
Fill
Dimension
```

只是辅助信息。

真正的对象分发以 `DxfType` 为准。

## `Traits` — 推荐

放“样式/显示属性”。

例如：

```json
{
  "Color": "White",
  "LineType": "Continuous",
  "LineWeightMm": 0.25
}
```

## `Data` — 必填

放“几何/文字/标注语义数据”。

**几何字段不要有时放根节点、有时放 Data。**

YangTools Reader 可以兼容旧格式，但 YangCAD 新版本统一输出 `Data`。

---

# 5. LINE

```json
{
  "Handle": "171",
  "DxfType": "LINE",
  "ExchangeLayer": "#0.25 Continuous White",
  "Traits": {
    "LineType": "Continuous",
    "LineWeightMm": 0.25,
    "Color": "White"
  },
  "Data": {
    "start": [0.0, 0.0, 0.0],
    "end": [100.0, 0.0, 0.0]
  }
}
```

必须：

```text
start
end
```

每点：

```text
[x, y, z]
```

V1 Revit 主要使用 XY，Z 保留用于诊断/未来扩展。

---

# 6. LWPOLYLINE

```json
{
  "Handle": "1A8",
  "DxfType": "LWPOLYLINE",
  "ExchangeLayer": "#0.25 Continuous White",
  "Traits": {
    "LineType": "Continuous",
    "LineWeightMm": 0.25
  },
  "Data": {
    "closed": true,
    "vertices": [
      {"point": [200.0, 0.0, 0.0]},
      {"point": [250.0, 0.0, 0.0]},
      {"point": [250.0, 50.0, 0.0]},
      {"point": [200.0, 50.0, 0.0]}
    ]
  }
}
```

V1 要求：

- 至少 2 点；
- `closed` 明确输出；
- 不依赖“首尾点相同”猜闭合状态。

如果 CAD Polyline 含 Bulge：

V1 可以：

1. YangCAD 在导出时拆成 LINE / ARC；
2. 或后续 schema 1.1 再增加 bulge。

**当前推荐方案：导出阶段拆成 LINE/ARC 语义段。**

---

# 7. ARC

```json
{
  "Handle": "173",
  "DxfType": "ARC",
  "ExchangeLayer": "#0.25 DASHED Red",
  "Traits": {
    "LineType": "DASHED",
    "LineWeightMm": 0.25,
    "Color": "Red"
  },
  "Data": {
    "center": [100.0, 50.0, 0.0],
    "radius": 30.0,
    "start_angle_deg": 0.0,
    "end_angle_deg": 180.0
  }
}
```

角度统一：

```text
degree
```

字段明确叫：

```text
start_angle_deg
end_angle_deg
```

不要有的文件传 degree、有的传 radian。

---

# 8. CIRCLE

```json
{
  "Handle": "175",
  "DxfType": "CIRCLE",
  "ExchangeLayer": "#0.50 DASHED White",
  "Traits": {
    "LineType": "DASHED",
    "LineWeightMm": 0.50
  },
  "Data": {
    "center": [150.0, 50.0, 0.0],
    "radius": 20.0
  }
}
```

必须：

```text
center
radius
```

---

# 9. TEXT / MTEXT

推荐：

```json
{
  "Handle": "176",
  "DxfType": "TEXT",
  "ExchangeLayer": "RVT-T H100 romans.shx",
  "Traits": {
    "TextStyle": "RVT-T",
    "Font": "romans.shx",
    "Height": 100.0,
    "HorizontalAlignment": "Left"
  },
  "Data": {
    "text": "测试文字",
    "position": [0.0, 70.0, 0.0],
    "rotation_deg": 0.0,
    "width": 0.0
  }
}
```

必须：

```text
text
position
```

推荐：

```text
rotation_deg
width
```

### 注意

`Height` / `Font` 只是 CAD 源信息。

YangTools **不得**自动：

```text
H100 → Revit 100mm
```

实际 Revit 类型由用户映射。

例如：

```text
RVT-T H100 romans.shx → Revit "2.5mm"
```

因此 YangCAD 必须把源标准输出完整，但不要尝试猜 Revit 类型。

### MTEXT 换行

JSON 中使用标准：

```text
\n
```

不要输出 AutoCAD 内部转义后无法还原的乱码。

---

# 10. LEADER / MULTILEADER

引线与文字属于一组，在 Revit 中重建为：

```text
TextNote + Leader
```

推荐 MULTILEADER：

```json
{
  "Handle": "1A5",
  "DxfType": "MULTILEADER",
  "ExchangeLayer": "RVT-ML H3.5 UNKNOWN",
  "Traits": {
    "TextStyle": "RVT-ML",
    "Font": "UNKNOWN",
    "Height": 3.5
  },
  "Data": {
    "text": "MLeader Note",
    "position": [120.0, 120.0, 0.0],
    "rotation_deg": 0.0,
    "leader_lines": [
      {
        "vertices": [
          [100.0, 100.0, 0.0],
          [110.0, 110.0, 0.0],
          [120.0, 120.0, 0.0]
        ]
      }
    ]
  }
}
```

## 顶点顺序必须锁定

V1 约定：

```text
vertices[0]      = 箭头端 / CAD 被指向对象端
vertices[last]   = 文字端
中间点           = elbow / 折点
```

不要不同对象使用相反顺序。

如果只有两个点：

```text
[箭头端, 文字端]
```

如果没有文字内容：

- 不要把它伪造成普通 Line；
- 可以导出为 `LEADER`，但 YangTools 可选择跳过并 warning。

---

# 11. HATCH

Revit 最终创建：

```text
FilledRegion
```

推荐：

```json
{
  "Handle": "1A9",
  "DxfType": "HATCH",
  "ExchangeLayer": "RVT-H SOLID S1 A0 White",
  "Traits": {
    "Pattern": "SOLID",
    "Scale": 1.0,
    "AngleDeg": 0.0,
    "Color": "White"
  },
  "Data": {
    "pattern": "SOLID",
    "boundary_loops": [
      {
        "curves": [
          {
            "Type": "LINE",
            "StartPoint": [200.0, 0.0, 0.0],
            "EndPoint": [250.0, 0.0, 0.0]
          },
          {
            "Type": "LINE",
            "StartPoint": [250.0, 0.0, 0.0],
            "EndPoint": [250.0, 50.0, 0.0]
          },
          {
            "Type": "LINE",
            "StartPoint": [250.0, 50.0, 0.0],
            "EndPoint": [200.0, 50.0, 0.0]
          },
          {
            "Type": "LINE",
            "StartPoint": [200.0, 50.0, 0.0],
            "EndPoint": [200.0, 0.0, 0.0]
          }
        ]
      }
    ]
  }
}
```

## V1 Hatch 要求

1. 每个 loop 必须闭合；
2. 外圈和洞口分成不同 `boundary_loops`；
3. 不把 Hatch 炸成几百根普通 LINE；
4. SOLID 必须保留 `pattern = SOLID`；
5. Pattern Name / Scale / Angle 保留给 Revit 映射参考；
6. Revit 无对应 FillPattern 时用户可以映射/复制/跳过。

### 边界 Curve

V1 最低要求支持：

```text
LINE
```

推荐 YangCAD 同时输出：

```text
ARC
```

若当前 Revit Reader 对 ARC Hatch 边界尚未完整支持，可在 V1 先把边界弧离散成足够精度的多段线，但必须：

- 保持闭合；
- 不改变填充拓扑；
- 记录 warning/approximation 标志。

---

# 12. DIMENSION

这是最重要的对接项之一。

V1 只做 Linear Dimension。

不尝试寻找真正的 Revit 墙/梁 Reference。

Revit 流程：

```text
CAD Dimension
→ 根据 definition_points 构造两条专用 DetailCurve
→ DetailCurve.GeometryCurve.Reference
→ Revit 原生 Dimension
```

YangCAD 推荐输出：

```json
{
  "Handle": "17C",
  "DxfType": "DIMENSION",
  "ExchangeLayer": "RVT-D LINEAR H0.18 Standard",
  "Traits": {
    "DimensionType": "LINEAR",
    "DimStyle": "Standard",
    "TextHeight": 0.18
  },
  "Data": {
    "measurement": 100.0,
    "display_text": "100",
    "definition_points": [
      {
        "role": "DimLinePoint",
        "point": [100.0, -20.0, 0.0]
      },
      {
        "role": "XLine1Point",
        "point": [0.0, 0.0, 0.0]
      },
      {
        "role": "XLine2Point",
        "point": [100.0, 0.0, 0.0]
      }
    ]
  }
}
```

## `definition_points` 角色锁定

### `XLine1Point`

第一条尺寸界线对应的测量参考点。

### `XLine2Point`

第二条尺寸界线对应的测量参考点。

### `DimLinePoint`

尺寸线经过的位置点。

对线性标注，这 3 个点足够让 Revit 端推导：

- 尺寸方向；
- 尺寸线位置；
- 两条 Reference DetailCurve。

## 可选

```text
measurement
display_text
```

`measurement` 用于：

- 校验 Revit 最终尺寸；
- 诊断比例/方向错误。

Revit 生成后的真实 Dimension Value 应与 CAD measurement 在容差内一致。

## 尺寸参考线

Revit 端统一使用：

```text
YangTools-尺寸界线
```

这是专用 LineStyle：

- 不与结构线混用；
- 不与普通图纸线混用；
- Dimension 失败时参考线一起回滚。

---

# 13. 样式 Traits 规范

建议 YangCAD 对所有对象尽量输出以下源信息。

## Curve

```json
{
  "LineType": "Continuous",
  "LineWeightMm": 0.25,
  "Color": "White"
}
```

## Text

```json
{
  "TextStyle": "RVT-T",
  "Font": "romans.shx",
  "Height": 100.0,
  "HorizontalAlignment": "Left"
}
```

## Hatch

```json
{
  "Pattern": "ANSI31",
  "Scale": 1.0,
  "AngleDeg": 45.0,
  "Color": "White"
}
```

## Dimension

```json
{
  "DimensionType": "LINEAR",
  "DimStyle": "Standard",
  "TextHeight": 0.18
}
```

这些 Traits 不是 Revit 最终类型。

只用于：

- UI 显示；
- 自动推荐；
- 新建/复制类型时参考；
- 日志诊断。

---

# 14. Revit 映射原则

## 文字

```text
RVT-T H100 romans.shx
→
2.5mm
```

用户决定。

禁止按 CAD 字高自动猜 Revit 字高。

## 线

```text
#0.25 Continuous White
→
#0.25 Continuous
```

YangTools 可以去掉：

```text
White
ByLayer
ByBlock
```

等颜色/继承尾缀后推荐 Revit LineStyle。

### 非 Continuous

例如：

```text
#0.25 DASHED White
```

若当前 Revit 没有对应 LinePattern：

- 不允许自动变成 Solid；
- 用户必须选择现有、复制、或跳过。

## 填充

```text
CAD Hatch
→
FilledRegionType
```

复杂 PAT 不强行翻译。

## 标注

```text
CAD DimStyle
→
DimensionType
```

由用户映射。

---

# 15. YangCAD 导出端禁止事项

禁止：

1. 新版本去掉 `DxfType`；
2. 有时使用 `type`、有时使用 `DxfType`；
3. 有时坐标单位 mm、有时实际数值是 drawing unit 却仍写 mm；
4. 同一字段一会根节点、一会 Data；
5. Hatch 只输出 pattern 不输出边界；
6. Dimension 只输出显示文字，不输出 definition_points；
7. MLeader 只输出文字不输出 leader_lines；
8. Polyline 不输出 `closed`；
9. 用 CAD Explode 后的大量 primitive 代替本来能保留语义的 HATCH/DIMENSION/MTEXT；
10. 把 Revit 类型名写死在 CAD 插件里。

---

# 16. unsupported 对象

如果遇到 V1 不支持：

```text
INSERT
TABLE
IMAGE
OLE2FRAME
SPLINE
ELLIPSE
3D*
PROXY*
```

YangCAD 两种方式均可：

### 推荐

不放入 `entities`，在导出报告中统计：

```json
{
  "unsupported": {
    "INSERT": 12,
    "TABLE": 2
  }
}
```

### 或

保留实体并增加：

```json
{
  "SupportedForRevit": false
}
```

但 YangTools V1 不负责转换。

禁止静默把 unsupported 对象错误标成 LINE/TEXT。

---

# 17. 导出摘要

YangCAD 每次 RVT Exchange 导出建议同时生成摘要：

```json
{
  "schema_version": "yangcad-rvt-exchange/1.0",
  "units": "mm",
  "entity_count": 1234,
  "counts": {
    "LINE": 500,
    "LWPOLYLINE": 300,
    "ARC": 20,
    "CIRCLE": 10,
    "TEXT": 200,
    "MTEXT": 100,
    "MULTILEADER": 30,
    "HATCH": 40,
    "DIMENSION": 34
  },
  "unsupported_count": 5,
  "warnings": []
}
```

YangTools 扫描出的总数应能与这个摘要对账。

---

# 18. 最小对接测试 fixture

YangCAD 自动测试至少生成一份固定 fixture，包含：

```text
1 LINE
1 LWPOLYLINE
1 ARC
1 CIRCLE
1 TEXT
1 MTEXT
1 MULTILEADER
1 SOLID HATCH
1 非 SOLID HATCH
1 LINEAR DIMENSION
```

硬门禁：

```text
schema_version == yangcad-rvt-exchange/1.0
units == mm
所有实体都有 Handle
所有实体都有 DxfType
所有实体都有 ExchangeLayer
所有实体都有 Data
```

其中：

```text
LINE：start/end
TEXT：text/position
MLeader：text/position/leader_lines
HATCH：boundary_loops
DIMENSION：definition_points 含
  DimLinePoint
  XLine1Point
  XLine2Point
```

---

# 19. CAD 端测试建议

YangCAD 更新后至少增加以下自动门禁：

## Schema

- `DxfType` 必存在；
- 禁止仅有 `type/kind`；
- schema 固定 1.0；
- units 固定 mm；
- origin 可解析；
- Handle 唯一。

## Geometry

- LINE 长度；
- Polyline 点序和 closed；
- Arc radius/angle；
- Circle radius；
- Text position/rotation；
- Hatch loop 闭合；
- Dimension 3 个 definition point role。

## Round-trip 对账

同一固定 DWG：

```text
CAD 源对象统计
→ YangCAD Export JSON 统计
→ YangTools Reader 扫描统计
```

三者应该一致，unsupported 单独统计。

---

# 20. 当前已验证的真实样本

当前 YangCAD 真实样本已经出现以下格式：

### LINE

```json
{
  "Handle": "171",
  "DxfType": "LINE",
  "ExchangeLayer": "#0.25 Continuous Red",
  "Data": {
    "start": [0, 0, 0],
    "end": [100, 0, 0]
  }
}
```

### LWPOLYLINE

```json
{
  "Handle": "1A8",
  "DxfType": "LWPOLYLINE",
  "ExchangeLayer": "#0.25 Continuous White",
  "Data": {
    "closed": true,
    "vertices": [
      {"point": [200,0]},
      {"point": [250,0]},
      {"point": [250,50]},
      {"point": [200,50]}
    ]
  }
}
```

### ARC

```json
{
  "Handle": "173",
  "DxfType": "ARC",
  "ExchangeLayer": "#0.25 DASHED Red",
  "Data": {
    "center": [100,50,0],
    "radius": 30.0,
    "start_angle_deg": 0,
    "end_angle_deg": 180
  }
}
```

### CIRCLE

```json
{
  "Handle": "175",
  "DxfType": "CIRCLE",
  "ExchangeLayer": "#0.50 DASHED White",
  "Data": {
    "center": [150,50,0],
    "radius": 20.0
  }
}
```

### TEXT

```json
{
  "Handle": "176",
  "DxfType": "TEXT",
  "ExchangeLayer": "RVT-T H2.5 simplex.shx",
  "Data": {
    "text": "Standard Text Height 2.5",
    "position": [0,70,0],
    "rotation_deg": 0
  }
}
```

### MULTILEADER

```json
{
  "Handle": "1A5",
  "DxfType": "MULTILEADER",
  "ExchangeLayer": "RVT-ML H3.5 UNKNOWN",
  "Data": {
    "text": "MLeader Note",
    "leader_lines": [
      {
        "vertices": [
          [100,100,0],
          [120,120,0]
        ]
      }
    ]
  }
}
```

### HATCH

```json
{
  "Handle": "1A9",
  "DxfType": "HATCH",
  "ExchangeLayer": "RVT-H SOLID S1 A0 White",
  "Data": {
    "pattern": "SOLID",
    "boundary_loops": [
      {
        "curves": [
          {"StartPoint":[200,0],"EndPoint":[250,0]},
          {"StartPoint":[250,0],"EndPoint":[250,50]},
          {"StartPoint":[250,50],"EndPoint":[200,50]},
          {"StartPoint":[200,50],"EndPoint":[200,0]}
        ]
      }
    ]
  }
}
```

### DIMENSION

```json
{
  "Handle": "17C",
  "DxfType": "DIMENSION",
  "ExchangeLayer": "RVT-D LINEAR H0.18 Standard",
  "Data": {
    "measurement": 100,
    "definition_points": [
      {"role":"DimLinePoint","point":[100,-20,0]},
      {"role":"XLine1Point","point":[0,0,0]},
      {"role":"XLine2Point","point":[100,0,0]}
    ]
  }
}
```

---

# 21. YangCAD 开发 AI 本轮任务

只更新 YangCAD RVT Exchange，不改其它 CAD 功能。

## 必须完成

1. 以 `DxfType` 为唯一正式对象类型字段；
2. 根结构固定：
   - schema_version
   - units
   - origin
   - entities
3. 统一所有几何/文本业务数据进入 `Data`；
4. 样式进入 `Traits`；
5. 预处理后的类型/图层统一放 `ExchangeLayer`；
6. 完整支持：
   - LINE
   - LWPOLYLINE
   - ARC
   - CIRCLE
   - TEXT
   - MTEXT
   - LEADER
   - MULTILEADER
   - HATCH
   - DIMENSION
7. Hatch 输出封闭 boundary_loops；
8. Dimension 输出标准 definition_points；
9. MLeader 输出 leader_lines；
10. 输出固定真实 fixture；
11. 更新 RVT Exchange 自动测试；
12. 保证旧 MCP/其它 YangCAD 功能不回退。

## 禁止

- 不修改 YangTools Revit 端协议；
- 不重新设计 schema；
- 不把 Revit 类型硬编码进 YangCAD；
- 不用 explode primitive 替代可保留语义的对象；
- 不推 Git，除非用户明确要求；
- 不自行宣布 Revit PASS。

---

# 22. 验收门禁

YangCAD 更新完成后返回：

1. 修改后的完整源码 ZIP；
2. SHA-256；
3. 修改文件清单；
4. RVT Exchange 测试日志；
5. 固定 fixture JSON；
6. 一个真实 DWG 导出的 RVT_MAP JSON；
7. 导出对象统计；
8. unsupported 对象统计。

随后用 YangTools 3.1.36 读取：

```text
TotalCount > 0
```

并核对：

```text
YangCAD 导出统计 == YangTools 扫描统计
```

支持对象数量不一致即 FAIL。

---

## 最终原则

**YangCAD 负责“准确表达 CAD 是什么”；YangTools 负责“把它映射成 Revit 的什么”。**

两个插件只通过 `yangcad-rvt-exchange/1.0` 对接，不互相硬编码对方项目内部类型。
