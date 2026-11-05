# GitHub 协作与发布流程

更新时间：2026-11-03 · Repo：`https://github.com/5788324/YANG-AGENT_CAD`

## 分支语义

- `main`：**稳定回退基线**，不是所有已开发功能的全集。
- `feature/*`：按用户真实需求开发；允许创建 Draft PR 供测试、审查、回滚。
- 功能分支若基于另一个未合并 PR（当前历史已发生），必须在 PR 中显式说明父分支/依赖；合并时避免重复提交/覆盖。
- 不用日期、聊天记录、ZIP 名称推断 HEAD。任何任务先取 `git rev-parse HEAD` / GitHub API 的 commit SHA。

## 日常工作

1. 获取最新 HEAD，核对正在使用的分支和未合并 PR。
2. 代码修改后完成静态测试，更新事实文档和版本元数据。
3. 需要 Autodesk 的测试交 Windows 执行者：真实 Build/Deploy/Loaded + DWG 对象回读 + Undo，证据按 SHA 归档。
4. 开 Draft PR，Windows 通过且审计通过后再 Merge/Tag；当前 `0.10.0-dev` 不是 stable。
5. 未经重新验证，不能复制前版的 PASS 结论。

## 大文件

- 源码及文档进 GitHub；含隐私/大尺寸的 DWG、截图、binlog、运行日志进受控云盘证据目录。
- 禁止将第三方原插件、Autodesk DLL、编译缓存、凭据提交 GitHub。`references/README.md` 描述合法资料收集流程。

## 回滚

- 每轮 feature commit 可回退；不要求清空整个仓库。
- 除用户明确要求重写仓库历史外，不对正常 `main` 使用 force push。

## 2026-11-05 0.10.0 RC1 集成例外

本轮父链 PR #4→#5→#6→#7→#8→#10 均经同一最终代码/工件链追踪；从 d5200c5 建立 `release/autocad2024-0.10.0-rc1` 的 **docs-only RC 集成 PR → main**，以防逐级合并时遗漏分支关系。不要从历史主分支 cherry-pick 重拼 DLL。完整规范见 `docs/RC_MERGE_PLAN.md`。

在集成 PR 被审查/批准前不自行 merge，合并后才处理旧 Draft PR 的状态。AutoCAD 2024 Quick scope GO 不等于 Civil 3D/Revit native / 其他年份发布 GO。
