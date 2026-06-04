# CLAUDE.md - YANG Agent CAD

本文件给 Claude Code、Claude、Copilot、Antigravity 或其他 AI 继续开发本项目时使用。当前项目定位是个人工作用的小型 CAD 工具箱，不做大型商业级 C# 插件工程。

如本文件与代码现状冲突，以代码和 `AGENTS.md` 为准，并同步更新本文。

## 当前路线

- 主线：Python CLI + MCP stdio + LISP 插件箱 + accoreconsole。
- 近期目标：先让用户能安全检查图纸、批量生成报告、沉淀常用插件。
- 暂缓：完整 WPF UI、多版本 C# 插件工程、Ribbon 大型工具箱、官方 MCP SDK 重构。
- 后续如需要 AutoCAD 内嵌插件，C# 只做按钮和面板外壳，核心逻辑继续复用 Python/LISP 能力。

## 必读顺序

1. `AGENTS.md`
2. `handoff/CURRENT_HANDOFF.md`
3. `docs/PROJECT_STATE.md`
4. 最新 `logs/worklog/YYYY-MM-DD.md`
5. 本文件

## 安全规则

- 查询和报告类任务优先走 MCP 或插件箱。
- 默认 dry-run，不修改 DWG。
- 批量修改、删除、覆盖、保存、清理、批量导出必须先 dry-run、备份并让用户确认。
- AI 生成的 LISP 首次只能作为临时脚本运行，不能直接入插件箱。
- 插件入箱必须有 manifest、参数 schema、风险等级、适用轨道和用户确认。
- 禁止提交 DWG、PDF、客户资料和 `.agent` 运行产物。
- 禁止把 shell 暴露为泛用 MCP 能力。

## 当前可用入口

小白优先使用：

```cmd
scripts\personal-health-check.cmd
```

该命令默认扫描 `sample`，只做 dry-run，并生成：

```text
.agent\reports\PERSONAL_HEALTH_PLAN.md
```

真实只读报告执行必须明确加 `--execute`，且优先对测试副本目录运行，不直接处理客户原图。

当前一键体检包含 5 个只读报告插件：

- 图层统计
- 块统计
- 文字/标注统计
- 外参和图片引用检查
- 图框标题栏候选检查

## 插件开发规则

- 插件位于 `toolbox/plugins/插件目录`。
- 每个插件必须包含 `plugin.json` 和入口 LISP。
- 只读插件风险等级使用 `read`。
- accoreconsole 插件必须 `accore_safe: true`，不得使用交互函数或对话框。
- 新插件必须补测试，至少验证 manifest 能被插件箱识别。

## MCP 规则

- 当前 MCP 是无依赖 stdio 兼容层，不是官方 MCP SDK server。
- 入口：`python -m yang_cad_agent.mcp_stdio` 或 `yang-cad-mcp-stdio`。
- 调用前可用 `{"action":"server_info"}` 查看工具和安全边界。
- MCP 不暴露真实批量执行；真实执行继续走 CLI，并按安全确认流程处理。

## 每轮交付要求

- 跑必要测试或 `scripts\doctor.cmd`。
- 更新 `docs/PROJECT_STATE.md`。
- 更新当天 `logs/worklog/YYYY-MM-DD.md`。
- 更新 `handoff/CURRENT_HANDOFF.md`。
- 提交并推送。
