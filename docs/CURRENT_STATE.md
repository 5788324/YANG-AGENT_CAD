## 2026-11-05 — YGOUTSIDE 独立候选（删除边界外）

- 基于已合并 RC1 的 `main@2f0980d4acd08b087d924b5c1f5a5bb18e8668a8` 建立独立分支 `feature/outside-boundary-v1-20261105`；绝不等同合并 PR #13/#14。
- 新增 `YGOUTSIDE` Rectangle/Select/Draw、只读预览、一次确认、预览指纹复核、单事务删除及跨界 LINE/ARC/CIRCLE/Polyline 修剪；共享 PR #14 的同一份 `PolygonBoundary2d` Blob。
- 状态 **STATIC_ONLY / AutoCAD 2024 WINDOWS_NOT_TESTED**。UI 可见、构建、Loaded DLL、撤销均未经实机证明；Civil/Revit 仍 NOT_TESTED，保留原 RC1 状态。
- PR #13（折断线）、PR #14（多边形大样）和本候选分支分开验收。尚未进行这 3 项的新功能集成发布。

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
