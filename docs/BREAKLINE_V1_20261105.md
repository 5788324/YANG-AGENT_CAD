# 直线转 Z 折断线 V1 — 2026-11-05

Issue：[#12](https://github.com/5788324/YANG-AGENT_CAD/issues/12)；当前状态：**STATIC_ONLY / WINDOWS_NOT_TESTED**。

## 目标与操作

- CAD 命令：`YGBREAKLINE`，或者 Ribbon「直线折断」/ 停靠工具箱「直线折断」；可在 `YGKEYS` 自定义命令缩写。
- 在图中点击普通 `LINE`，点击位置自动成为折断中心。原 `LINE` 被一条原生可编辑 `LWPOLYLINE` 替换；空间直线使用 `POLYLINE3D`。
- 不需要重新指定端点；输出曲线保留原有**起点、终点、图层、颜色、线型、线宽等普通图元属性**；每点一条独立事务，用户可使用一次原生 `U` 撤销该条转换。
- 自适应折断宽度：默认 14% 线长，折断中心距离线两端至少 20%，宽度限制不会越过原始端点。命令内部 `Request.SymbolWidth` 已提供绝对宽度，后续可开放设置。
- 回车/ESC 结束，继续点击可转换下一条。

## 明确的边界

- 目前只支持当前模型/布局空间内的 **LINE**；`Polyline` / `Arc` / 块内部线 / DWG 外参 / 锁定、冻结或隐藏图层不会被强制处理。
- 不修改块定义或外参源文件，不转换现有复合标注；不承诺保留 LINE 对象原始 Handle、拓展实体数据或外部 ObjectId 关联。输出为新图元，因此 Handle 会变。
- 与 AutoCAD 原生 `BREAK` 不同：本功能是**造型折断符号**，不是实际剪断模型几何。
- 单次选择只有一次 CAD Transaction；这仍然需要 Windows 真机检验 Undo 和重开 DWG 后的几何。

## 代码

- `src/YangCAD.AutoCAD/Services/Cad/BreaklineService.cs`：验证、坐标投影、2D/3D Polyline、属性复制和事务内原线擦除。
- `src/YangCAD.AutoCAD/Commands/BreaklineCommands.cs`：CAD 交互拾取和错误处理。
- `RibbonBootstrapper.cs` / `ToolboxView.xaml` / `ShortcutAliasService.cs`：三个常用入口。
- `tools/test_breakline_policy.py`：静态源代码策略测试，**不是** Autodesk 编译或运行测试。

验收按 `tasks/GEOMETRY_EDIT_V2_WINDOWS_SMOKE_20261105.md`。不把 V1 的开发状态写入 0.10.0 RC1 的已验收功能表。
