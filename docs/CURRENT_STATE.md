> **审查快照提示（2026-11-09）**：本 `review/full-source-20261109` 工作树同时包含 [PR #13](https://github.com/5788324/YANG-AGENT_CAD/pull/13)、[#14](https://github.com/5788324/YANG-AGENT_CAD/pull/14)、[#15](https://github.com/5788324/YANG-AGENT_CAD/pull/15)、[#16](https://github.com/5788324/YANG-AGENT_CAD/pull/16)、[#17](https://github.com/5788324/YANG-AGENT_CAD/pull/17) 的源码。它们全部是尚未合并的 Draft。请先看 `docs/AI_REVIEW_SNAPSHOT_20261109.md`，不要将此快照当成可发行 DLL，本文下方若有“当前任务仅 PR #17”等内容属于原分支交接历史。

## 2026-11-09 — 新增标注文字工具三功能（独立源码候选）

- 独立分支 feature/annotation-utilities-v1-20261109 基于 main@2f0980d4acd08b087d924b5c1f5a5bb18e8668a8，与 Gemini 测试中的 Draft PR #16 完全独立。
- YGDIMTXT：只选择当前模型/布局空间 Dimension.DimensionText 存在覆盖内容的标注（非默认空白/仅 <>），不修改标注测量或文字。
- YGSLOPE：Section=|ΔY|/|ΔX| 或 Plan3D=|ΔZ|/XY 水平距离，两点/LINE，Percent、Degrees、Ratio 三种文字，独立短坡度指示线+低端箭头+原生 MText。
- YGTBOX：针对 TEXT/MTEXT 文字显示范围，生成原生矩形 Polyline 或 Circle，按 mm 设置偏移留白；文字不变。源自用户参考贱人工具箱/源泉，附件 column_mark / beam_mark (实际 CMK+CMKD 柱标注) / 双线 LISP 不直接包含上述功能，不改现有命令。
- 新增 tests/tools/test_annotation_utilities_policy.py（实际路径 tools/test_annotation_utilities_policy.py）、docs/ANNOTATION_UTILITIES_V1_20261109.md、tasks/ANNOTATION_UTILITIES_WINDOWS_SMOKE_20261109.md；存储用户上次参数并提供中文 Toolbox/PGP 入口。
- STATUS=STATIC_ONLY / Windows AutoCAD 2024 Build, NETLOAD, RealDWG, Native _.U 均 NOT_TESTED。不得合并/发布，待 G1 CI、Gemini G2–G4 和 ChatGPT G5 独立审核。

---

# YangCAD 当前事实（2026-11-05）

**当前发布管理标记：`0.10.0-rc.1`（AutoCAD 2024 Quick 范围） / `G5_AUTOCAD2024_SCOPE_PASS`。**

源码中的编译与 Python/Bundled 版本仍为 **`0.10.0` / `0.10.0-dev`**；此次 RC 分支未重新编译、更换 DLL 或改变业务行为。产品可运行不等于跨宿主正式 Release。

## Git 与证据

- `main`：旧稳定回退 SHA `396a79408bfb38d374871fb030b54012748be8b6`，**尚不含 0.10 RC**。
- R00–R10 AutoCAD 2024 实机基线：`b6ff1370b0ed48e5e50fec43d2a5a2e14ca5ec54`。
- 修复发布契约且 Windows PS5.1 定点验收：`d5200c57a75b1fd228713dc67b606126a5c04924`。
- 唯一 RC 文档集成分支：`release/autocad2024-0.10.0-rc1`，基于 d520；[Draft PR #11](https://github.com/5788324/YANG-AGENT_CAD/pull/11) → `main`，静态 CI 已通过、无冲突、**尚未合并**。
- G5 ChatGPT 独立审计：`https://github.com/5788324/YANG-AGENT_CAD/issues/9#issuecomment-6646760886`。
- 已验收 Quick ZIP SHA256 `ACA88A9440968411A2D7A014800AC70FE8E60C314ECBE936BBBC4037C5C79692`；AutoCAD DLL SHA256 `880329CF7E5C464078686FE56B0BB7070B4C2F7B7F482A91771D9C8`。

## 分层状态

| 范围 | 结论 | 限制 |
|---|---|---|
| G0/G1 源码身份、GitHub CI、STATIC | PASS | RC 文档增量仍须 CI 校验 |
| G2/G3 AutoCAD 2024 Windows Build/Loaded | PASS（b6ff137） | 新业务代码修改必须重新追溯 |
| G4 R00–R10 AutoCAD 2024 指定功能 | CONDITIONAL / SCOPE PASS | R06 是对象级 Undo；R07 批量为 PARTIAL |
| G4 Release 74/37/67 正反向 | PASS（d520 + Windows PS5.1 日志） | Civil 67 是静态合同，不是 live |
| G5 独立证据审查 | **AUTOCAD2024_SCOPE_PASS** | 不是所有年份/宿主稳定版 |
| Civil 3D live 67 | **NOT_TESTED** | Object Enabler 不算宿主测试 |
| Revit Native E2E | **NOT_TESTED** | MAP/ReaderVerifier 11/11 不等于真正生成 Revit 元素 |
| 2024 以外 AutoCAD 宿主 | **NOT_TESTED_CURRENT_RC** | 历史旧版记录不可平移 |

## 本版已验证功能与明确边界

图层合并/显隐/冻结/锁定、**矩形**大样、比例转换、引线恢复与文字关联、PGP 快捷键、Ribbon/Palette、图片 OLE 和 Xref Bind、MCP、CAD→Revit MAP 均有 **AutoCAD 2024 特定样例** 的真实测试材料。R07 文件夹批量结果为 2 PASS、1 SKIP、1 FAIL，不是全成功；R06 标准模式跳过受检 INSERT；Undo 不是完整 DWG 字节级证明。详细证据见 `docs/RC_AUTOCAD2024_0.10.0.md`。

未实现：闭合多段线任意边界提取、复杂跨界对象可选裁切、删除边界外、直线 Z 折断线；请不要在 GUI/MCP 说明中冒称已完成。

## 当前动作

已建立 **docs-only RC Draft PR #11** → `main`，静态 CI PASS，RC 与 d520 运行时代码无差异；`main` 不自动合并，保留旧版回退；后续按 `docs/RC_MERGE_PLAN.md` 处理堆叠 PR。**STATIC** 与 **WINDOWS** 状态必须分列记录。
