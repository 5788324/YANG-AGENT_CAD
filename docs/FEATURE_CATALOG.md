# YangCAD 功能目录（供用户和 AI 查阅）

更新时间：2026-11-03 · 版本：0.10.0-dev · **新增功能静态实现，未统一 Windows 验收**。

| 功能 | UI 入口 | CAD 命令 | MCP 产品入口 | 当前状态 |
|---|---|---|---|---|
| 工具箱 | Ribbon → 工具箱 | `YANGCAD_TOOLBOX` | — | 历史 UI 可用；本轮新增快捷按钮待验收 |
| 图层合并/开关/冻结/锁定 | 工具箱快捷入口 / Ribbon | `YGLAYER`, `YGLMERGE`, `YGLONOFF`, `YGLFREEZE`, `YGLLOCK` | `cad.layer.merge/visibility/freeze/lock` | STATIC ONLY |
| QG 风格矩形大样提取 | 工具箱 / Ribbon | `YGDETAIL` | `cad.detail.extract` | STATIC ONLY；闭合边界增强尚未开发 |
| 比例转换（标准1:1/缩尺） | 工具箱 / Ribbon | `YGSCALE` | `cad.scale.convert` | PARTIAL EVIDENCE / RETEST |
| 炸开引线重建、关联文字 | 工具箱 / Revit 预处理详情 | `YGLEADERFIX` | `cad.leader.rebuild` | STATIC ONLY |
| Revit 图纸预处理 | 工具箱 → Revit | GUI 快捷一键 | `project.revit.prepare` | 核心历史通过，当前增量待回归 |
| 图片/外参内嵌 | 工具箱 → 引用内嵌 | GUI 快捷一键/选文件夹 | `project.image.embed` | STATIC ONLY / OLE/Xref 实机待验 |
| 快捷键管理 | 控制中心 | `YGKEYS` | — | STATIC ONLY / PGP REINIT 待验 |
| MCP 管理 | Ribbon → MCP | `YANGCAD_MCP` | `cad.status` 等 | 历史核心已验证，新增接口待回归 |

下列已提出但**尚未算已实现/通过**：任意闭合多段线切割、复杂对象可选裁切、删除边界外、普通直线转 Z 折断线。见 `docs/FEATURE_LIBRARY_YQARCH_JRCAD_20261103.md`。

当前 MCP 公开合同源码预期：**stdio 74、AutoCAD 37、Civil 3D 67**。单工具完整清单见 `docs/MCP_COVERAGE_MATRIX.md`；不要按 GUI 按钮数量推导 MCP 数量。
