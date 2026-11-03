# 开发分支与依赖关系（2026-11-03）

当前 `main` 不包含下面全部增量。不要简单地说“main 没有功能，所以功能不存在”；也不要把开发分支误当稳定版。

```text
main @ 396a79408bfb38d374871fb030b54012748be8b6
  └─ PR #4 feature/reference-embed-v2-20261026
      └─ PR #5 feature/revit-prep-leader-repair-20261027
          └─ PR #6 feature/layer-tools-detail-qg-20261103
              └─ PR #7 feature/shortcut-manager-native-ribbon-20261103
                  └─ PR #8 feature/product-foundation-v010-20261103
                      └─ [本轮管理/Gemini 验收资料进入 PR #8 头部]
```

所有完整 SHA 以 GitHub API 在执行时查询为准；这是关系图，不是新的测试证据。

## 当前策略

1. **Gemini 只测试 PR #8 当前 HEAD**，它包含所有父 PR 的功能代码；禁止自行切换旧 ZIP 或主分支。
2. Windows 原始证据通过后由 ChatGPT 审核；失败项优先修复或拆出，不要求从 #4 到 #8 每个 PR 重复全量跑一次。
3. 不能因旧 PR 保持 Draft 就让 `main` 永远落后：审核通过后按 #4→#8 顺序合并，或在保留来源和证据的前提下建立明确的整合 PR。切忌无验证地批量 merge。
4. `main` 保持稳定，发布版本独立打 Tag。含未完成功能的整合分支不能按正式版分发。
