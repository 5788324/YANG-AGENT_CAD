# Gemini Windows AutoCAD 2024 实机测试：YGOUTSIDE V1

源代码：`feature/outside-boundary-v1-20261105` 的**完整最新 SHA**。测试机 Windows + AutoCAD 2024 net48。
不得继承历史 RC1 DLL SHA、PR #13/14 Windows 状态；未经运行不得标 PASS。测试专用 DWG 必须先复制并 SHA-256 留档，不碰用户原始图纸。

## G0–G3 身份和 Build
1. Clean git status、精确 commit SHA、AutoCAD PID/版本、PowerShell 5.1 环境。
2. `tools/Build.ps1 -Year 2024` 的全部命令、退出码、binlog、DLL SHA；部署路径和真正 Loaded DLL 路径/SHA 必须与本 SHA 构建匹配。
3. 验证命令行 `YGOUTSIDE`、停靠工具箱按钮、`YGKEYS` 内快捷键入口。

## G4 图元级真实测试
- Rectangle / Select / Draw 三种边界，凸/凹多边形；自交、开口、零面积、重复点、带 bulge 边界必须拒绝，且无源图修改。
- LINE：完全内/外、多次穿过凹多边形、顶点相切、共线重叠；保存前后输出段数、起止点和原 Handle 的 **before/after**。
- ARC/CIRCLE：完全内/外、与多边形相交 0/1/2/更多次，验证原生 ARC/CIRCLE、圆心/半径、角度、图层和精确端点。
- Polyline：纯直线与内容 bulge Arc；全内不改变 Handle，跨界按原生拆分，源图属性与最终段数可读回。
- 复杂跨界 BLOCK / 动态块 / 属性块、DIM、HATCH、MLeader、普通文字：复杂跨界必须 skip，完全位于外侧的允许类型按策略处理；外参、关联 HATCH、Raster/PDF/Viewport、受保护图层不修改。
- CurrentSpace：模型空间和布局空间独立测试，保留另一个空间不变；已有外部参照的源 DWG 不变。
- **预览**：记录扫描数、删除/修剪/新段/跳过数，执行 No 或 Esc -> DWG before/after 一致；变更预览后对象，必须拒绝过期计划。
- **确认**：Yes 后实际修改对象与预览计划一致；完整的原生 `_.U` 一次后对比 source Handle、几何和输出对象，确认 Undo 真实粒度。
- 保存、关闭、重开受控输出 DWG，做最终 readback。
- 检查部分弧线 OCS/标高异于 XY、锁定图层、关联对象、海量图元上限保护。

## 证据
记录原始 before.json / after.json / undo_readback.json、输入 DWG SHA、截图、Build/Deploy/Loaded DLL SHA、PowerShell 命令和日志、必要的对象 Handle/类型/顶点；每条 PASS/FAIL/BLOCKED/NOT_TESTED，不能以静态代码或模拟脚本代替 AutoCAD 运行。
Windows 测试者只回复 `READY_FOR_CHATGPT_REVIEW — git=<full-sha> — PASS=N FAIL=N BLOCKED=N NOT_TESTED=N — evidence=<Drive link>`；**不合并 PR、不宣布 FINAL GO**。
