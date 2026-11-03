# YangCAD 自主功能库：源泉设计 / 贱人工具箱体验参考

日期：2026-11-03 · 状态：**功能规划，不代表全部已实现**

## 1. 原则

- 目标：把自己日常真用得上的能力逐项做成 **YangCAD 自有实现**，允许自己修改流程、默认值和快捷键；不是逆向复制原插件的代码、资源、界面或发布包。
- 源泉设计（YQArch）和贱人工具箱的“功能名称/使用体验”作为需求调研参考。不能由老版本下载站推断官方已完全停更。
- AutoCAD/Civil 3D 原生属性和关联关系优先保留；不为了复制视觉效果盲目 Explode。
- 命令入口稳定，中文简短操作；已有命令不重复造。新功能需要时再补 MCP，避免一次添加数百个半成品。
- 各阶段按独立任务小批次实现，接受 AutoCAD 2024 真编译与 Undo/对照样本验证后才晋升正式功能。

## 2. 已有 YangCAD 对应能力

| 对照体验 | YangCAD 现状 | 继续要做 |
|---|---|---|
| 源泉 QG 切割提取大样 | `YGDETAIL` 静态候选 | 闭合多段线边界、复杂对象裁切策略、标注/引线保留选项 |
| 源泉图层管理/贱人改层 | `YGLAYER`、`YGLMERGE`、`YGLONOFF`、`YGLFREEZE`、`YGLLOCK` 静态候选 | 多选图层、批量应用、外参依赖层细分 |
| 贱人折断线 ZD | 用户新需求已明确 | 原线转可编辑折断线；保留图层/颜色/端点；还未验收 |
| 源泉图纸比例 SD | `YGSCALE` 标准1:1/缩尺双模式 | 标注/引线/动态块真实样本补齐 |
| 源泉命令总览、贱人快捷键 | `YGKEYS` 本轮新增 | PGP 命令缩写可配置；Ctrl/Shift 交给 CAD CUI |
| 旧引线修复 | `YGLEADERFIX` 静态候选 | 多分支/复杂引线作为以后按需扩展 |

## 3. 优先参考库（非承诺排期）

**A. CAD 基本修改（高频，用户需求直接关联）**

- 闭合边界内/外保留与裁切，支持矩形/拾取多段线（需要继续完善真实对象裁切）。
- 直线转折断线：从已有 Line 插入自定义 Z/斜线/波浪标记，可批量，优先做 Z 形。
- 连接直线/圆弧/多段线、批量延伸/修剪/定距偏移。
- 选择性把原图层图元转入目标层，含块插入/内部定义的边界选择。

**B. 文字与标注（工程详图高频）**

- 文字批量查找替换、复制内容和格式、文字对齐与编号递增。
- 标注连接/断开/对齐、标注样式和实际数值保护，优先保留关联标注。
- 炸开的引线恢复原生 MLeader，独立文字关联。

**C. 图块/图框**

- 改图块基点但不改变当前图元世界坐标；快速建块、块属性批改；批量块导出。
- 根据图纸框或闭合轮廓批量出图、图纸比例切换。

**D. 填充/线型**

- 拾取现有填充作为模板、快速填充、重建边界、比例/角度批改。

**E. Civil / Revit 跨应用**

- 已有 CAD→Revit Exchange 仍按 `yangcad-rvt-exchange/1.0`；为真实生产需求新增原生对象重建，不把纯 AutoCAD 视觉图元冒充 Revit 原生对象。

## 4. 本轮交付：快捷键与 Ribbon

- `控制中心 → 快捷键 → 打开快捷键面板`，或 `YGKEYS`。
- 在“功能”下拉选择自己的 YangCAD 工具，输入例如 `YQG` 并保存。可以设为 `QG`，但源泉插件可能也定义 `QG` 命令：PGP 冲突检查不能检测 AutoLISP `c:QG`，所以同时安装时推荐使用唯一的 `Y` 前缀。
- PGP 文件优先由 Autodesk `HostApplicationServices.FindFile` 解析当前使用的 `acad.pgp`。仅修改文件末尾 YangCAD 标记区块、操作前备份、冲突提示、显式允许覆盖。
- 点击“应用到 CAD”，在 AutoCAD 原生 REINIT 窗口勾选“PGP 文件”后确定。不能假称“保存就已经当前会话生效”。
- Ctrl/Shift 等组合键在原生 CUI 设置；本轮不增加系统级键盘 hook。
- Ribbon `YangCAD` 标签只作为附加标签，添加时保留用户原先选中的 CAD 标签；没有有效选中项时尝试恢复原生 Home/常用。

## 5. 调研来源（功能名称/体验，仅用于需求分析）

- YQArch 软件概述与动态快捷键：`https://code.buxiantang.top/file/yqhelp.pdf`
- 源泉快捷键目录/命令帮助（QG、尺寸、图层等）：`https://www.studocu.com/ph/document/university-of-pangasinan/bsce-bs-in-civil-engineering/%E6%BA%90%E6%B3%89%E8%AE%BE%E8%AE%A1%E5%BF%AB%E6%8D%B7%E9%94%AE%E8%AF%B4%E6%98%8E%E6%96%87%E4%BB%B6-v667/155939323`
- 贱人工具箱功能索引（文字、尺寸、块、折断线）：`https://www.cncrk.com/copy/84537.html`
- 贱人工具箱快捷键导出/导入使用说明：`https://www.zzzcad.com/collection/jrcad`
- Autodesk 官方 AutoCAD 命令缩写/组合键机制：`https://help.autodesk.com/cloudhelp/2024/ENU/AutoCAD-DidYouKnow/files/GUID-D2B4BF16-B1F7-4EF2-89AF-88ACE85FAD73.htm`

## 6. 验证状态

`IMPLEMENTED / STATIC TEST ONLY / AUTOCAD 2024 PGP + CUI + RIBBON RUNTIME REQUIRED`。

禁止将文档研究条目计入“已实现”的工具数量或 MCP Coverage。
