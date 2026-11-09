# YangCAD — AI 综合源码审查快照（2026-11-09）

**性质：代码审查用全集，不是可发行产品，也不是已经通过 Windows 编译/AutoCAD 2024 实机测试的集成版本。**

仓库：https://github.com/5788324/YANG-AGENT_CAD  
分支：`review/full-source-20261109`  
稳定基线：`main@2f0980d4acd08b087d924b5c1f5a5bb18e8668a8` (AutoCAD 2024 RC1 的**有限测试范围**曾验收；Civil3D 和 Revit native 不应宣称通过）。

## 源码来源（逐分支精确 HEAD）
| PR | 功能 | 来源 HEAD | 原状态 |
|---|---|---|---|
| [#13](https://github.com/5788324/YANG-AGENT_CAD/pull/13) | `YGBREAKLINE` 直线 Z 折断 | `433a1e8224ddae60bedc056817d2a7712608a430` | Draft，G5 未通过完整原生证据复核 |
| [#14](https://github.com/5788324/YANG-AGENT_CAD/pull/14) | `YGDETAIL` 多边形提取 | `bb4d877c07eb448a44659bebe065b00ca699274a` | Draft，部分 native Undo、标注跳过证据有疑点 |
| [#15](https://github.com/5788324/YANG-AGENT_CAD/pull/15) | `YGOUTSIDE` 删除/裁切边界外 | `38be3c21a3ac0be7dc6485ac23ef78f8bc6a678b` | Draft，原生 GUI/Undo/Loaded DLL 证据待补 |
| [#16](https://github.com/5788324/YANG-AGENT_CAD/pull/16) | `YGDL` 双线/管道、`YGDLW` 中心固定改宽 | `6fc978fba63862c019cc7d28ae6f5b6fda4a8693` | Draft，Gemini Windows 实机测试进行中 |
| [#17](https://github.com/5788324/YANG-AGENT_CAD/pull/17) | `YGDIMTXT` 文字覆盖标注筛选、`YGSLOPE` 坡度、`YGTBOX` 文字加框 | `f7bfaedc8148fb26587cf8f687def9f07ce2c619` | Draft，GitHub G1 静态 CI 通过，G2–G5 尚未执行 |

原有 `main` 保留 RC1 功能、静态检查、C# .NET AutoCAD SDK 集成、Civil3D 兼容框架和 Revit 图纸交换逻辑。此分支不是“PR 已合并”：**review/ 是复制候选源码并消除同名文件冲突的快照，不改变任何 PR 的状态，不得直接合并到 main。**

## 在这一分支里整合了什么
- 五组原生命令及 Service 的源文件，保留每个 PR 的完整源代码和 Windows 测试 SOP。
- PR #14 和 #15 共享一份同 Git Blob 的 `PolygonBoundary2d.cs`；无并行几何算法。
- `UserSettings.cs` 同时包含 PR #16 的 `DoubleLine*` 设置和 PR #17 的 `Slope*`/`TextFrame*` 设置及 Normalize 保护。
- `ToolboxView.xaml` / `.xaml.cs` 整合 `YGBREAKLINE`、`YGOUTSIDE`、`YGDL`、`YGDLW`、`YGDIMTXT`、`YGSLOPE`、`YGTBOX` 七个新快捷按钮（不移除旧入口）。
- `ShortcutAliasService.cs` 合并对应七个命令的 `YGKEYS` 目录，保留原有 PGP 冲突/备份保护。
- PR #13 的 Ribbon “直线折断”入口以及 UI 策略检查保留。
- `PROJECT_TREE.txt` 和 `SHA256SUMS.txt` 将按最终快照的全部文件重新生成；不是任一单独 PR 的清单复制。

## 让其他 AI 优先审核什么
1. **真实编译与 API**：本快照尚未在 Windows AutoCAD 2024 + .NET Framework 4.8 执行 Build。优先看 Autodesk 实体类型、`GetOffsetCurves`、`MText.GetBoundingPoints`、`DBText` 克隆测量、`RegApp`/XData 序列化是否真实可用；不接受“Python CI 通过 = AutoCAD 可用”。
2. **五组功能同一程序集的静态兼容**：命令注册是否冲突、Toolbox UI 与 XAML 事件、快捷键目录、合并后的 UserSettings 序列化及版本迁移、原有 RC1 行为回归。
3. **几何正确性**：凹多边形多穿越裁切、圆弧正负方向、Offset 内外边线、倒角/弯头中轴几何、可重复 600→1000→600 无漂移、UCS 坐标、标高、模型和布局空间。
4. **图元与事务**：异常时事务内所有新实体是否回滚；真正用户级 `_.U` 后是否一次恢复（不能以 Transaction.Abort 冒充）；对象 Handle/XData/属性是否意外变化；复杂对象的保守跳过是否遗漏。
5. **新标注工具**：`YGDIMTXT` 只识别 `Dimension.DimensionText` 手工文字覆盖；`YGSLOPE` Section 的 XY 坡度和 Plan3D 的 Z 坡度分开；`YGTBOX` 旋转 TEXT / MTEXT、实际文字宽度、中文字形、外扩距离、当前层与布局环境。
6. **安全与兼容**：禁止静默删除生产 DWG、不要动用户现有 Steam/CAD 插件、源泉 XD 和另一个 LISP 的 `YGDL` 命令不能混载。MCP stdio 74 / AutoCAD 37 / Civil 67 契约不能因工具集更新被悄悄改掉。

## 研究边界与建议输出
- 优先做**只读源码审查**，不对这一审查分支写入修复。需要修改时，应在明确的独立修复分支或对应 PR 进行，记录精确 commit。
- 若未在 Autodesk 宿主运行，标 `STATIC_ONLY` 或 `NOT_TESTED`；不得声称 `BUILD_PASS`、`LOADED_PASS`、`NATIVE_UNDO_PASS`。
- 每一条问题需提供：文件路径、可定位的代码行/符号、可复现条件、严重级别 P0/P1/P2、预期结果、最小修复建议、与现有 PR 关联。
- 已知风险或验收不足请参考 [Issue #12](https://github.com/5788324/YANG-AGENT_CAD/issues/12) 和每个 PR 的 Windows smoke task。
- 审查快照为各 PR **代码并置后的工作树**，不保证已经解决所有跨模块运行时冲突。**切勿将此代码直接部署到真实生产绘图环境**。

## 检查入口
- `docs/AI_START_HERE.md`、`docs/HANDOFF.md`、`docs/CURRENT_STATE.md`。
- `tools/run_static_checks.py`、`tools/verify_source_manifest.py`。
- `src/YangCAD.AutoCAD/Commands/`、`src/YangCAD.AutoCAD/Services/Cad/`。
- `tasks/*WINDOWS_SMOKE*.md` 是各原功能验收说明，非已完成证据。
