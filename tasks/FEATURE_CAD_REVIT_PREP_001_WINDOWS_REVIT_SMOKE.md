# FEATURE-CAD-REVIT-PREP-001 — Windows + Revit 2024 Smoke

状态：**READY AFTER STATIC CANDIDATE**

## 0. 输入

使用 ChatGPT 返回的最新 `RVT_EXCHANGE_V1_STATIC_CANDIDATE` 完整仓库。
先核 Outer SHA + internal SHA256SUMS。

## 1. Static / build

运行全部现有 policy +：

`python tools/test_revit_exchange_v1_policy.py`

Build：
- AutoCAD 2021 / 2022 / 2023 / 2024 clean
- Civil 2022 / 2023 / 2024 clean
- BatchWorker 2021–2024 clean
- external System.Text.Json.dll = 0

若编译失败，只修真实 API/namespace blocker，不重构设计。
源码一旦改，必须返回完整 FINAL_SOURCE + Outer SHA。

## 2. AutoCAD/Civil 3D 2024 P0 sample

准备一张受控测试 DWG，至少包含：

### Geometry
- Line/Polyline/Arc/Circle
- 至少 2 个 lineweight
- Continuous + dashed linetype
- ACI 1 Red + ACI 7 White

### Text
- DBText 两种字高/字体
- MText
- Bold / Italic / Underline 至少各一个
- 至少一个 stacked/superscript/subscript 或 mixed-format MText（若测试环境方便）

### Leader
- MLeader with MText
- Leader（若项目样本有）

### Dimension
- Rotated/Aligned 至少一种
- Angular / Radial / Diametric 至少一种非线性类型

### Hatch
- Solid
- ANSI31 或其他 pattern
- 不同比例/角度

### Blocks
- 一个普通 block definition
- 如能方便制造 ByBlock，则加入并确认 Warning，而不是要求完全解析

## 3. Preview

打开 Toolbox → Revit 图纸预处理 → 预览。

必须：
- 当前 DWG entity/layer count 不变
- 没有新文件
- 返回 scanned/classified/generated layers/category counts
- warnings 对 skipped/unresolved 情况可读

## 4. Execute

执行。

必须生成：
- `<name>_RVT.dwg`
- `<name>_RVT_MAP.json`
- `<name>_RVT_REPORT.json`

同名已存在时必须自动生成新名字，不覆盖旧文件。
当前活动 DWG 不改、不改名。

## 5. DWG output verification

打开 `_RVT.dwg` 检查：

### Geometry
实际出现类似：
- `#0.25 Continuous Red`
- `#0.25 Continuous White`
- 其他实际 lineweight/linetype/color 组合

几何实体：
- Layer 正确
- Color = ByLayer
- Linetype = ByLayer
- LineWeight = ByLayer

### Text
出现 `RVT-T ...` 层；至少能区分测试样本中的字高/字体/格式 flags。
文字内容/位置仍正确，MText 未由 YangCAD explode。

### Leader
出现 `RVT-ML ...` / `RVT-L ...`；MLeader/Leader 仍保持原对象类型。

### Dimension
出现 `RVT-D LINEAR/ALIGNED/...`；Dimension 仍是 Dimension，不是提前炸开的 line/text。

### Hatch
出现 `RVT-H ...`；Hatch 仍是 Hatch。

## 6. MAP verification

抽查对应 Handle/对象：
- original_layer
- exchange_layer
- category
- text flags
- leader vertices/text location
- dimension measurement/style/definition_points
- hatch pattern/scale/angle

MAP `schema_version = yangcad-rvt-exchange/1.0`。

## 7. REPORT verification

检查：
- scanned_entities
- classified_entities
- category_counts
- generated_layers
- mixed_mtext
- gradient_hatches
- anonymous_blocks_skipped
- xref_blocks_skipped
- unresolved_byblock
- warnings

不允许把 unsupported/skipped 写成 PASS/0 假象。

## 8. Revit 2024 workflow proof — mandatory

在 Revit 2024 新测试项目：

1. Import CAD：使用 `_RVT.dwg`，不要 Link。
2. 先确认导入对象 Subcategory/GraphicsStyle 中能看到生成交换层。
3. 在受控小样上 Full Explode。
4. 确认：
   - 几何线可以通过 `#0.25 Continuous Red` 等来源层/样式识别；
   - Text/Leader/Dimension/Hatch 对应的 `RVT-*` 分类信息在 Revit 侧仍可用于后续映射；
   - 记录 Revit 爆炸后实际出现的名称/类型，不从预期推测。

本轮**不开发 Revit 重建插件**；只证明 CAD exchange 输出对现有工作流有用。

## 9. Compatibility minimum

2021/2022/2023：
- exact-year clean build 必须
- 2021 AutoCAD minimum Preview + Execute 一次（可用简化样本）
- 2022/2023 minimum Preview 或 Execute smoke，若 exact-year build 已充分且 runtime 成本高，可至少各打开 Toolbox + Preview

## 10. Regression

- stdio 65
- AutoCAD 28
- Civil 58
- 0 unhandled exception
- 0 token leak

另外如果 `FEATURE-INFRA-MCP-CONFIG-001` 仍未单独 Windows PASS，可在同一轮顺手完成它的 targeted smoke；不要重跑历史全矩阵。

## PASS

只有 AutoCAD output + MAP/REPORT + Revit 2024 controlled import/explode 均有真实证据，才能写：

> FEATURE-CAD-REVIT-PREP-001 = CLOSED / GO / PASS
