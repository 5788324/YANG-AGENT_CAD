# Gemini：YGDETAIL 多边形 V1 Windows AutoCAD 2024 原生测试

**对象**：`feature/detail-polygon-v1-20261105` 的完整最新 Git SHA。
基线：`main@2f0980d4acd08b087d924b5c1f5a5bb18e8668a8`。
**禁止**继承 AutoCAD RC1 的 Build/Loaded DLL SHA 或旧 R04 PASS；新 C# 必须重新编译实测。
本任务为独立 Draft PR；不得直接修改 main、不得合并、不得宣布 FINAL GO。

## G0–G3

1. Windows AutoCAD 2024 / net48，干净工作区；记录 git status、SHA、2024 Build 命令、完整 binlog、ExitCode 和编译后的 DLL SHA-256。
2. 记录 AutoCAD PID、准确版本、真实 NETLOAD/部署路径、Build / Deploy / Loaded DLL SHA 一致性。所有图纸使用**复制的受控 DWG**，保留源 DWG SHA before/after。
3. 确认 Ribbon/Palette 调用 YGDETAIL，命令行有三个模式，默认 Rectangle 的老路径仍正常。

## G4 功能矩阵（每个对象均读回 Handle、类型、顶点/端点、长度、图层和关键属性）

- Rectangle：用既有 R04 矩形 fixture 重测一组，证明同一个命令未破坏旧功能。
- Select：拾取闭合凸多边形 / **凹多边形**，选择 1 倍、2 倍；原边界 Handle 与坐标不变，新框是原生闭合 Polyline。
- Draw：逐点指定 3 / 5 个顶点，回车闭合；源图不新增多边形，新图位置符合目标插入点。
- LINE：完全内部、完全外部、一次穿越、多次穿越凹多边形、沿边界共线、顶点相切，确认交点、段数和线端点。
- ARC、CIRCLE：完全内、完全外、跨 1/2/多个边，尤其凹边界多交点；验证切分后是原生 ARC 或完整 Circle，弧心/半径/端角符合几何预期。
- LWPOLYLINE：含直边和**源图曲线 bulge**的跨界内容，验证 Explode+精确裁切、属性与源图保护。
- 边界 bulge：必须明确报错**拒绝**，不得无声近似。另测非闭合、零面积、自交、重复顶点、多段线不是 WCS XY、宽度非零。
- 块、动态块、含属性块、DIM、关联 HATCH、MLeader、PDF/图片外部参照：完全安全在内时按策略保留、跨界或危险情况明确 skip 与 warning；源对象和 Xref 源文件不变。
- 已冻结/关闭/锁定图层：所有源对象不被直接修改；不可安全复制的项目明确 skip。
- Model 与 Paper 各一组；非零标高 WCS XY 边界与一条 3D LINE 的定位/裁切单独核对。
- 对于一次提取后执行**原生 `_.U`**（或 Ctrl+Z），生成对象消失、源对象不变；保存、关闭、重开结果依旧正确。
- 运行 dry_run（如能从受控调用路径触发）必须完全不增加源对象，不得把人工未执行测试写为 PASS。

## 提交原始证据

按场景附 `before.json` / `after.json`、原始输入/输出 DWG、副本 SHA、DWG 对象级 JSON、真实命令行/GUI 截图、Undo readback、完整 PowerShell 命令/ExitCode/binlog、新 Build/Deploy/Loaded 对账表。逐项 PASS/FAIL/BLOCKED/NOT_TESTED；一条失败先保留证据，不要偷偷跳过或修补样本。

最终回复：`READY_FOR_CHATGPT_REVIEW — git=<full_sha> — PASS=N FAIL=N BLOCKED=N NOT_TESTED=N — evidence=<Drive link>`。
