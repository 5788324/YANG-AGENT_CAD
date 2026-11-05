# 文档事实优先级

更新时间：2026-11-03

1. **GitHub 当前分支/PR 的源码及对应 SHA**（是否实现的最终依据）
2. `docs/CURRENT_STATE.md`（当前事实、尚未验收事项）
3. `tasks/CURRENT_TASK.md`（唯一当前任务）
4. `docs/HANDOFF.md`（新 AI 接手说明）
5. `docs/FEATURE_CATALOG.md`（用户功能入口、源码状态）
6. `docs/PRODUCT_STRUCTURE.md` / `docs/PRODUCT_UI.md`（产品结构与设计）
7. `docs/AI_FEATURE_DEVELOPMENT_STANDARD.md` / `docs/AGENT_RULES.md`（工程纪律）
8. `docs/COMPATIBILITY.md` / `docs/RELEASE_CHECKLIST.md`（历史兼容性与最新待验证门禁）
9. 具体功能规范（Exchange、Image Embed、Scale 等）
10. `WORKLOG.md`（历史记录，不覆盖当前事实）

**历史文档不可回写当前结论。** 老的 65/28/58、68/31/61 以及旧的“main-only”规则属于当时阶段，当前开发树预期合同 74/37/67，分支采取 feature/Draft PR。

**来源合法性**：`references/` 的第三方功能与版权信息仅作开发需求参考，不能被当作本仓库原创代码或官方许可。
