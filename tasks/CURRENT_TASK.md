# 当前开发任务 — YGOUTSIDE V1 删除边界外（Issue #12）
日期：2026-11-05；唯一源码分支 `feature/outside-boundary-v1-20261105`；来源 `main@2f0980d4acd08b087d924b5c1f5a5bb18e8668a8`（已验收 AutoCAD 2024 RC1）。
## 实际新增范围
- 命令 `YGOUTSIDE` 的 Rectangle / Select / Draw 三种闭合直边边界模式，WCS XY 平面投影；
- `OutsideBoundaryService`：只读预览、默认取消/一次确认、预览结果指纹校验，完全外侧删除、跨界 LINE/ARC/CIRCLE/Polyline 内部段生成；
- `PolygonBoundary2d` 从 PR #14 复制精确 Git Blob，保证后续合并复用；MCP 74/37/67 不改；
- Palette 中文快捷按钮、YGKEYS/PGP 命令名；安全跳过锁层、外参、关联对象、未知跨界实体。
## 门禁
- [ ] Github CI / `python tools/run_static_checks.py` 和 manifest G1。
- [ ] Windows AutoCAD 2024 本 SHA 新 Build/Deploy/Loaded DLL。
- [ ] 真实 DWG 预览、Yes/No、取消、凹边多交点、原生 `_.U` / 关闭重开。
- [ ] ChatGPT 独立证据审核通过后才考虑合并。
## 禁止
- 不将 PR #13/#14 的新功能记作本分支运行测试结果，不再重复历史 RC1 测试。
- Civil live / Revit Native E2E / AutoCAD 其他年份均 NOT_TESTED。
- 测试任务：`tasks/OUTSIDE_WINDOWS_SMOKE_20261105.md`；说明：`docs/OUTSIDE_V1_20261105.md`。
