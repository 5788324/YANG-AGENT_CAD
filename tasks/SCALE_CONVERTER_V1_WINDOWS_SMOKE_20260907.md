# Gemini / Windows 执行任务：YangCAD 图纸比例转换 V1

日期：2026-09-07  
执行器：Gemini（仅执行，不拥有 FINAL GO 权限）

## 0. 证据纪律

- 不允许仅写“已测试 / PASS / 正常”。
- 每个 PASS 必须有原始数值、命令输出、截图或保存后的 DWG 证据。
- Build PASS != Loaded PASS != Product PASS。
- 不允许用直接 AutoCAD API/Python primitive 冒充 `YGSCALE` / `cad.scale.convert` 产品链。
- 如果某项做不到，写 `NOT_TESTED`，不得猜测。
- 最终只提交证据给 ChatGPT 独立复核，不自行宣布 FINAL GO。

## 1. Build / Deploy / Loaded

记录：

- Windows / AutoCAD 版本；
- build command；
- Build DLL path / bytes / SHA-256；
- deploy path / SHA-256；
- AutoCAD PID / start time；
- AutoCAD 实际 Loaded Assembly path / SHA-256；
- 要求 Build == Deploy == Loaded（若无签名步骤）。

## 2. UI Smoke

真实桌面截图：

1. Ribbon 出现 `MCP / 工具箱 / 比例转换 / 控制中心`；
2. 打开 `比例转换` 窗口；
3. 截图标准模式；
4. 截图缩尺绘图模式；
5. 墨曜主题下不得出现 RadioButton 样式异常、白底黑字泄漏、窗口启动异常。

## 3. 受控测试 DWG

模型空间至少创建：

- Line：长度 200；LinetypeScale = 1；
- DBText：Height = 3.5；
- MText：TextHeight = 3.5；
- Rotated/Aligned Dimension：测量几何 200，Dimlfac = 5，显示目标 1000；
- Radial/或 Diametric Dimension；
- Leader；
- MLeader；
- Hatch：PatternScale = 5；
- 普通 BlockReference；
- 带 Attribute 的 BlockReference，Attribute Height = 3.5。

另准备可验证跳过项：至少 Xref 或 Annotative 对象之一；Civil 3D 环境存在时再加一个 Civil 对象。

保存 `00_BEFORE.dwg` 并记录 SHA。

## 4. P0-A：缩尺绘图 1:5 → 1:10

必须通过真实 `YGSCALE` GUI 产品入口执行：

```text
mode = scaled_drawing
old = 5
new = 10
base = 0,0,0
geometry_factor = 0.5
```

执行后至少记录：

- Line 长度：200 → 100；
- DBText Height：3.5 → 3.5；
- MText TextHeight：3.5 → 3.5；
- Dimension 几何：200 → 100；
- Dimension Dimlfac：5 → 10；
- Dimension 显示值：仍为 1000（允许格式字符但数值必须一致）；
- Hatch PatternScale：5 → 5；
- Line LinetypeScale：1 → 1；
- 普通 Block 的插入位置/ScaleFactors 确实按 0.5 变化；
- Attribute Height：3.5 → 3.5；
- 跳过对象有明确统计而命令继续完成。

保存 `01_SCALED_5_TO_10.dwg`、截图、属性输出。

## 5. P0-B：一次 Undo

紧接 P0-A 执行 **一次** Ctrl+Z / U：

必须重新读取并记录：

- Line 长度恢复 200；
- Text/MText 恢复；
- Dimension 几何与 Dimlfac 恢复；
- Hatch / LinetypeScale 恢复；
- Block / Attribute 恢复。

保存 `02_AFTER_ONE_UNDO.dwg`。

如果需要两次以上 Undo 才恢复，FAIL。

## 6. P0-C：缩尺绘图反向 1:10 → 1:5

从受控输入执行：

```text
geometry_factor = 2.0
```

至少证明：

- 100 → 200 或等价受控几何 ×2；
- TextHeight 保持；
- Dimlfac 按 `old / geometry_factor` 反向正确；
- Hatch / LinetypeScale 保持。

## 7. P0-D：标准 1:1 模式 1:5 → 1:10

准备真正 1:1 的受控对象，例如 Line 长度 1000。

通过 GUI：

```text
mode = standard_1to1
old = 5
new = 10
geometry_factor = 1
annotation_factor = 2
```

必须证明：

- Line 几何长度：1000 → 1000；
- DBText Height：例如 17.5 → 35；
- Dimension Dimlfac：保持原值，不因为比例变化修改；
- Dimension `Dimtxt/Dimasz/Dimgap/Dimexe/Dimexo` 等可读有效值按约 2 倍变化（实际存在的字段逐项记录）；
- Hatch PatternScale：例如 5 → 10；
- Line LinetypeScale：1 → 2；
- 普通 BlockReference 在标准模式应被安全跳过并报告 `standard_mode_block_ambiguous` 对应说明，不得被偷偷缩放。

再执行一次 Ctrl+Z，以上全部恢复。

## 8. P0-E：异常/跳过不拖垮整批

同一 Selection 中包含：

- 至少 2 个可转换普通对象；
- 至少 1 个明确不支持/跳过对象。

要求：

- 普通对象仍转换；
- skipped > 0；
- failed 不得因为安全跳过而增加；
- 命令不崩溃；
- 一次 Undo 可恢复已转换对象。

## 9. P0-F：MCP dry-run / real

先确认 MCP contracts：

```text
stdio = 68
AutoCAD live = 31
Civil 3D live = 61（Civil 3D 宿主测试时）
```

对同一组 Handle：

1. `cad.scale.convert(... dry_run=true)`：
   - 返回 geometry_factor / annotation_factor / processed/skipped；
   - changed_handles 应为空；
   - DWG 属性实测不变。
2. `cad.scale.convert(... dry_run=false)`：
   - 必须走 current PID / current document binding；
   - changed_handles 返回真实 Handle；
   - 与 GUI 相同 Service 业务结果一致。

提交 MCP 原始 request/response，不要只提交摘要。

## 10. 输出证据包

至少包含：

```text
00_BUILD_DEPLOY_LOADED.json/md
01_UI_STANDARD.png
02_UI_SCALED.png
03_TEST_INPUT.dwg
04_GUI_SCALED_RESULT.json/md
05_GUI_STANDARD_RESULT.json/md
06_UNDO_RESULT.json/md
07_MCP_DRYRUN_RAW.json
08_MCP_REAL_RAW.json
09_BEFORE_AFTER_PROPERTIES.csv/json
10_FINAL_EVIDENCE_REPORT.md
```

若 Gemini 为解决编译问题修改源码：

- 必须列 changed files + diff 摘要；
- 返回完整 FINAL_SOURCE ZIP；
- 返回新 SHA-256；
- 不得静默修改验收标准或删除 fail-closed 边界。

## 11. 判定

Gemini 只能写：

- `EVIDENCE COMPLETE — SUBMIT TO CHATGPT FOR FINAL REVIEW`
- `NO-GO — FAILURE EVIDENCE ATTACHED`
- `INCOMPLETE — UNPROVEN ITEMS REMAIN`

禁止自行写 `FINAL GO`。
