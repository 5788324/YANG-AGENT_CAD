# 当前任务 — 新图形编辑 V2（Issue #12）

更新时间 2026-11-05；唯一开发源 `main` 合并提交 `2f0980d4acd08b087d924b5c1f5a5bb18e8668a8`。旧 PR #4–#10 已归档，PR #11 已合并，Issue #9 已关闭。**不要恢复旧 RC Draft 状态。**

## 本轮具体交付

- 在 `feature/geometry-edit-v2-20261105` 开发 `YGBREAKLINE` 原生折断多段线：点击 LINE、保留端点和属性、一次 Undo，可编辑 2D/3D Polyline，快捷键/Palette/Ribbon 可访问。
- 静态验证、复核错误处理与事务；**Windows AutoCAD 2024 尚未实际测试，不能标 Runtime GO**。
- Windows 验收任务：`tasks/GEOMETRY_EDIT_V2_WINDOWS_SMOKE_20261105.md`。

## 下一增量（不得假称已实现）

1. `YGDETAIL` 支持矩形/画闭合多段线/拾取闭合多段线；精确几何裁切，对复杂图块、尺寸标注、关联填充提供显式的安全模式。
2. `YGOUTSIDE` 删除边界外，跨界安全裁剪、预览、一次 Undo；同一个 polygon boundary service，保护外参源文件。

第三方插件仅作功能参考，所有代码独立实现。Civil 3D live/Revit Native 仍 NOT_TESTED。
