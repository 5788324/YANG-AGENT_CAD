> 2026-08-31 状态更新：用户补充真实 CAD→Revit 工作流后，原“图层分离”已升级为 `project.revit.prepare / YangCAD RVT Exchange v1` 并进入开发，权威规范改见 `docs/RVT_EXCHANGE_V1.md`。本文件保留为历史需求/旧源码评审。图片内嵌已于 2026-08-31 进入 `FEATURE-PROJECT-IMAGE-EMBED-001` 静态开发；权威规范改见 `docs/IMAGE_EMBED_V1.md`。

# Feature Review — 图层分离 / 图片转 OLE

日期：2026-08-28  
来源：用户提供的旧插件功能说明与完整源码

## 总结

| 功能 | 建议 | 定位 | 是否直接移植旧代码 |
|---|---|---|---|
| 图层分离 | **GO / 值得做** | CAD 高破坏型按需 Tool | **否，按 YangCAD 架构重写** |
| 图片转 OLE | **CONDITIONAL GO / Experimental** | Project/Batch 特殊工具 | **否，必须重写并做实机专项** |

---

# 1. 图层分离

## 原功能意图

旧功能按图元实际显示特性把 Line / Polyline / Arc / Circle 分到：

`#{线宽} {线型} {颜色}`

白色/ByLayer 简化为：

`#{线宽} {线型}`

尺寸标注归：

`#0.10 Continuous`

同时在前后删除空图层。

这个需求本身有明确生产价值，适合进入 YangCAD 按需功能池。

## 旧实现存在的问题

### P0/P1-1 空图层删除方式不合适

旧代码对每个 Layer 调 `IsLayerUsed()`，而 `IsLayerUsed()` 又扫描全部 BlockTableRecord / Entity。

复杂度接近：

`图层数 × 全图实体数`

大图容易非常慢。

更重要的是，直接 `LayerTableRecord.Erase()` 需要自己处理当前层、xref、依赖关系等边界。

### 推荐

不要先删层。

执行顺序改成：

1. 单次扫描建立映射；
2. Preview；
3. 实体迁移；
4. 最后只把候选旧层交给 `Database.Purge`；
5. 仅 Erase AutoCAD 判定真的可 purge 的层。

---

### P0/P1-2 “实际显示特性”解析不完整

旧实现主要处理：
- ByLayer lineweight
- ByLayer linetype
- ByLayer color

但还没有完整定义：
- ByBlock
- TrueColor
- ColorBook
- Default lineweight
- block reference context

因此某些图元可能被命名到错误图层。

### 推荐 v1 边界

只处理顶层 ModelSpace 实体：

- Line
- Polyline
- Arc
- Circle
- Dimension

对无法稳定解析为 ACI/明确线型/明确线宽的实体：

> **skip + warning**

不要猜。

TrueColor 是否“映射最近 ACI”必须由用户以后明确要求，第一版不要自动近似。

---

### P1-3 旧代码没有真正处理块内部图元

旧功能提示“请先炸块”，源码也确实只遍历 ModelSpace 顶层实体。

不应自动把“递归修改 block definition”当成简单增强：

- 修改 block definition 会影响所有引用；
- ByBlock 外观依赖 BlockReference context；
- 动态块 / xref 更复杂。

### 推荐

v1 明确：

> **只处理 ModelSpace 顶层实体，不自动炸块、不修改块定义。**

Preview 统计：
- processed
- skipped_block_references
- unsupported_entities

以后真有需要再做独立“块内规范化”功能。

---

### P1-4 图层名与对象 override 应统一

旧实现只改 `entity.Layer`，但原实体可能仍保留显式：

- Color
- Linetype
- LineWeight

这样虽然视觉暂时相同，但目标图层并没有真正接管属性。

如果业务目标是“规范化图层”，推荐：

1. 先计算 effective appearance；
2. 创建目标层并写入对应 Layer Color/Linetype/LineWeight；
3. 移动实体；
4. 将实体 Color/Linetype/LineWeight 归一成 ByLayer。

这样图层名和图层属性才真正一致。

必须 Preview，因为这是更强的规范化动作。

---

### P1-5 Dimension 范围需要明确

旧源码只 special-case：

- RotatedDimension
- AlignedDimension

如果业务规则是“所有尺寸标注统一 #0.10 Continuous”，实现应针对 `Dimension` 基类，并明确是否包括：
- Angular
- Radial
- Diametric
- Ordinate
- ArcDimension

建议第一版按 `Dimension` 基类统一，但在 Preview 里单列数量。

---

## YangCAD 推荐设计

建议 Tool ID：

`cad.layer.separate-by-appearance`

类型：

> **High-Destructive Write**

GUI 参数建议：

- scope：ModelSpace（v1 固定即可）
- normalize_overrides：true
- dimensions_to_standard_layer：true
- purge_empty_source_layers：true
- default_lineweight_mm：0.25（只用于无法从 DB default 解析时的 fallback；最好尽量不 fallback）
- dry_run / Preview

Preview 输出：

- scanned
- supported
- skipped
- layers_to_create
- entity_moves
- overrides_to_normalize
- candidate_layers_to_purge
- warnings

Real：

- 单一 YangCAD write transaction
- create layers
- migrate/normalize
- `Database.Purge`
- erase only purgable
- Native Undo

### 结论

**值得做，而且比较适合 YangCAD。**

但不要复制旧源码；建议作为下一次真实需要出现时的单独 1-Tool 任务。

---

# 2. 图片转 OLE

## 原功能意图

把 JPG/PNG `RasterImage` 外部引用转换成嵌入 DWG 的 `AcDbOle2Frame`，避免外发 DWG 后图片路径失效。

需求真实存在，但实现风险明显高于普通 CAD Tool。

---

## 旧实现中的高风险问题

### P0-1 会删除所有 RasterImage

旧代码最后：

- 遍历目标 Layout
- 所有 `AcDbRasterImage` 都 `Delete()`

但前面只收集 `.jpg/.png`。

结果：

- 不支持的图片类型也可能被删；
- 某张转换失败时原图仍可能被删。

### 正确原则

> **每张原 RasterImage 只有在对应新 OLE 已确认创建成功并校准后，才删除这一张原图。**

禁止 blanket delete。

---

### P0-2 第一张图片可能修改已有 OLE

旧代码每次粘贴后遍历 Layout 的所有 `AcDbOle2Frame`：

只要 ObjectID 不在 `processedOleIds` 就修改 Width / InsertionPoint。

第一次循环时，已有 OLE 也不在集合里，因此可能被当成“新 OLE”重定位/改宽。

### 正确原则

粘贴前记录：
- 当前 OLE ObjectId/Handle 集合

粘贴后：
- 等待命令完成
- 只接受 before/after delta 中新增的那一个 OLE

如果新增数量不是 1：
- 不删除原 RasterImage
- report failure

---

### P0-3 Clipboard.Clear 时序有竞态

旧实现：

1. `Clipboard.SetImage`
2. `SendCommand("_pasteclip ...")`
3. 马上 `Clipboard.Clear`

`SendCommand`/AutoCAD command completion 不应假设这一刻已经完成读取剪贴板。

可能出现：
- PASTECLIP 尚未消费图片
- 剪贴板已清空
- 转换随机失败

### 正确原则

必须等待 AutoCAD：
- command 完成
- `IsQuiescent`
- 新 Ole2Frame 已出现

之后才清理/恢复 Clipboard。

---

### P0-4 固定 COM CLSID 不适合 2021–2027

旧代码硬编码：

`4AC6DFE1-607B-45B2-B289-D7FBCD44169C`

这天然带版本/安装环境耦合。

### 推荐

不要依赖单一硬编码 CLSID。

如果做 batch：
- 由 YangCAD 根据目标 AutoCAD year 显式选择对应 host；
- isolated process；
- 明确记录 product/version/PID；
- 每次只驱动一个精确 host。

---

### P1-5 文档说“展开块参照”，源码实际上没有

源码只遍历选定 Layout 的直接 entities。

没有递归展开 BlockReference。

所以旧说明里的“块参照展开后的图元”与实际源码不一致。

---

### P1-6 几何恢复不完整

旧代码主要记录：
- `GeometricExtents.MinPoint`
- Width
- Height

这不足以可靠恢复：
- rotation
- image orientation
- clipping
- non-axis-aligned raster
- unusual scale/aspect

第一版必须明确限制。

推荐 v1：

> 仅转换未裁剪、平面、普通旋转/缩放可可靠映射的 JPG/PNG RasterImage。

对 clipped/skewed/unsupported：
- skip
- warning
- 保留原图

---

## 关于“纯 .NET Ole2Frame 创建”的结论

AutoCAD .NET API 有 `Autodesk.AutoCAD.DatabaseServices.Ole2Frame` wrapper，也公开若干属性。

但是公开资料并不支持“直接 new Ole2Frame + 填二进制流即可可靠创建嵌入 OLE”这一结论。

Autodesk Developer Blog 对嵌入图片的说明指出：
- OLE 可以嵌入到 DWG；
- 但 `AcDbOle2Frame` 的创建需要用户交互路径，不能简单通过公开 API programmatically create。

2023 Autodesk .NET Forum 同类问题的回答仍认为没有公开 API 可直接创建嵌入 bitmap OLE。

因此 YangCAD 不应把“纯 .NET 直接写 OLE binary”作为 v1 方案。

---

## YangCAD 推荐实现路线

建议 Tool：

`batch.image.embed-as-ole`

或 GUI 项目工具：

`project.image.embed-as-ole`

建议状态：

> **Experimental**

### 架构

因为这是多 DWG + OLE + Clipboard + UI command：

不要放进普通 live CAD database Tool 模式里硬做。

推荐：

```text
Toolbox GUI
   ↓
OleBatchService / orchestrator
   ↓
独立 exact-year AutoCAD COM process
   ↓
逐 DWG
   ↓
PASTECLIP
   ↓
wait quiescent
   ↓
verify new OLE
   ↓
delete exact original RasterImage
   ↓
SaveAs / output copy
```

### 强制保护

- 默认输出副本，不覆盖输入 DWG
- 输入 SHA before/after 相同
- 每张 image 原子式“转换成功才删原图”
- Clipboard 使用 STA
- Clipboard retry
- 尽量保存并恢复用户原 Clipboard
- 等待 AutoCAD quiescent
- 精确 before/after OLE entity delta
- 每个 DWG 独立结果
- fail one image ≠ delete source
- fail one DWG ≠ silently mark success
- fixed version COM identity 禁止
- token/Gateway 与 batch COM 隔离

### 是否值得做

**有价值，但优先级低于普通生产工具。**

只有你确实经常遇到：
- DWG 外发图片丢失
- 很多图需要批量嵌图

才值得开发。

这类功能建议单独 1-Tool 任务，不跟普通 CAD 工具一起做。

---

# 建议优先级

1. **图层分离：优先级较高，GO**
2. **图片转 OLE：条件 GO / Experimental**

如果下一步要从这两个里选一个实际开发：

> 推荐先做“图层分离”。
