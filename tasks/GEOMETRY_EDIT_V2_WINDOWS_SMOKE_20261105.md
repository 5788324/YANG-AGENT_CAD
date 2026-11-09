# Gemini Windows 2024 验收 — 图形编辑 V2（Issue #12）

**基线**：本轮 `feature/geometry-edit-v2-20261105` 精确 SHA。祖先 `main` 包含 0.10.0 AutoCAD 2024 RC1；以前的已验收 DLL SHA `880329CF...` 不可再用作新构建成功的替代证据。

## A. 折断线 V1（本轮代码）

1. 确认 Windows 10 + Autodesk AutoCAD 2024、工作区 clean、精确 Git SHA；新代码执行 `tools/Build.ps1 -Year 2024` 并记录真正的 DLL SHA、Build/Deploy/Loaded 同一性。
2. Ribbon / Palette / CAD 命令 `YGBREAKLINE` 可见可运行；点击 2D LINE 的 25%、50%、75% 位置，生成 `LWPOLYLINE`，实际 6 顶点；原 LINE 消失，新 Polyline 原起点/终点与原线一致，图层/颜色/线型/线宽一致，图形外观为 Z 形。
3. 原生 `_.U` 一次：原 LINE 恢复，新 Polyline 消失，读回 Handle / 端点；取消命令不产生未提交更改。
4. 长短线、竖直/斜线、不同绝对 Z 的 3D LINE 转 `POLYLINE3D`；原线 Z 端点不变。保存/关闭/重开 DWG 读回仍正确。
5. 已锁定层、块内部、外参、非 LINE：拒绝/不更改源图、无异常崩溃；模型空间和布局空间分别测试；至少一组含原始颜色/线型属性。
6. 快捷键 PGP 列表含 `YGBREAKLINE`，原有快捷键不丢失。

## B. 边界裁切 / 删除边界外（后续代码，不得预报 PASS）

- `YGDETAIL`：矩形、拾取/画闭合 Polyline、凸/凹多边形、bulge 弧段，源对象 Handle 原封不动；边界相交的 LINE/ARC/CIRCLE/POLYLINE 裁切，复杂块/标注/Hatch 可选模式分别验收。
- `YGOUTSIDE`：同上三种输入；保留内部、删除完全外部、剪断跨界，锁定层/外参/复杂对象不应被静默破坏；必须支持预览和一次原生 Undo 回滚，原始 DWG 做 SHA 保护或测试副本。
- 边界自交、非闭合、短线、零面积、边界外对象都必须有明确行为；处理 Model/Paper 对象时不编辑外部参照源。

所有真实测试输出：源 DWG + before/after JSON + screenshots + Undo readback + binlog + Git/DLL SHA 及未测项；在 Issue #12 链接证据，Gemini 只提交 READY_FOR_CHATGPT_REVIEW。
