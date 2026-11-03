# AGENTS.md — YangCAD 项目接手规则

## 先读

1. `README.md`
2. `docs/CURRENT_STATE.md`
3. `docs/HANDOFF.md`
4. `docs/FEATURE_CATALOG.md`
5. `docs/PRODUCT_STRUCTURE.md`
6. `tasks/CURRENT_TASK.md`
7. `docs/AI_FEATURE_DEVELOPMENT_STANDARD.md`
8. 所涉功能的 `docs/*` 规范及 `tools/test_*_policy.py`

完整权威顺序见 `docs/DOCUMENT_AUTHORITY.md`。

## Git 事实源

- `main`：稳定回退基线，不允许把未经实机验收的功能当作已发布。
- `feature/*`：开发候选；Draft PR 用于审查与 Windows 验收。**查看分支/PR 的最新 HEAD，不从日期猜。**
- 2026-11-03 当前开发线上最新增量以 `tasks/CURRENT_TASK.md` 为准；旧 PR 可能是父分支，提交前确认基点。
- 用户允许覆盖仓库，但常规开发不要无理由 force push；任何版本发布必须明确 SHA 和状态。

## 代码与安全边界

- 优先使用现有 `Service / Request / Result`；GUI、命令、MCP 共享业务层，不重写三套。
- Autodesk API 只在合法 Document/Command/Transaction 上下文运行，禁止由 MCP HTTP 后台线程直接调用。
- 写图尽量一次操作一次 Undo；高破坏操作提供预览或可回退，不增加无意义弹窗。
- Batch 必须保留输入 DWG；外参源文件不可被静默修改。
- 不将静态 PASS、已有旧版 Runtime PASS 冒充当前候选 Windows GO。
- 不把源泉/贱人工具箱 VLX/LSP/安装包、版权资产、破解版本或未知许可内容直接提交到公开仓库；参考目录仅放合法资料和自主整理的功能对照。
- 不提交 Autodesk DLL、bin/obj、token、用户工程文件、真实私密日志。
- 不做无需求的 DI/CQRS/EventBus/RBAC；用户是主要个人使用者。

## 每轮交付

同步 README 入口（必要时）、`docs/CURRENT_STATE.md`、`docs/HANDOFF.md`、`tasks/CURRENT_TASK.md`、`WORKLOG.md`，执行静态门禁；需要 Windows 实机的任务必须保留具体样本与证据要求。
