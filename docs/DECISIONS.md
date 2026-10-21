# Decisions

- **D-001 MCP First**：第一个功能就是 MCP Gateway + Tool 系统。
- **D-002 SDK v2**：新项目直接 `mcp>=2,<3`。
- **D-003 Python 与 Autodesk Host 分离**：Python 不加载 Autodesk DLL。
- **D-004 AutoCAD/Civil 分程序集**：Civil API 只在扩展 DLL。
- **D-005 localhost + random token**：不开放公网。
- **D-006 Handle First**：AI 长期引用实体用 DWG Handle。
- **D-007 写操作统一 dry_run/log/transaction**。
- **D-008 不默认开放任意命令执行**：优先结构化数据库 Tool。
- **D-009 主题不污染 AutoCAD 全局 WPF**。
- **D-010 2021–2027 独立构建**：net48 / net8 / net10 分代。
- **D-011 Ribbon 不是核心协议**：UI 是人工入口，MCP Tool 是主接口。
- **D-012 精简 Git / 单主线**：个人项目以 `main` 为唯一开发线和事实源，不强制一任务一分支/PR。每轮保持少量清晰提交，重要稳定节点可打 tag；旧 YANG-AGENT_CAD history/archive/backup 不恢复。Git 写入默认仍由用户指定的 Windows/Codex 执行者完成，除非用户明确要求 ChatGPT 直接操作仓库。
- **D-013 Generic MCP Fallback**：专用 Tool 缺失时优先 object/property → API；不直接把任意反射 setter 当万能接口。
- **D-014 Escape Hatch 默认关闭**：`cad.command.run` 与 `cad.extension.invoke` 单独授权，默认 false；command 不允许异步假成功。
- **D-015 Host-owned Extension Transaction**：动态扩展由 YangCAD Host 提供唯一 Transaction，扩展使用版本化 DLL，不修改 Core Tool contract。
- **D-016 Curated Themes**：0.2.0 起只保留墨曜 / 月瓷 / 雾蓝三套主题；UI 以 MCP 诊断和权限为主，不堆面板。
- **D-017 Civil Production Read-First**：TASK-006B 先只开放 FeatureLine / Corridor 强类型读取与诊断；不在读路径中隐式 Rebuild，不新增写 Tool。任何后续 Civil 写能力必须基于真实需求另立门禁。

- **D-018 Batch DWG Process Isolation**：TASK-006C 的 `batch.*` 由 Python MCP 本地编排独立 `AcCoreConsole.exe` + `YangCAD.BatchWorker.dll`；不进入当前 acad.exe Gateway/ToolRegistry。v1 每 DWG 一个串行进程。real hygiene 默认禁止，需 `dry_run=false + allow_write=true`，且只 SaveAs 新 output 副本；输入 DWG SHA 前后必须相同。

- **D-019 Managed Agent Extension Workflow**：TASK-007 的 AI-generated Extension 由 Python MCP 本地 builder 生成固定 Contract wrapper，只接受 Execute body；编译/安装不经过 live Gateway。managed artifact 必须版本化并带 manifest，绑定 target year/product、source/DLL SHA 与当前 Host DLL SHA。live load/invoke 继续复用 `cad.extension.list/invoke`；write 由 Host-owned Transaction 控制，dry-run Abort，real 单 Transaction；change ledger 为 Extension 自报证据，不宣称 Host 独立 diff。

- **D-020 Quick Install During Iteration**：项目频繁迭代阶段默认使用 User-scope `ApplicationPlugins` + 精确 YangCAD `TRUSTEDPATHS` 快速安装；保持 `SECURELOAD`，不信任宽泛目录。`APPAUTOLOAD` 仅启用启动/发现插件所需 bit。AuthentiCode/TrustedPublisher 正式签名链保留，稳定发布后再切换为默认。


- **D-021 Compatibility Evidence Is Per-Year / Per-Layer**：兼容性必须按年份、AutoCAD/Batch/Civil、build/runtime 分项记录；`NOT_INSTALLED` 不得外推 PASS，build PASS 不得外推 runtime PASS。Compatibility matrix 使用 schema v2 分离 `autocad_build / batch_build / civil_build / runtime_*`。
- **D-022 Runtime Matrix Uses Exact-Year Isolation**：RELEASE-002 runtime 每次只验证一个精确年份宿主，记录 PID/product/version/document，不跨年份复制 DLL。用户于 2026-08-28 确认 2021 工作已结束，因此 2021 可重新纳入 runtime matrix；此前“2021 GUI 不触碰”仅是工作占用期间的临时测试约束，不是架构边界。

- **D-023 Toolbox Product Direction Supersedes Old UI Priority**：YangCAD 长期定位为“CAD / Civil 3D 多功能生产力工具箱 + MCP / AI 自动化核心”。保留 D-011“Ribbon/UI 不是核心协议、业务逻辑不得绑死在 UI”这一架构原则，但废止其中“MCP Tool 是产品主接口”的产品优先级表述；GUI Toolbox 与 MCP/AI 是两种并列入口，日常高频确定性操作优先 GUI，复杂/批量/组合任务优先 MCP/AI。D-016 的“UI 不堆面板”条款失效；墨曜 / 月瓷 / 雾蓝三套主题约束继续有效。

- **D-024 GUI / MCP Share Services, Not Each Other**：GUI Toolbox 与 MCP Tool 必须复用同一业务 Service / Request 语义，但 GUI 不允许通过 MCP/HTTP 绕回 Host，MCP 也不得复制一份独立业务实现。入口只负责输入适配、上下文获取与执行调度。

- **D-025 CadExecutionContext Is Runner-Owned**：新 Toolbox Service 不允许自行读取 `Application.DocumentManager.MdiActiveDocument`；Autodesk 上下文必须由执行入口经 `CadTransactionRunner` 提供。`CadExecutionContext` 只包含 `Document / Database / Editor / Transaction`，且**只能由 `CadTransactionRunner` 构造**，禁止 Service/Command/Adapter 直接 `new`。为避免影响现有 65 个 MCP Tool，不修改现有 `Read/Write(Func<Transaction,T>)` 签名；未来采用纯加法、明确命名的 `ReadContext/WriteContext`（或等价唯一命名）入口，避免 `Func<Transaction,T>` 与 `Func<CadExecutionContext,T>` 同名重载造成 lambda overload ambiguity。

- **D-026 Descriptor Is Static, Adapter Is Runtime**：`ToolDescriptor` 只承载可序列化静态声明：`Id / Name / CategoryPath / Description / IconKey / Host / SupportedYears / DeclaredStatus / IsWrite / SupportsDryRun / SupportsGui / SupportsMcp / SupportsBatch / SearchKeywords / ParameterShape / HelpId`。`IsAvailable(context)`、`Execute(...)` 等运行时行为属于 `ToolRuntimeAdapter`，不得塞进 Descriptor 的可变状态。

- **D-027 ParameterShape Is Separate From Request Values**：`ParameterShape` 只描述参数形状与基础校验，不保存运行时参数值；Service 使用强类型 Request。第一阶段仅支持 `Text / Number / Bool / Choice / Layer / ObjectSelection / Point / File`，复杂工具允许自定义 ParameterView。ObjectSelection 入口优先转换为 Handle，沿用 D-006 Handle First。

- **D-028 Toolbox Uses Personal-Project Safety Model**：TASK-007 的 source-policy、动态 DLL manifest/SHA、AI-generated-code trust 防线只适用于动态/不可信 Extension，不扩散到固定编译进主程序的 Toolbox Tool。Toolbox 仅保留防崩溃、误改图纸与破坏输入的必要边界：Autodesk thread/document affinity、Transaction/rollback、基础 Undo、Batch isolation、输入文件保护。普通 GUI Tool 不引入 RBAC、审批流或 `allow_xxx` 权限。

- **D-029 Toolbox Write Safety Is Three-Level and Dry-Run Is Optional**：普通只读工具直接执行；普通写工具默认 `Transaction + Ctrl+Z`，不增加确认；批量删除、覆盖、Purge、大规模自动修复等高破坏工具按需提供一次确认或 Preview/dry_run。`SupportsDryRun` 是能力声明，不是所有 Toolbox 写工具的义务。D-007 的统一 dry_run/log/transaction 继续适用于既有 MCP/AI typed write Tool contract；不得解释为 Toolbox GUI 所有写工具都必须实现 dry_run。

- **D-030 GUI Descriptor and MCP Schema Stay Lightly Coupled**：TOOLBOX-001 第一阶段不建立“大一统 Domain Tool Metadata”。GUI Descriptor 与 MCP schema 可保持独立文案/展示模型，仅共享 Service、强类型 Request，并在确有收益时共享最小 `Id + ParameterShape`。禁止为了自动生成所有 UI/MCP/help/schema 而提前平台化。

- **D-031 Demand-Driven Toolbox Expansion**：TOOLBOX-001 Round 0–3B 完成并通过实机后，框架证明阶段结束。后续不再为了路线图批量预开发 CAD/Civil/Project 功能；真实工作中出现需求时再立小任务，默认每轮 1–3 个 Tool，优先复用/提取共享 Service，做与风险匹配的最小 static + Windows smoke 后合回 `main`。`FEATURE_BACKLOG.md` 只是候选池，不代表排期或承诺；Civil / QA / Project 等原 Round 4/5 内容降级为按需 backlog。 具体执行规范统一见 `docs/AI_FEATURE_DEVELOPMENT_STANDARD.md`，所有 AI 开发者必须遵循。

- **D-032 YangCAD RVT Exchange v1**：`project.revit.prepare` 定位为 CAD→Revit 中间交换预处理器，而非普通图层规范器。V1 固定输出 `*_RVT.dwg + *_RVT_MAP.json + *_RVT_REPORT.json`，覆盖 Geometry/Text/Leader/Dimension/Hatch 五类。活动 DWG 不写；通过 `Database.Wblock()` 克隆当前内存数据库，只在 detached output Database 上重新分层并 SaveAs。几何层名保持 `#0.25 Continuous Red` 兼容现有 Revit Line Style 工作流；注释/标注/填充采用 `RVT-*` 稳定分类；完整重建语义进入 MAP。普通块定义只做诊断扫描，不发布 definition-space 子实体；BlockReference 因 V1 无实例 Transform 合同而进入 unsupported_items。匿名/system block 与 Xref 不硬改，ByBlock 无法完全解析时必须 Warning。RVT Exchange schema 进入 GO 后只做 additive 演进，破坏性变更升级 schema version。


- **D-033 Batch Image Embed v1**：图片内嵌定位为批量 output-copy 工具，不在 live acad.exe 中循环打开大量图纸。GUI 只创建 job；独立 PowerShell/COM worker 启动与当前编译年份一致的 `AutoCAD.Application.<series>`，在 working-copy DWG 中使用 Windows Clipboard + `PASTECLIP` 创建 `AcDbOle2Frame`，校正几何后才删除对应 RasterImage。输入 DWG 永不原位保存并记录 SHA before/after；默认 require-all，clipped/brightness-contrast-fade/transparency/nested block RasterImage 不能静默降级为 PASS。
