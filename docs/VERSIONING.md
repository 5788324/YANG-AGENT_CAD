# YangCAD 版本与发布身份（2026-11-05）

**当前 RC 管理标签：`0.10.0-rc.1`，仅 AutoCAD 2024 Quick 指定范围。**

**二进制实际产品版本：`0.10.0` / `0.10.0-dev`。** Release ZIP 为已验收的 419,586 字节工件；本 RC 分支只做文档和工作流整理，未重新编译并宣称 DLL 是 0.10.0-rc.1。保留 DLL SHA 是复用 R00–R10 现场运行证据的前提。

- `0.x.y-dev`：开发来源标记，不表示跨宿主运行通过。
- `0.x.y-rc.N`：**仓库审查管理里程碑**，必须明确特定宿主、发行渠道和工件 SHA；如需真正把 RC 写入程序集，请重新 Build/Deploy/Loaded 并标明新 SHA。
- `0.x.y` stable：按定义的完整兼容矩阵通过 G5 后才可标注；不能用部分 AutoCAD 2024 证据代替全部年份/Civil/Revit。

当前已验收 AutoCAD 2024 ZIP：`ACA88A9440968411A2D7A014800AC70FE8E60C314ECBE936BBBC4037C5C79692`，DLL：`880329CF7E5C464078686FE56B0BB7070B4C2F7B7F482A91771D9C8`。

将来变更版本字段时需同步 C# `Directory.Build.props`、Python MCP `mcp/pyproject.toml`/`__init__.py`、Bundle PackageContents、Setup/Package-Release，以及所有源代码/发行包校验器；改变输出字节必须重新绑定运行测试范围。

0.10.0 RC1 的单一说明见 `docs/RC_AUTOCAD2024_0.10.0.md`。不需要把所有宿主 GO 才可提供**仅 AutoCAD 2024 个人使用的未签名 Quick 候选**；但它不是跨宿主稳定公开发布。
