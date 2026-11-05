# 0.10.0 RC 集成 PR 合并计划（2026-11-05）

## 合并前事实

`main` → #4（引用内嵌/比例）→ #5（引线/简化 UI）→ #6（图层/矩形 QG）→ #7（快捷键/Ribbon）→ #8（0.10.0 产品与文档；b6ff137 编译修复）→ #10（Release 契约修复；d5200c5）→ **RC1 docs-only 集成 PR #11**。

前述 #5–#10 均以父功能分支为 base，并非单独面向 `main` 的独立 PR。错误的合并顺序可能导致差异重复、PR 自动关闭或误判测试 SHA。

## 本轮采用单集成 PR，避免重复 merge

1. 基于 **`d5200c57a75b1fd228713dc67b606126a5c04924`** 新建 `release/autocad2024-0.10.0-rc1`，只写 RC 范围/事实文档、GitHub CI 分支识别、SOURCE HASH 清单。
2. 已建立 [Draft PR #11](https://github.com/5788324/YANG-AGENT_CAD/pull/11) → `main`，已核对 107 个变更文件、14 个提交、无冲突以及包含 #4、#5、#6、#7、#8、#10 全链。
3. 验证 RC PR 的 GitHub Actions、无 DLL/EXE/用户 DWG、实际运行代码与受测 `d5200c5` 比较**无差异**；不得把文档变化当作新业务 Runtime PASS。
4. 这一步**不自动合并 `main`**。如果决定合并，先记录 `main` 旧 SHA，采用保留历史的普通 merge（不强制改写 main），再核验 main 的实际内容与集成树一致。
5. 确认集成 PR merge 后，再逐个对 #4–#10 作 `superseded by integrated PR` 注释并关闭/归档；不要先合并父分支导致历史重复。
6. **不要自动创建跨宿主 stable tag**。有需要时单独创建作用域清晰的 AutoCAD 2024 RC 标签，并确保对应已审查源码 SHA、实际 Quick ZIP 和 DLL SHA 明确配对。

## 后续开发如何开始

以集成后的主线为起点；Civil 3D 与 Revit 专项另开 issue/feature，明确 NOT_TESTED；未实现的闭合边界、外部删除、Z 折断线继续作为 Backlog 需求，开发前由用户确认优先级。
