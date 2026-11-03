# YangCAD

**可定制的 AutoCAD / Civil 3D 工具箱，内置 MCP / AI 自动化能力。**

> **当前版本：`0.10.0-dev` · 开发预览（STATIC PASS，Windows 新功能尚未统一验收）**  
> 当前活跃开发以 GitHub 分支和 PR 为准：`main` 是稳定回退基线，功能分支是待验收候选。不要把 `STATIC PASS` 当作 `Runtime GO`。

## 开始使用

插件以 AutoCAD/Civil 3D 的 **YangCAD Ribbon**、**可停靠工具箱**、**CAD 命令行**三种方式使用：

- Ribbon：工具箱、比例转换、图层工具、大样提取、MCP、控制中心；默认不抢占 CAD 自带“常用”选项卡。
- 工具箱：常用命令快捷入口，以及“Revit 图纸预处理”“引用内嵌”等图纸处理任务。
- 命令行：`YGLAYER` 图层管理、`YGDETAIL` 提取大样、`YGSCALE` 比例转换、`YGLEADERFIX` 引线修复、`YGKEYS` 快捷键管理。

> 本仓库以 **源码**为主。开发版不保证可直接安装；真实可用性以绑定到对应 Git SHA 的 Windows 编译、加载、DWG 结果与 Undo 验收为准。

## 已有能力与状态

| 模块 | 功能 | 当前新增部分状态 |
|---|---|---|
| CAD 编辑 | 图层合并、开关、冻结、锁定；QG 风格提取大样 | 开发候选，待 AutoCAD 2024 实机 |
| 标注辅助 | 比例转换双模式、炸开引线重建、文字关联 MLeader | 开发候选，待专门实机 |
| 图纸处理 | Revit 图纸预处理、图片转 OLE、DWG 外参绑定 | 新增/变更部分待实机 |
| 操作效率 | 可停靠工具箱、快捷键缩写管理、Ribbon | 交互升级待实机 |
| AI / 自动化 | 本地 MCP 网关、批处理、扩展工具 | 历史核心有实机记录，当前新增接口待回归 |

当前契约：**74 个 MCP stdio 工具 / 普通 AutoCAD 37 个 live / Civil 3D 67 个 live**（源码预期值，不是本次运行结果）。

## 给新 AI / 新对话

请依次阅读：

1. [`AGENTS.md`](AGENTS.md) — 项目纪律与最短入口
2. [`docs/CURRENT_STATE.md`](docs/CURRENT_STATE.md) — 当前事实、状态与风险
3. [`docs/HANDOFF.md`](docs/HANDOFF.md) — 代码接手、下一步
4. [`docs/FEATURE_CATALOG.md`](docs/FEATURE_CATALOG.md) — GUI / 命令 / MCP 对照
5. [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — 程序分层（详见补充 `docs/PRODUCT_STRUCTURE.md`）
6. [`tasks/CURRENT_TASK.md`](tasks/CURRENT_TASK.md) — 唯一正在进行的任务

**严格区分**：代码存在 ≠ 静态测试通过 ≠ Windows 编译通过 ≠ AutoCAD 真正运行通过 ≠ 最终 GO。历史证据不得冒充当前候选。

## 目录导航

| 路径 | 内容 |
|---|---|
| `src/YangCAD.AutoCAD/` | AutoCAD 命令、服务、MCP live Tools、WPF 界面 |
| `src/YangCAD.Civil3D/` | Civil 3D 专用功能 |
| `src/YangCAD.Core/` | 共享契约 |
| `src/YangCAD.BatchWorker/` | AcCoreConsole 批处理 |
| `src/YangCAD.Setup/` | 安装/更新/修复/卸载 |
| `mcp/` | Python MCP stdio 桥接 |
| `workers/ImageEmbed/` | 图片内嵌 STA / PowerShell worker |
| `docs/` | 产品、架构、接口、限制与历史证据 |
| `references/` | 源泉与贱人工具箱参考索引；**不放第三方未授权二进制** |
| `tasks/` | 当前任务与 Windows 真实验收清单 |
| `tools/` | 构建、测试、封包脚本 |

## 开发与验证

Windows（已安装对应 Autodesk SDK/宿主）使用 `tools/Build.ps1 -Year 2024` 进行构建；按 `docs/BUILD_DEPLOY.md` 和 `docs/RELEASE_CHECKLIST.md` 部署、收集证据。无宿主环境仅可做静态验证：

```bash
python tools/verify_source.py
python tools/verify_release_source.py
python tools/test_product_handoff_policy.py
```

不要上传 Autodesk 官方 DLL、用户 DWG、token、二进制缓存或未经许可的第三方插件。

## 产品设计与贡献

- [UI 结构与交互规则](docs/PRODUCT_UI.md)
- [参考插件与独立开发规则](references/README.md)
- [版本与发布策略](docs/VERSIONING.md)
- [Git 协作工作流](docs/GIT_WORKFLOW.md)
- [兼容性与已验证范围](docs/COMPATIBILITY.md)
- [历史开发记录](WORKLOG.md)

**项目性质**：个人工作流工具，中文界面，优先少点击、原生对象、可撤销；与 Autodesk、源泉设计及贱人工具箱均无官方关联。第三方名称仅用于功能对照。

## 开发、测试与 AI 接手（2026-11-03）

新 AI/新对话从 [`docs/AI_START_HERE.md`](docs/AI_START_HERE.md) 开始；当前可执行静态门禁见 `docs/QUALITY_GATES.md`，Windows 实机待测范围见 `docs/RUNTIME_TEST_MATRIX.md`。Gemini 统一任务：`tasks/GEMINI_WINDOWS_INTEGRATED_SMOKE_20261103.md`。CI 只验证源代码/策略测试，不等于 AutoCAD Runtime GO。父 PR 依赖关系见 `docs/BRANCH_DEPENDENCIES.md`。
