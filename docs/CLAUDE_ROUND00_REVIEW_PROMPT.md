# Claude Review Prompt — YangCAD TOOLBOX-001 Round 0

你现在作为 **YangCAD 的第二架构/代码审查者**。

请基于我提供的完整仓库 ZIP：

`YangCAD_FULL_REPO_MAINLINE_ROUND00_20260828.zip`

进行一次**只审查、不主动大重构**的静态评审。

## 项目定位

这是个人工作用 AutoCAD / Civil 3D 插件箱，不是企业产品。

优先级：

1. AI 快速开发功能；
2. 快速迭代；
3. 低维护成本；
4. 尽快上线干活；
5. 足够稳定，不把 AutoCAD/Civil 3D 搞崩。

不要以企业级安全、平台化、通用框架完整度作为主要优化目标。

## 当前事实

Contract：
- MCP stdio = 65
- AutoCAD live = 28
- Civil 3D live = 58

已关闭：
- TASK-001~007
- RELEASE-001
- RELEASE-002 Phase A
- RELEASE-002 Phase B1 Civil 2022/2023/2024

Phase B1：
- 2022/2023/2024 Civil health=58
- PVI PASS
- 2022/2023 legacy Station fallback
- 2024 RawStation
- Batch PASS
- runtime exception=0
- FeatureLine/Corridor = SAMPLE_MISSING limitation

当前：
- TOOLBOX-001 Round 0 = STATIC IMPLEMENTED / WINDOWS NOT TESTED

## Round 0 新增

重点审：

- ToolboxPaletteSet
- ToolboxView.xaml/.cs
- ToolDescriptor
- ToolRegistry
- ToolRuntimeAdapter
- ParameterShape
- CadExecutionContext
- CadTransactionRunner 新 ReadContext/WriteContext
- Ribbon 工具箱入口
- ControlCenter MCP / 系统设置 / 关于
- settings 中 Recent/Favorites 数据口
- `tools/test_toolbox_foundation_policy.py`

## 已锁定决策

必须尊重 `docs/DECISIONS.md` D-023~D-030：

- GUI/MCP 并列入口
- GUI/MCP 共享 Service
- Descriptor 静态 / Adapter runtime
- CadExecutionContext 只能 Runner 构造
- 不改现有 65 Tool Read/Write transaction API
- ParameterShape 与强类型 Request 分离
- TASK-007 动态代码安全不扩散
- Toolbox 三档安全
- dry_run 按需
- GUI/MCP metadata 第一阶段轻耦合

## 不要建议

除非发现真实 blocker，不要建议：
- DI Container
- CQRS
- Mediator
- EventBus
- Repository Pattern
- Plugin Loader
- RBAC
- 审批系统
- 完整 fuzzy search
- 现在统一所有 GUI/MCP metadata
- 现在重写现有 65 MCP Tool
- 现在大规模迁移已有 Service

## 你需要回答

### A. 总结论
只能选：
- GO TO WINDOWS ROUND 1
- GO WITH SMALL STATIC FIX
- NO-GO

### B. P0
只列会导致：
- 编译失败
- AutoCAD/Civil 启动崩溃
- PaletteSet 生命周期明显错误
- 文档/事务越界
- 现有 65/28/58 contract 回归
的问题。

### C. P1
最多 5 条，必须是低成本高收益。

### D. 过度设计检查
指出当前 Round 0 有没有为了框架而框架。

### E. AI 新增 Tool 摩擦
用一个例子评估：
“以后新增一个文字数值 +100 工具，要改几个文件？”
如果超过合理范围，请给最小简化建议。

### F. Round 1 Windows SOP
审查 `tasks/TOOLBOX_001_ROUND_01_WINDOWS_UI_SMOKE.md`：
- 是否够；
- 哪一项可删；
- 哪一项必须补。

## 输出要求

中文。
引用具体文件/类/方法。
不要只讲原则。
不要直接改源码。
不要因为没有 Windows 环境就把整个项目判 NO-GO。
