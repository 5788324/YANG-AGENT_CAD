# YangCAD · 项目总览

更新时间：2026-11-03  
版本：**0.10.0-dev**  
状态：**结构与文档整理的静态候选，Windows 实机待验收**。

## 产品目标

建立一个能持续扩展的**个人 AutoCAD / Civil 3D 生产力工具箱**：点一次完成日常动作，通过统一服务将同一功能提供给 GUI、命令和 MCP，并能在必要时与 Revit / YangTools 对接。

优先级：**实际能用 > 简单交互 > 原生 CAD 对象与 Undo > 统一代码结构 > 外观效果**。不为了扩展性引入过度框架。

## 产品界面

1. CAD 原生 Ribbon：YangCAD 标签的轻量入口，默认不替换 CAD 首页。
2. 可停靠 PaletteSet：主要工具箱，常用操作入口、图纸处理。
3. CAD 命令 + PGP 快捷键：不修改键盘底层，不强行替换用户 CUI。
4. 控制中心：主题、快捷键、关于和本机诊断；MCP 另有专门窗口。

产品结构见 `docs/PRODUCT_STRUCTURE.md`；交互规范见 `docs/PRODUCT_UI.md`。

## 架构边界

`CAD 命令 / WPF Palette / MCP Adapter → Shared Service → CadTransactionRunner → Autodesk API`

Batch/Worker 单独隔离。MCP stdio 通过 host gateway，不允许后台线程直接调用 Autodesk API。所有新增写操作考虑 Undo、预览/取消和当前 Document binding。

## 当前功能与验收

详见 `docs/FEATURE_CATALOG.md` 和 `docs/CURRENT_STATE.md`。当前静态 MCP 合同为 **74 / 37 / 67**，不等于本轮运行验证。历史可用功能与新增待验收功能明确分开。

## 开发与交接

源码基线：GitHub `main`；开发中的 `feature/*` 分支 + Draft PR。完整源码 ZIP 可作为候选快照，但不取代 Git 历史。

- 当前任务：`tasks/CURRENT_TASK.md`
- 接手手册：`docs/HANDOFF.md`
- Windows 证据清单：`docs/RELEASE_CHECKLIST.md`
- 版本规则：`docs/VERSIONING.md`
- 参考工具：`references/README.md`

未经 Windows 编译和实机核验，不标记 Stable / Runtime GO。
