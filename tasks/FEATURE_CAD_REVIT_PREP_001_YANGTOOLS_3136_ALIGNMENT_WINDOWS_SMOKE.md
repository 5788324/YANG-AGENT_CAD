# FEATURE-CAD-REVIT-PREP-001 — YangTools 3.1.36 对接实机验收

日期：2026-09-01  
状态：`STATIC PASS / WINDOWS AUTOCAD + YANGTOOLS E2E RETEST REQUIRED`

## 0. 目的

本轮只验证 YangCAD `project.revit.prepare` 更新后的 `yangcad-rvt-exchange/1.0` 是否与 YangTools 3.1.36「CAD转Revit图纸」真实对齐。

禁止把静态检查、JSON 手工样本或数学换算冒充 AutoCAD/Revit 实机 PASS。

## 1. 输入源码

使用 ChatGPT 交付的：

`YangCAD_0.9_RVT_EXCHANGE_YANGTOOLS_3136_ALIGN_STATIC_20260901.zip`

SHA-256：以 ChatGPT 同轮交付的包外 `YangCAD_0.9_RVT_EXCHANGE_YANGTOOLS_3136_ALIGN_STATIC_20260901_SHA256.txt` 为准。

> 最终 ZIP 哈希不写入 ZIP 内部任务文件，避免自引用导致哈希变化。先核对包外 SHA，不一致停止。

## 2. 本轮允许修改范围

只允许修：

- `src/YangCAD.AutoCAD/Services/Cad/RevitExchangeService.cs`
- `tools/test_revit_exchange_v1_policy.py`
- RVT Exchange fixture / 文档 / 本任务
- 因真实 AutoCAD 编译/API 差异必须做的最小兼容修复

禁止：

- 改 MCP 65/28/58 contract；
- 恢复旧 Toolbox GUI；
- 改 Image Embed；
- 改主题体系；
- 顺带重构项目；
- 推 Git。

## 3. 静态门禁

在全新目录解压：

```text
python tools/test_revit_exchange_v1_policy.py
python tools/verify_source.py
python tools/verify_release_source.py
```

要求：

- RVT Exchange policy = `43/43 PASS` 或修复后更多，但 0 failure；
- source/release verifier = 0 error；
- 其余 `tools/test_*_policy.py` 全部 PASS。

## 4. AutoCAD 2024 真实编译

必须真实编译当前源码，不复用旧 DLL。

记录：

```text
AutoCAD 版本
MSBuild/dotnet 命令
0 error
DLL path
DLL size
DLL SHA-256
```

如果其它年份未安装，可以只做实际可编译年份；禁止把未安装宿主写 Runtime PASS。

## 5. AutoCAD 2024 真加载

关闭旧 AutoCAD 2024 测试进程后加载新 DLL。

记录：

```text
acad.exe PID
启动时间
加载 DLL 完整路径
加载 DLL SHA-256
活动 DWG
```

不得只覆盖磁盘 DLL 后继续使用旧进程内存程序集。

## 6. 真实导出 — P0

使用：

1. 固定受控样本 DWG（必须包含本协议对象）；
2. 至少一个真实项目 DWG。

执行真正的：

`工具箱 → Revit 图纸预处理 → Execute`

必须产生：

```text
*_RVT.dwg
*_RVT_MAP.json
*_RVT_REPORT.json
```

记录三个文件路径与 SHA-256。

## 7. MAP 根协议 — P0

真实 `_RVT_MAP.json` 必须读取并记录：

```text
schema_version = yangcad-rvt-exchange/1.0
units
origin
source_insunits
entities count
counts
unsupported_count
unsupported
warnings
```

硬要求：

- `DxfType` 是正式对象类型字段；
- 每个支持对象都有 `Handle / DxfType / ExchangeLayer / Traits / Data`；
- `Handle` 唯一；
- 不允许只有 `type/kind` 而没有 `DxfType`。

## 8. 单位门禁 — P0

### mm DWG

`INSUNITS=Millimeters`：

```text
units == mm
```

坐标数值保持 CAD WCS 数值，不隐式缩放。

### Undefined DWG

如果测试：

```text
units == mm
warnings 包含 INSUNITS 未指定回退
```

### 显式不支持单位

可用受控副本设置为例如 Miles（不要改原图）。

期望：

```text
导出 FAIL
明确提示先设置 mm/cm/m/in/ft
不得生成被错误标成 mm 的成功 MAP
```

## 9. 图元字段逐项 — P0/P1

固定样本至少含：

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

### LINE

必须：`Data.start / Data.end`

### LWPOLYLINE

必须：`Data.closed / Data.vertices[].point / bulge`

### ARC

必须：`center / radius / start_angle_deg / end_angle_deg`

### CIRCLE

必须：`center / radius`

### TEXT / MTEXT

必须：

```text
text
position
rotation_deg
Traits.text_height/text_style/font
```

### LEADER / MULTILEADER

必须：

```text
position（有文字附件时）
leader_lines
leader_vertex_order = arrow_to_text
```

并抽查真实首点是 arrow/head，末点是 text/landing tail。

### HATCH

必须：

```text
pattern
boundary_loops
```

Curve 边界中普通直线稳定输出：

```json
{"Type":"LINE","StartPoint":[...],"EndPoint":[...]}
```

圆弧边界若存在，稳定输出 `Type=ARC` + Center/Radius/Angles。

Polyline loop 可继续保留 vertices+bulge。

### DIMENSION

Rotated/Linear 和 Aligned 必须固定：

```text
definition_points:
DimLinePoint
XLine1Point
XLine2Point
```

并：

```text
revit_rebuild_supported = true
```

Radial/Angular 等非本轮 Revit V1 原生重建对象可以保留语义，但不能伪装为 Linear。

## 10. 导出统计对账 — P0

对受控样本记录：

```text
CAD 源对象支持类型统计
MAP counts
REPORT dxf_type_counts
```

要求同一支持对象统计一致。

unsupported 必须单独统计，不得混入支持对象。

## 11. YangTools 3.1.36 Reader 端到端 — P0

优先使用已存在的 YangTools MCP / Revit 2024 自动验证，不要让用户重复人工操作。

用本轮真实 AutoCAD 导出的 `_RVT_MAP.json`，在加载 **YangTools 3.1.36 FINAL** 的 Revit 2024 中真正调用产品 Reader / 打开「CAD转Revit图纸」。

至少记录：

```text
Curves
Texts
Fills
Dimensions
TotalCount
```

要求：

```text
TotalCount > 0
YangCAD MAP 支持对象统计 == YangTools Reader 扫描统计
```

特别确认：

```text
DxfType 不再导致 0 对象
```

## 12. Revit 最小真实转换

若 MCP 可完成，至少从本轮真实 MAP 实际转换：

```text
1 Text
1 Line
1 Hatch
1 Linear Dimension
```

记录真实 Revit ElementId 和类型。

这里必须走产品：

```text
CadRevitExchangeReader
→ Mapping
→ CadToRevitRebuildService
```

禁止用 Python/MCP 直接手工创建 4 个 Revit 对象后冒充端到端 PASS。

如果 GUI/文件选择器确实无法由 AI 操作，标记 `USER_TEST_REQUIRED`，不得自行 PASS。

## 13. 不需要重复的事项

除非本轮代码真实影响到对应模块，否则无需重新做：

- YangTools 四主题视觉测试；
- 已闭环的线性布置 P0；
- 材质绑定完整回归；
- Image Embed。

静态回归仍必须全部跑。

## 14. 原始证据

至少交付：

```text
01_BUILD_LOG.txt
02_AUTOCAD_RUNTIME.md
03_CONTROLLED_RVT_MAP.json
04_CONTROLLED_RVT_REPORT.json
05_REAL_PROJECT_RVT_MAP.json
06_EXPORT_COUNTS.json
07_YANGTOOLS_READER_COUNTS.json
08_E2E_RESULTS.md
09_HASHES.txt
```

有 Revit MCP 原始响应时一并保存。

## 15. 结果枚举

每项只能：

```text
PASS
FAIL
NOT_TESTED
NOT_INSTALLED
USER_TEST_REQUIRED
```

禁止“理论 PASS / 应该没问题”。

## 16. 最终权限

执行器只能写：

```text
TEST EXECUTION COMPLETE
```

最终 GO / CONDITIONAL GO / NO-GO 由 ChatGPT 复核原始证据决定。
