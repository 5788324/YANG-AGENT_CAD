# YangCAD 功能目录

以下为整合候选中的入口，验证状态见 [项目状态](CURRENT_STATE.md)。

| 功能 | 主要入口 | 验收重点 |
|---|---|---|
| Ribbon、工具箱、主题、快捷键 | YANGCAD_TOOLBOX、YANGCAD、YGKEYS | GUI、启动页签、PGP |
| 图层、比例、双线 | YGLAYER、YGSCALE、YGDL / YGDLW | 图元属性与撤销 |
| 矩形与多边形大样 | YGDETAIL、YGDETAILPOLY | 保留原图，直边多段线 |
| 折断线、边界外编辑 | YGBREAKLINE、YGOUTSIDE | 原生多段线、预览、撤销 |
| 文字与标注 | YGTL、YGINC、YGLEADERFIX、YGDIMTXT、YGSLOPE、YGTBOX | 对齐、数值、引线、外框 |
| 填充 | YGHCLIP、YGHCLIPSPLIT、YGHSPLIT、YGHUNION | 裁切、分离、纹理、回滚 |
| 梁柱板 | BMK、CMK、SLB / SLBFIX / SLBLAYER | 工程标记与箭头方向 |
| 图纸与引用 | YGREVIT、YGEMBED / YGEMBEDBATCH | 保留原图、输出副本、重开 |
| 自动化与批处理 | YANGCAD_MCP、Python MCP、BatchWorker | 读写、权限、批处理源图哈希 |
| Civil 3D | Civil 宿主与其 MCP 工具 | 仅有真实 Civil 3D 2024 时执行 |

[快捷命令目录](../src/YangCAD.AutoCAD/Toolbox/CadToolCatalog.cs) · [MCP 覆盖表](MCP_COVERAGE_MATRIX.md)。新几何命令先提供 CAD 入口。
