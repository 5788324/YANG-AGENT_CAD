# YangCAD 0.10.0 RC 分支关系（2026-11-05）

```text
main @ 396a79408bfb38d374871fb030b54012748be8b6 [旧回退版本]
 └─ PR #4 引用内嵌 + 比例转换
    └─ PR #5 引线重建 + 一键预处理
       └─ PR #6 图层工具 + 矩形大样
          └─ PR #7 快捷键 + Ribbon
             └─ PR #8 产品正规化 + Gemini 编译修复 b6ff137
                └─ PR #10 Release Contract 修复 d5200c5 [已验收]
                   └─ release/autocad2024-0.10.0-rc1 [仅文档/CI，Draft 集成 PR → main]
```

#4/#5/#6/#7/#8/#10 目前仍保持 Draft，并未单独合并 `main`。**RC 集成 PR 是单独的全链审查入口，不代表这些老 PR 应当一个个乱序合并。** 完整操作步骤见 `docs/RC_MERGE_PLAN.md`。

- 源泉/贱人工具箱功能仅是独立研发参考，未把第三方未授权二进制放进仓库。
- 此前的原生 AutoCAD 2024 证据绑定 `b6ff137`；发布检查绑定 `d5200c5`。RC docs-only SHA 的出现不改变受测 DLL。
- 不能把 AutoCAD G5 PASS 扩大为 Civil 3D / Revit native / 所有年份 PASS。
