# FEATURE BACKLOG

状态：**UNSCHEDULED / ON-DEMAND**。仅记录候选需求，不代表 NEXT、排期或承诺开发。真实工作需要时再选 1–3 个 Tool 进入任务。

## CAD

### 图层
- **Revit 图纸预处理 / YangCAD RVT Exchange v1**（ACTIVE：`project.revit.prepare`；已从“按显示特性分离图层”升级，见 `RVT_EXCHANGE_V1.md`）
- 隔离 / 反向隔离
- 冻结 / 解冻
- 锁定 / 解锁
- 图层匹配
- 批量改图层
- 图层合并
- 删除空图层
- 图层标准检查
- 图层命名检查

### 绘图 / 修改
- 中心线
- 多段线辅助
- 等距线
- 批量平行线
- 坡度线
- Z=0
- 多段线去重复点
- 自动闭合
- 对齐
- 等距分布
- 批量旋转 / 缩放

### 标注
- 连续标注
- 快速标注
- 批量标注
- 对齐标注
- 标注间距统一
- 标注样式检查
- 坐标标注
- 高程标注

### 文字
- 对齐
- 等距分布
- 合并 / 拆分
- 自动编号
- 前后缀
- 查找替换
- 数字递增
- 数值加减乘除
- 文字高度 / 样式统一

### 块
- 块统计
- 块替换
- 属性修改
- 属性提取
- 相同块选择
- 图块库

### 清理 / 检查
- 图纸健康检查
- 重复对象
- 多段线异常
- 空图层
- 未使用块 / 样式
- Z 值异常

## Civil 3D

### Surface
- 信息
- 高程查询
- 边界检查
- 统计
- QA

### Alignment
- 路线信息
- 半径检查
- 最小半径
- 曲线检查
- Polyline → Alignment
- 偏移路线
- 桩号工具

### Profile
- PVI 表
- 坡度检查
- 最大 / 最小坡度
- 变坡点
- 地面线 / 设计线对比
- 挖填高度
- Profile 数据导出

### Section / SampleLine
- 后续规划

### FeatureLine / Grading
- 信息
- 高程 / 坡度检查
- 断点检查
- QA

### Corridor
- 信息
- Baseline
- Region
- Code
- 诊断
- QA

### PipeNetwork
- Pipe / Structure 信息
- 管径
- 坡度
- 管底高程
- 覆土
- 连通
- 逆坡
- 最小坡度
- 最小覆土
- 管网 QA

### PressureNetwork
- 后续规划

## Project / Batch
- **批量图片内嵌（OLE）**：已进入 `FEATURE-PROJECT-IMAGE-EMBED-001`，当前 STATIC IMPLEMENTED / WINDOWS PENDING；权威规范见 `docs/IMAGE_EMBED_V1.md`。
- DWG 批处理
- 外参管理
- 图纸标准检查
- 批量打印
- 批量导出
- 项目 QA 报告

## AI / MCP
- 自然语言工作流
- 多 Tool 编排
- Agent Extension
- Batch Agent
- 检查中心


## Toolbox Framework P1

Claude Round 0 review 记录，非 Round 1 blocker：

- `ToolboxPaletteManager.Shutdown()`：后续评估 Dispose
- Toolbox 专用 Ribbon 图标
- `system.mcp-status` Adapter 未来改成结果/展示分离模板
- `ToolboxRegistry` UI-thread-only 假设注释
- 若未来真有需要，再强化 `CadExecutionContext` 构造限制；当前不增加复杂度
