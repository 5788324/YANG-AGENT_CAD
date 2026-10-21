# YangCAD 0.9 UI / MCP / Toolbox Closeout — Windows 2024 Smoke

状态：**READY / REQUIRED BEFORE FINAL GO**

## Scope

只验证本轮 0.9 收口，不新增业务功能。

## P0-1 Clean build / load

- AutoCAD 2024 clean build + load PASS；
- Civil 3D 2024 clean build + load PASS；
- runtime.log 无 unhandled exception；
- contract：AutoCAD 28 / Civil 58 / stdio 65。

## P0-2 Ribbon / visible product surface

YangCAD Ribbon 必须准确只有：

1. MCP
2. 工具箱
3. 控制中心

要求：

- 没有可见 “MCP 状态”按钮；
- MCP 点击打开独立 MCP Window；
- 工具箱只显示 Revit 图纸预处理、批量图片内嵌；
- 不显示 System / Planning / legacy CAD/Civil / 参数面板自检；
- 控制中心只显示系统设置、关于；不显示 MCP 页面。

## P0-3 Three-theme visual smoke

分别切换：

- 墨曜 · Obsidian
- 月瓷 · Porcelain
- 雾蓝 · Mist

逐个检查：

- Control Center
- MCP Window
- Toolbox
- Toolbox 参数 ComboBox / 文件路径控件 / Result 区

重点：Obsidian 不得出现默认 WPF 白底 + 黑字导航/下拉区域；文字可读，选中态、边框、滚动区域无明显主题断层。

## P0-4 MCP status / self-check

AutoCAD 2024：

- MCP Window 显示 `CAD 网关就绪`；
- self-check expected = 28；
- critical tools PASS。

Civil 3D 2024：

- expected = 58；
- Civil critical tools PASS；
- 不允许只有 28 个 AutoCAD Tool 时误报 PASS。

## P0-5 MCP full + partial self-heal

### Healthy bundle

- `Contents\Mcp` 完整；
- 复制配置 command 指向健康安装 runtime；
- stdio server 启动；
- `cad.status` PASS；
- clipboard 无 Gateway Token / Bearer。

### Missing runtime

- 临时移走 `Contents\Mcp`；
- 点击复制 MCP 配置；
- `%LOCALAPPDATA%\YangCADAgent\runtime\Mcp` 自动恢复完整 runtime；
- stdio + `cad.status` PASS。

### Partial corruption

- 恢复安装目录；
- 保留 `Start-YangCAD-Mcp.ps1`，仅删除或篡改 `yangcad_mcp\server.py`；
- 再次复制 MCP 配置；
- 不得使用损坏安装 runtime；
- 必须回退 embedded healthy runtime；
- `cad.status` PASS。

## P0-6 Revit Exchange closeout regression

以前的 RVT Exchange v1 核心实机 PASS 不作废，但本轮 C# 有收尾变更，必须跑定点回归：

1. 建一个源 DWG，其中预先存在一个会与生成规则同名的 Exchange layer；
2. 将该层设置 Off / Frozen / Locked / Non-Plottable；
3. 执行 Revit 图纸预处理；
4. source DWG 状态不变；
5. output clone 中该 Exchange layer 必须 On / Thawed / Unlocked / Plottable，并保持生成语义的 color/linetype/lineweight；
6. 包含一个 dynamic block reference，REPORT 必须出现 `dynamic_block_references_detected > 0` 和 warning；
7. 既有 `_RVT.dwg/_MAP/_REPORT` 基本输出回归 PASS。

## P0-7 Image Embed

继续执行：

`tasks/FEATURE_PROJECT_IMAGE_EMBED_001_WINDOWS_SMOKE.md`

必须特别证明：

- `PASTECLIP` 产生 `AcDbOle2Frame`；
- ModelSpace / PaperSpace 正确；
- 移走原图片后关闭/重开 DWG 仍可见；
- source SHA 不变；
- output 不覆盖。

## PASS / GO

只有 P0-1 ~ P0-7 全通过，当前 `0.9.0-dev` closeout candidate 才能升为最终 Windows GO。
