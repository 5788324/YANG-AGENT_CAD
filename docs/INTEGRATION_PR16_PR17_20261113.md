# YangCAD：PR #16 与 PR #17 源码整合（2026-11-13）

## 来源与范围

只整合两个独立验收的 PR：
- PR #16 `6fc978fba63862c019cc7d28ae6f5b6fda4a8693`：YGDL 连续绘制普通双线／管道、YGDLW 保持中轴对称改宽。原 Core Console G5 结论：https://github.com/5788324/YANG-AGENT_CAD/pull/16#issuecomment-6848545441
- PR #17 `f7bfaedc8148fb26587cf8f687def9f07ce2c619`：YGDIMTXT 尺寸文字覆盖筛选、YGSLOPE 坡度标注、YGTBOX 文字矩形／圆框。原 Core Console G5 结论：https://github.com/5788324/YANG-AGENT_CAD/pull/17#issuecomment-6848546242

不包含尚未 G5 放行的 PR #13/#14/#15 或综合审查 PR #18，也不覆盖源泉/贱人工具箱命令。

## 共享文件处理

- `UserSettings`：保留双方的 `DoubleLine*`、`Slope*`、`TextFrame*` 属性及 Normalize；上次宽度与文字/斜率参数继续持久化。
- `ToolboxView.xaml`／`.xaml.cs`：新增加的 5 个命令均有中文按钮与允许列表。
- `ShortcutAliasService.cs`：五个命令均进入快捷键目录。
- `PROJECT_TREE.txt`、`SHA256SUMS.txt` 由整合后实际文件生成，不复用任一 PR 的过期清单。
- 所有 Service / Command 算法源文件保持原 PR 的字节内容。

## 门禁与下一轮验收

- 已完成：PR #16、PR #17 分别在 Windows AutoCAD 2024 Core Console 的原生用户命令、API 几何与原生 `_.U` 验证。
- **未完成：合并后的同一 DLL** 在 Windows AutoCAD 2024 的重新构建与 NETLOAD、原生 GUI Toolbox 按钮与鼠标拾点、即时显示及用户配置升级回归。
- GitHub Python source/contract CI **不等于** Autodesk 的 Windows 编译、加载、绘图与 GUI 测试。应提供合并后 SHA、DLL SHA、真实宿主 PID、完整 Build log 与至少五个命令最小回归。
- 未提供正式可分发 DLL；请勿将源码合并等同于正式产品发布。
