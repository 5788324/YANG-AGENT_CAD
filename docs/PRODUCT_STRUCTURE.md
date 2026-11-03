# YangCAD 产品架构蓝图

状态：2026-11-03 设计已锁定，当前仅实现部分 UI；不宣称完整版全部落地。

## 定位

以 AutoCAD/Civil 3D 作为主宿主，以个人工程高频 CAD 操作为中心：**可停靠面板 + 原生命令 + 轻量 Ribbon + MCP**。

### 交互分工

| 入口 | 适合什么 | 不应该做什么 |
|---|---|---|
| YangCAD Ribbon | 一键打开工具箱、几项极高频编辑、MCP/控制中心 | 不挤满 100 个按钮、不抢 CAD 默认常用 Tab |
| 停靠工具箱 PaletteSet | 日常主界面：常用命令、图纸任务、分类入口 | 不对每条工具展示技术字段或执行链 |
| 命令行/PGP | 快速调用，适合熟练绘图者 | 不劫持系统键盘输入 |
| 控制中心 | 主题、快捷键、关于、诊断 | 不重复所有绘图功能 |
| MCP | AI 与自动化操作，可读回结构化结果 | 不把后台线程直接暴露给 Autodesk DB |

### 工具箱分类（目标设计）

1. **常用**：最近使用与常用命令；当前阶段先提供静态快捷入口。
2. **图形编辑**：比例转换、裁剪大样、删除边界外、折断线等；其中部分尚未实现。
3. **图层**：合并、开关、冻结、锁定；当前单独窗口。
4. **文字与标注**：引线修复/文字关联；后续文字批改/标注对齐。
5. **图纸与引用**：图片/外参内嵌、CAD→Revit 预处理。
6. **批处理**：多 DWG 处理，按已有 Batch Worker 能力逐步开放 GUI。

此表是目标信息架构，已实现范围以 `docs/FEATURE_CATALOG.md` 为准。

## 代码结构

- `Commands/`：CAD 命令生命周期、用户拾取。
- `UI/`：Ribbon/Toolbox/ControlCenter/单功能简窗，只有表现与交互。
- `Services/Cad/`：真实业务规则与 DTO（GUI/MCP 共享）。
- `Toolbox/`：面板工具 Descriptor/Adapter；现有两个长任务型 Descriptor 保留。
- `Mcp/Tools/`：面向 AI 的薄 Adapter 与结果回读。
- `Infrastructure/CadTransactionRunner.cs`：统一文档/事务/Undo 边界。
- `mcp/`：Python stdio 与 Gateway 路由；`BatchWorker/` 单独批处理。

不要为实现本蓝图而重构已验证核心，不做无收益的复杂框架迁移。
