# YangCAD

面向个人工程图纸的 AutoCAD / Civil 3D 工具箱，提供 Ribbon、停靠工具箱、原生命令和本地 MCP。

[整合候选 PR #20](https://github.com/5788324/YANG-AGENT_CAD/pull/20) 以 AutoCAD 2024 为首轮验收宿主。[功能目录](docs/FEATURE_CATALOG.md) · [项目状态](docs/CURRENT_STATE.md) · [Windows 实机入口](tasks/CURRENT_TASK.md)

| 常用任务 | 入口 |
|---|---|
| 工具箱、控制中心、快捷键 | YANGCAD_TOOLBOX、YANGCAD、YGKEYS |
| 大样、折断、边界修剪 | YGDETAIL、YGDETAILPOLY、YGBREAKLINE、YGOUTSIDE |
| 图层、比例、双线 | YGLAYER、YGSCALE、YGDL、YGDLW |
| 填充、标注、板符号 | YGHCLIP、YGLEADERFIX、YGSLOPE、SLB、SLBFIX |
| 图纸交换、引用内嵌、MCP | YGREVIT、YGEMBED、YGEMBEDBATCH、YANGCAD_MCP |

Windows 构建使用 tools/Build.ps1，开发验收用 NETLOAD 加载候选 DLL。[代码结构](docs/PRODUCT_STRUCTURE.md) · [接手说明](docs/HANDOFF.md) · [历史 RC1 证据](docs/RC_AUTOCAD2024_0.10.0.md)

```bash
python tools/verify_source_manifest.py --complete
python tools/run_static_checks.py
```

本项目是个人工程工具，与 Autodesk、源泉设计和贱人工具箱无官方关联。
