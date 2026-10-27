# Revit 图纸预处理 UI 简化 + 引线修复 V1

状态：`SOURCE IMPLEMENTED / STATIC PASS / AUTOCAD 2024 RUNTIME NOT TESTED`

## 用户日常操作

工具箱 → **Revit 图纸预处理**。

- **一键预处理当前图纸**：沿用既有 Exchange v1 默认参数，原 DWG 不变，输出 `_RVT.dwg`、`_RVT_MAP.json`、`_RVT_REPORT.json`。
- **修复引线 / 关联文字**：把已经炸成线段或原本没有关联文字的 CAD 引线重新转换为原生 MLeader。
- **高级设置**：默认折叠，保留输出目录、文件后缀、扫描块定义、包含布局、几何归一选项。

移除了详情页直接显示的 Exchange v1、ByLayer、BlockReference 等过程描述；技术信息保留在高级设置、日志和 RESULT 中。工具箱卡片只保留一句功能说明。

## YGLEADERFIX 用户流程

1. 点击 **修复引线 / 关联文字**，或在 CAD 命令行输入 `YGLEADERFIX`。
2. 选择一条旧式 `LEADER`、一条开放 `LWPOLYLINE`，或者若干首尾连通且没有分叉的 `LINE`。
3. 按 Enter，点击想要关联的独立 `TEXT/MTEXT`；如只重建无字引线则直接 Enter，然后点选箭头端点。
4. 服务先检查端点拓扑、对象空间、文本类型和方向，再单事务创建原生 `MLEADER`；创建成功后擦除已选旧对象。失败时事务不提交，旧对象保留。
5. 原生 MLeader 的文字和引线现在属于同一个对象，可整体移动或编辑。

文字匹配绝不凭距离全自动猜测。多个 DBText 必须先合并成 MTEXT，否则仅支持选中的单条 TEXT。

### 2026-10-27 静态审查加固

- 修正 DBText → MText 转换：把反斜线、花括号作为**字面文字**转义，避免 `A\B`、`{C}` 被 MText 当作格式代码；原生 MText 的已有格式仍原样传递。
- MText → MLeader 保留原 MText 的 Attachment（锚点），降低位置漂移概率，最终位置仍以 AutoCAD 2024 读回为准。
- 引线顶点除闭合/零长/分叉外，增加**非相邻线段相交或重叠、自交回头、非共面 3D 坐标**安全拒绝，避免生成外观错误的 MLeader。
- 这轮测试为源码审查与静态门禁，尚未调用 Autodesk .NET API 进行实体重建，不能据此宣布运行时 PASS。

### 边界

- 普通开放、仅直线段的 2D Polyline 支持；带 bulge 弧段、闭合环、多分支、非连接线段、样条线跳过。
- 复杂多引线、混有独立箭头三角形/填充的炸开组，**不承诺自动恢复为原样**；可单独选线段重建后核对箭头。
- 旧式 Leader 转 MLeader 采用当前多重引线样式；不保证外观像素级相同。
- 创建独立无文字 MLeader 仅是 CAD 原生恢复。Exchange v1/YangTools Reader 不一定支持在 Revit 创建无文字引线。
- Exchange schema 锁定 `yangcad-rvt-exchange/1.0`，本次未改字段。

## MCP 产品接口

`cad.leader.rebuild`，默认 `dry_run=true`。

```json
{
  "source_handles": ["2AF", "2B0"],
  "text_handle": "2B1",
  "arrow_point": null,
  "keep_originals": false,
  "dry_run": true
}
```

MCP 真实执行需要先开启普通写入许可以及**删除权限**，因为默认成功后删除旧引线/文字。GUI 直接点击选择作为交互授权；默认按单事务执行。MCP stdio 当前增加 1 个产品 Tool，预期合同为 `69/32/62`，实机待验证。

## 待 Gemini 实机必测

- AutoCAD 2024 一键处理命名与未命名 DWG（路径和后缀是否正确）。
- WPF 一键预处理按钮是否在使用、重开 DWG 后正常运行；长任务结果窗口是否不截断错误。
- Polyline + MText -> MLeader：文字内容、原文字高度、位置、图层、箭头方向与端点读回。
- 两段 LINE 拼成折线 + TEXT -> MLeader；反向选择顺序应得到相同链。
- 已有无文字 MLeader + 独立 MTEXT 关联；旧式 Leader + TEXT 关联。
- 独立无文字引线，提示 Exchange v1 不保证 Revit 重建。
- 闭合/断裂/分叉/弧段/不同空间/无效 TEXT 等必须 FAIL，且所有原对象保留。
- 创建后一次 Ctrl+Z，确认旧线和文字都恢复，新 MLeader 消失。
- 运行 `project.revit.prepare` 检查新生成 MLeader Data 包含 `text`、`leader_lines[].points`，再由 YangTools 3.1.36 Reader 真实读取。
- 当前模板 `MLeaderStyle` 不同、中文字体/SHX/MText 混排情况下的变化报告。
- 编译 DLL 与加载 DLL SHA 严格相同，真实运行才可标 GO。
