# Windows 执行任务：YangCAD 图层工具 + QG 风格提取大样

日期：2026-11-03；角色：Windows/AutoCAD 2024 执行器。未经 ChatGPT 审查，禁止 FINAL GO。

## A. Build/Deploy/Loaded

1. 从最新 GitHub 开发分支 fetch，记录完整 Commit SHA。
2. Windows + AutoCAD 2024 clean build，记录原始 build command、binlog、错误和警告。
3. 比对构建、部署、加载 DLL SHA-256 + PID + path + document id；所有结果绑定同一 commit。
4. MCP stdio tools=74，AutoCAD live=37；Civil 3D 若未安装写 NOT_TESTED（目标67）。

## B. 图层操作

在测试 DWG 中创建：本地 SOURCE 与 TARGET 层；ModelSpace 2 实体，PaperSpace 1 实体，块定义内 2 实体，块属性 1 实体。SOURCE 初始锁定也测一次。预期：合并后全部改到 TARGET；对象 Handles 和内容保持；ByLayer 表现允许随目标层变化；原 SOURCE 可保留。执行真实一次 `_.U` 或 Ctrl+Z，所有 LayerId 和当前层状态恢复。

分别在普通层与 Xref 依赖层测试 ON/OFF、FREEZE/THAW、LOCK/UNLOCK。通过嵌套点选确认“内部图层 / 块或参照插入层”的不同结果；原 xref 源 DWG SHA 保持。先隐藏/冻结后再用图层窗口恢复。当前 CLAYER 关闭/冻结必须拒绝且不改变 DWG。

## C. 提取大样

创建矩形裁切框内/外/跨界样本：LINE、ARC、CIRCLE、LWPOLYLINE（bulge>0 和 bulge<0）、TEXT、MTEXT、Dimension、Leader/MLeader、普通块、动态块、属性块、HATCH、关联 HATCH、Xref。

1. 1 倍提取：准确剪裁跨界直线、弧线、多段线；原 Handle/实体/数量不变化；新图元仅在目标范围，Except explicitly skipped.
2. 2 倍提取：对简单标注读取 `DIMLFAC`、`Measurement`、`DimensionText`；标注读数应与原图一致，文字位置合理。
3. 默认框线存在；带属性块/关联 HATCH/动态块/跨界复杂对象有明确 skipped，不出现半成品或丢内容。
4. 一次 `_.U` / Ctrl+Z，复制的大样全部消失、原图完全恢复。
5. MCP `cad.detail.extract` dry_run 不增加实体数，real 可以生成，绑定其他 Document/PID 必须拒绝。

## D. 证据

保留测试 DWG、前后属性 JSON、build/deploy loaded SHA、MCP raw request/response、runtime.log、有效截图、Undo 前后对象数；同时记录“哪些对象类型无样本”，不可写 ALL PASS。

最终只允许：`EVIDENCE COMPLETE — SUBMIT TO CHATGPT` / `NO-GO` / `INCOMPLETE`。
