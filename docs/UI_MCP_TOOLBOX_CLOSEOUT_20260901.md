# YangCAD 0.9 UI / MCP / Toolbox Closeout Review

日期：2026-09-01  
结论：**STATIC PASS / TARGETED WINDOWS RETEST REQUIRED**

## 1. 用户要求

本轮按产品收敛要求执行：

1. MCP 作为大核心，单独保留一个 Ribbon 一级按钮；
2. Toolbox GUI 只保留 Revit 图纸预处理、批量图片内嵌；
3. System / Planning / 旧 CAD/Civil GUI 卡片删除可见入口；
4. 修复 Obsidian UI、MCP 状态/自检/自愈、Revit 收尾及相关静态回归。

## 2. 最终可见 UI

### Ribbon

- MCP → `YANGCAD_MCP`
- 工具箱 → `YANGCAD_TOOLBOX`
- 控制中心 → `YANGCAD`

`YANGCAD_MCP_STATUS` 仅作为隐藏命令行诊断入口，不再有 Ribbon/Toolbox 可见按钮。

### Toolbox

准确为两个 descriptor：

- `project.revit.prepare`
- `project.image.embed`

无 System、Planning、CAD Audit/Cleanup、Civil GUI、Recent/Favorites/Search、参数面板自检。

### Control Center

只剩：

- 系统设置 / 三主题
- 关于

没有 MCP 页面、MCP 权限或 MCP 状态卡。

## 3. Theme fix

截图中的 Obsidian 白色左导航来自原生 WPF `ListBox` 默认样式漏出。

修复：

- `NavigationList`
- `ThemeList`
- implicit `ListBoxItem`
- `ComboBoxItem`
- `Separator`
- `ScrollBar`
- `ResultTextBox`

全部使用 DynamicResource theme brushes。三主题资源 key 完整一致。

## 4. MCP fix

- 独立 `McpWindow` 承载连接、复制配置、诊断、自检、日志和写入权限；
- `LocalGateway.IsRunning` 检查 listener / CTS / loop task / port；Dispose 时清零 listener/port；
- UI 文案由“在线”改为准确的“CAD 网关就绪”；
- self-check：AutoCAD expected 28，Civil 3D expected 58，并检查关键 Tool；
- copy config 只在 live Gateway 可用时生成；
- clipboard 继续 token-free，只传 current PID；
- installed MCP root 不再只看 launcher，完整验证 7 个 runtime assets 的内容；
- launcher 存在但 pyproject/module 缺失或陈旧同样触发 embedded self-heal；
- embedded materialize 后再次健康校验；
- PowerShell launcher 要求 `mcp` package major version 2。

Contract 未变化：65 / 28 / 58。

## 5. Revit fix

`project.revit.prepare` 核心协议不变：source read-only → Wblock clone → output triplet。

收尾：

- `DeclaredStatus = Available`；
- generated/reused RVT exchange layer 强制 On / Thawed / Unlocked / Plottable；
- dynamic block reference 计数并写 warning/report；
- anonymous/system definitions 继续安全跳过，不直接改 AutoCAD 动态匿名表示；
- UI 描述改为边界准确，不宣称特殊对象“完整转换”。

## 6. Toolbox UX fix

- File / Folder / FileOrFolder 增加原生选择按钮；
- output folder 可手输尚未创建的新目录；
- FileOrFolder 必须指向存在的 DWG 或目录；
- 结果 TextBox 可滚动并复制；
- 去除无意义分类侧栏，因为只剩两个工具。

## 7. Static result

- verify_source：`cs=62 xaml=8 python=19 themes=3 tools=65 xml=14 errors=0`
- release source：`errors=0`
- UI Surface：`11/11 PASS`
- XAML parse：`8/8 PASS`
- Toolbox Foundation：`16/16 PASS`
- Toolbox Interaction：`13/13 PASS`
- Legacy CAD read GUI retirement：PASS
- Legacy CAD write GUI retirement：PASS
- RVT Exchange v1：`26/26 PASS`
- MCP config/self-heal：`28/28 PASS`
- Image Embed：`26/26 PASS`
- Extension：`12/12 PASS`
- Civil compatibility：`8/8 PASS`
- Quick installer：`10/10 PASS`
- Internal SHA manifest：`183/183 PASS`

## 8. Remaining gates

当前环境不是 Windows + AutoCAD/Civil 3D 2024，因此以下不能伪造为 PASS：

- net48 AutoCAD/Civil 3D 2024 真编译；
- Obsidian/Porcelain/Mist 实际 WPF 渲染 smoke；
- MCP partial-runtime self-heal 实机；
- copied MCP config → stdio bridge → `cad.status`；
- Revit Exchange 本轮图层状态/dynamic-block-report delta 定点回归；
- Image Embed `PASTECLIP → AcDbOle2Frame`；
- PaperSpace 实际落点；
- 移走源图片后关闭并重开 DWG 仍可见。

因此本轮 0.9 candidate 最终状态是 **STATIC PASS / WINDOWS TARGETED RETEST REQUIRED**。RVT Exchange 的历史核心闭环仍有效，但本轮新增 C# delta 不能在未编译/未 smoke 的情况下直接继承最终 GO。
