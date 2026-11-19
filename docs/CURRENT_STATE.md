# YangCAD 当前状态（2026-11-19）

本轮接手以 `main@ef23385a6bf832388c70cd9f0862ed7e0a49f58e` 为基线。采用局部重构：保留原生命令、绘图服务、事务和 MCP 契约，先整理日常使用的工具箱。

| 功能 | 现有依据与范围 |
|---|---|
| 图层、矩形大样、比例转换、引线、PGP | RC1 在 AutoCAD 2024 有指定样例证据。矩形大样跳过部分复杂对象，标准比例转换有 INSERT 跳过记录。 |
| 双线、固定中轴改宽、标注改字筛选、坡度、文字框 | [PR #16](https://github.com/5788324/YANG-AGENT_CAD/pull/16#issuecomment-6848545441) 和 [PR #17](https://github.com/5788324/YANG-AGENT_CAD/pull/17#issuecomment-6848546242) 分别有 Core Console 证据。合并后的 DLL 和 GUI 待验。 |
| 图片与外参内嵌 | RC1 批处理为 2 通过、1 跳过、1 失败。Worker 处理磁盘 DWG。本轮统一工具箱两个“处理当前图纸”入口的保存要求。 |
| CAD → Revit、Civil 3D | CAD 端交换文件读回有记录，Revit 原生重建和 Civil 3D 本轮实机待验。 |
| 折断线、多边形大样、边界外裁切 | 分别在 PR #13–#15，未进入基线。PR #18 是审查快照。 |

本轮 STATIC 检查和 WINDOWS 实机分别记录，后者待验。复测顺序见 [当前任务](../tasks/CURRENT_TASK.md)。

## 2026-11-13 — YangCAD 双线 + 标注工具整合

按用户确认，整理已单独通过 AutoCAD 2024 Core Console G5 的 PR #16 和 PR #17：`YGDL`、`YGDLW`、`YGDIMTXT`、`YGSLOPE`、`YGTBOX`。保留双线/坡度/文字框全部个人设置、Toolbox 按钮、快捷键。**GUI 及联合程序集 Windows Build 未验收**，不代表已安装发布。PR #13/#14/#15 与综合审查 PR #18 不包含在本次主线合并中。完整说明 `docs/INTEGRATION_PR16_PR17_20261113.md`。

---

## RC1 历史验收（2026-11-05）

**当前发布管理标记：`0.10.0-rc.1`（AutoCAD 2024 Quick 范围） / `G5_AUTOCAD2024_SCOPE_PASS`。**

源码中的编译与 Python/Bundled 版本仍为 **`0.10.0` / `0.10.0-dev`**；此次 RC 分支未重新编译、更换 DLL 或改变业务行为。产品可运行不等于跨宿主正式 Release。

## Git 与证据

- RC1 合并前的历史基线：`396a79408bfb38d374871fb030b54012748be8b6`。
- R00–R10 AutoCAD 2024 实机基线：`b6ff1370b0ed48e5e50fec43d2a5a2e14ca5ec54`。
- 修复发布契约且 Windows PS5.1 定点验收：`d5200c57a75b1fd228713dc67b606126a5c04924`。
- RC1 集成由 [PR #11](https://github.com/5788324/YANG-AGENT_CAD/pull/11) 合入主线，合并提交 `2f0980d4acd08b087d924b5c1f5a5bb18e8668a8`。
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
