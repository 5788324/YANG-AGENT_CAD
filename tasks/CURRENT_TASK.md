# 当前开发任务 — YGDETAIL 闭合多边形 V1（Issue #12）

日期 2026-11-05；开发分支 `feature/detail-polygon-v1-20261105`；直接从 `main@2f0980d4acd08b087d924b5c1f5a5bb18e8668a8` 派生。

## 实际已编码范围（G1 SOURCE ONLY）

- 原有 Rectangle 大样保持原路径；`YGDETAIL` 新增 Select（拾取 WCS XY 闭合零宽度 LWPOLYLINE）与 Draw（逐点闭合）两种模式。
- 新 `PolygonBoundary2d` 精确点内判断、凸/凹直边多边形自交检验、LINE 与 ARC/CIRCLE 分割；`PolygonDetailExtractService` 管理当前空间的受控复制、裁切、缩放与单事务写入。
- 明确不支持带 bulge 弧线作为**裁切边界**，复杂跨界块/标注/Hatch 采取安全跳过；源图保持不擦除。
- 文档 `docs/DETAIL_POLYGON_V1_20261105.md`，策略 `tools/test_polygon_detail_policy.py`。

## 仍需完成的验收

- [ ] GitHub G1 CI 与 SHA manifest 验证。
- [ ] Windows AutoCAD 2024 真正 Build/Deploy/Loaded 同一 SHA。
- [ ] 真实 DWG 的凸/凹、多交点、弧段、Undo/重开、布局/图层、锁定/外参保护。
- [ ] ChatGPT 独立复核证据后才讨论合并。

## 未实现 / 独立并行

- `YGOUTSIDE`（删除边界外）仍 NOT_IMPLEMENTED。
- `YGBREAKLINE` 在独立 Draft PR #13（不同 HEAD），不能作为本分支实现或测试结果。
- Civil3D live/Revit Native E2E/其它 AutoCAD 年份仍 NOT_TESTED。
- 按 `tasks/DETAIL_POLYGON_WINDOWS_SMOKE_20261105.md` 交 Gemini；不可修改 main 或将 STATIC PASS 写为 Windows PASS。
