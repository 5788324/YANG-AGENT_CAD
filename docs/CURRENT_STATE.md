## 2026-11-04 独立 Windows 证据复核与发布元数据修复（定点候选）

- Gemini 受测源码提交 `b6ff1370b0ed48e5e50fec43d2a5a2e14ca5ec54` 已进入 GitHub；独立核验新版证据 ZIP SHA、发布 ZIP SHA 和真实打包 AutoCAD DLL SHA 均与报告吻合。
- AutoCAD 2024 相关 11 个测试阶段原始证据具有有效价值，但 R08 Civil 3D live、R09 Revit Native E2E 仍为 `NOT_TESTED`；不得声明完整产品跨宿主 GO。
- 发现发行脚本 `Package-Release.ps1` 的 `release-manifest.json.contract` 仍固定旧值 `68/31/61`，而实际 MCP 为 `74/37/67`。
- 本修复仅纠正发行清单数量，强化 `Verify-Release.ps1` 对实际打包 Python server 工具数量/重复名称的校验，并在静态验证中防止回归。**当前修复仍需 Gemini 执行 Windows 2024 定向重新打包及校验；原 b6ff 已验收 DLL 不因元数据修复改变。**
- 禁止修改既有受测 `b6ff137` 提交、不得将修复后的未测试 release 工件冒充旧 release；新的发行 ZIP 必须有独立 SHA 和测试记录。

# YangCAD 当前状态（唯一事实入口）

更新时间：2026-11-03  
目标版本：**0.10.0-dev**  
当前结论：**PROJECT STRUCTURE REFRESH / STATIC CANDIDATE / WINDOWS BUILD AND RUNTIME REQUIRED**。

## 事实边界

- `main` 当前为既有稳定回退基线；新功能在 `feature/*` Draft PR 上逐步叠加，不能假装都已进入主分支。
- 2026-11-03 源码中预期 **MCP 74 stdio / 37 AutoCAD live / 67 Civil live**；它是**静态合同**，本轮不声明真机返回值。
- 已有历史底层 MCP/Toolbox/Batch/Exchange 运行证据，但 **图片内嵌 V2、比例转换、引线修复、图层工具、QG 风格提取大样、快捷键面板等当前增量尚未整体验收**。
- YangTools 3.1.36 Reader 的历史稳定性不能代替当前 CAD→Revit Exchange 的真实 11/11 E2E。
- 2026-09-08 Gemini 的部分报告被独立审计否定，历史报告不可直接用于当前 GO（`docs/CHATGPT_GEMINI_EVIDENCE_AUDIT_20260908.md`）。

## 本轮已动的内容

- 产品定位与交接文档重建：README / AGENTS / PROJECT / HANDOFF / 功能目录 / 版本规则 / Git 流程。
- 新建 `references/`：源泉与贱人工具箱的功能、来源、版权说明和差异表；不包含非授权程序。
- Ribbon 拆分常用工具、连接与设置两组；Toolbox Palette 加入高频命令入口，保留两个专业产品卡片和原本业务 Service。
- 控制中心“关于”显示动态开发版本、宿主、进程、Loaded DLL、用户设置路径及 GitHub 链接。
- 版本元数据统一为 **0.10.0-dev**（Product/File/Assembly、Python MCP、Bundle、Release 脚本），不是正式发布。

## 当前核心功能状态

| 模块 | 命令/能力 | 状态 |
|---|---|---|
| 图层工具 | `YGLAYER` / `YGLMERGE` / `YGLONOFF` / `YGLFREEZE` / `YGLLOCK` | 静态候选；2024 实机未验 |
| 大样提取 | `YGDETAIL`（当前矩形裁切 V1） | 静态候选；闭合多段线/复杂裁切后续开发 |
| 比例转换 | `YGSCALE`（真实 1:1 / 缩尺绘图） | 部分旧证据；当前增量仍待实机 |
| 引线修复 | `YGLEADERFIX`、`cad.leader.rebuild` | 静态候选；箭头方向和文字关联待实机 |
| 图片/外参内嵌 | `project.image.embed`（OLE / Xref Bind） | 静态候选；PaperSpace / reopen 等待实机 |
| Revit 预处理 | `project.revit.prepare`、Exchange v1 | 历史核心 PASS；本轮增量需真实 CAD/Revit 对账 |
| 快捷键 | `YGKEYS`、PGP 缩写/原生 CUI | 静态候选；PGP/REINIT 待实机 |
| 基础 MCP/Civil/Batch | 既有功能 | 历史已有部分真实验收；新版本需要回归 |

## 本轮明确没有完成

- 没有 Windows dotnet / Autodesk 2024 Build、DLL Loaded SHA、Ribbon/Palette UI 截图和真机 Undo。
- 没有验证当前候选 Civil 3D live 67 tools。
- 没有实机证明 QG 与源泉等价，也没有完成任意闭合多段线及复杂图元裁切。
- 没有对外发布稳定版或上传第三方插件二进制。

## 下一步

按 `tasks/CURRENT_TASK.md` 统一执行 Windows 定点测试；修编译/运行问题时必须重新绑定源码 SHA。最终 GO 需审查原始 AutoCAD DWG、属性 readback、截图、日志与 Loaded DLL。

## 2026-11-03 — 统一质量门禁 / Gemini Windows 任务（STATIC ONLY）

新增源代码 CI、统一测试脚本与 SHA manifest 校验、GitHub Issue/PR 模板、Gemini 实机执行 SOP、R00–R10 状态矩阵和 PR 分支依赖图。**无 Windows 业务实机结果新增，0.10.0-dev 仍为 STATIC_ONLY。**
