# Gemini Windows/AutoCAD 2024 — Revit Prep Simplification & MLeader Repair V1

执行角色：Windows 实机执行器；不得自行宣称 FINAL GO。

## 输入

当前 `feature/revit-prep-leader-repair-20261026` 候选源码。先核对源码 ZIP SHA / Git commit，并确保 Build=Deploy=Loaded SHA。

## 优先任务

1. 新 UI：截图确认普通用户只看到“一键预处理当前图纸”和“修复引线 / 关联文字”；“高级设置”默认收起。普通保存 DWG 运行一键预处理，验证三联产物和原 DWG SHA 不变。
2. `YGLEADERFIX`：用实际 `LINE×2 / Polyline / Leader / None MLeader` + `TEXT/MTEXT` 的受控 DWG 分别执行。输出 before/after 属性 JSON，包括 Handle、DxfName、Layer、TextHeight、Text/MTEXT content、MLeader vertices / arrowhead / MTEXT location。
3. **真实一次 Ctrl+Z**，给出 CAD 命令历史和 before/after/after-undo readback；不能只写 `undo_semantics` 字符串。
4. 失败场景：断裂、分叉、闭合、非零 Bulge、MLeader 已带文字、不同布局、没有文字也没有指定箭头，必须不损坏原数据。
5. MLeader 与独立文字关联后，再执行 `project.revit.prepare`，检查 RVT_MAP 中实体包含 `MULTILEADER`、`leader_lines[].points` 和 `text`。可选启动 Revit 实测 YangTools 3.1.36，否则标 `NOT_TESTED`。
6. MCP `cad.leader.rebuild` dry-run 和 real：验证 PID/Document binding、写权限与删除权限；统计预期 `69/32/62`，Civil 3D 无宿主就 `NOT_TESTED`。

## 必须上传

原始 DWG、三个阶段的属性 JSON、完整 CAD 命令记录、构建 log + loaded SHA、界面截图（非黑屏）、生成 Exchange MAP/REPORT、失败栈、Git commit SHA。

真实失败时，停止验收并报告具体复现条件，**不要掩饰为通过，也不要直接覆盖 main**。
