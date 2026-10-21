# FEATURE-CAD-REVIT-PREP-001 — Revit 2024 Targeted Retest

状态：**CLOSED / GO / PASS — 2026-08-31**

## Scope

不要重跑完整 2021–2024 矩阵。
不要重跑 AutoCAD P0 sample / MAP / REPORT。
不要重新开发功能。

只验证最终清洁源码 + Revit 工作流。

## 1. Source / build

使用本包。

必须：
- Outer SHA match
- internal SHA match
- AutoCAD 2024 clean build = 0 error
- 11 static/policy gates可不重跑；如源码未变只记录既有 PASS

确认生产源码中不存在测试辅助命令：
- `YANGCAD_RVT_CREATE_SAMPLE`
- `YANGCAD_RVT_PREPARE_PREVIEW`
- `YANGCAD_RVT_PREPARE_EXPORT`

## 2. Reuse proven output

可以直接使用上轮已经生成并审计通过的受控：
`Controlled_P0_Sample_RVT.dwg`

如果该文件已不存在，只需用 GUI 正常执行一次 `project.revit.prepare` 重新生成；不要添加测试命令。

## 3. Revit 2024 — mandatory

在新的 Revit 2024 测试项目：

1. `Insert → Import CAD`
2. 选择 `_RVT.dwg`
3. 不要 Link
4. 记录导入后的 CAD ImportInstance
5. 检查导入对象 subcategory / GraphicsStyle

必须实际记录至少这些名称是否可见：

- `#0.25 Continuous Red`
- 一个 `RVT-T ...`
- 一个 `RVT-D ...`
- 一个 `RVT-ML ...`
- 一个 `RVT-H ...`

不要从预期推测，记录 Revit UI/API 实际返回的名称。

## 4. Controlled Full Explode

只对受控小样执行 Full Explode。

必须记录：

### Geometry
- `#0.25 Continuous Red` 等来源分类是否仍可用于识别/替换 Revit 线型

### Text
- 爆炸后的文字是否可追溯到 `RVT-T` 分类，或记录 Revit 实际丢失/改名行为

### Leader
- `RVT-ML` 在 Revit 爆炸后实际变成什么对象/名称
- 如果 Revit 本身不能保留 leader 语义，要明确记录，不是假 PASS

### Dimension
- `RVT-D` 在 Full Explode 后实际结果是什么
- 记录是否变成线+文字等；不要声称仍是 Revit Dimension，除非真实如此

### Hatch
- `RVT-H` 是否生成可识别 FilledRegion/相关图元
- 记录实际 Revit 名称，即使乱码也要记录

## 5. PASS rule

允许出现“Revit Full Explode 本身丢失部分 CAD 语义”。

本任务 PASS 的核心不是要求 Revit 自动保留 Dimension/MLeader 原生语义，而是证明：

1. Import 后交换图层/GraphicsStyle 可读取；
2. 当前人工 layer replacement 工作流仍可使用；
3. MAP 保留后续自动重建所需的原 CAD 语义；
4. Revit 的实际 explode 限制被如实记录。

只有上述有真实记录才能：

> FEATURE-CAD-REVIT-PREP-001 = CLOSED / GO / PASS

## 6. Return evidence

回传简短报告即可，至少包含：

- final source SHA
- AutoCAD 2024 clean build
- Revit 2024 import PASS/FAIL
- 实际 GraphicsStyle/Subcategory 名称样本
- Full Explode 后 Geometry/Text/Leader/Dimension/Hatch 的实际结果
- 0 unhandled exception

若有截图可附，但文字/API 实际结果记录即可。


## Final result

- clean source SHA: `A11EE60367A4FA68749F8CCD25CADC1559942807DC372ADC22BC5A90AA41EF77`
- AutoCAD 2024 clean build: PASS
- Revit 2024 Import CAD: PASS
- actual GraphicsStyle/Subcategory names: PASS
- controlled Full Explode: PASS
- Geometry/Text/Leader/Dimension/Hatch actual behavior recorded
- 0 unhandled exception

Verdict:
**FEATURE-CAD-REVIT-PREP-001 = CLOSED / GO / PASS**
