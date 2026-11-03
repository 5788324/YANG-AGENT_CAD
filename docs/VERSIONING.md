# 版本与发布约定

当前版本：`0.10.0-dev`（2026-11-03 项目结构与产品 UI 整理候选）。

## 开发标签

- `0.x.y-dev`：未完成本版 Windows 编译/加载/图纸验收，**不等于 Release**。
- `0.x.y-rc.N`：静态+真机验证基本齐全，可供最终独立复核。
- `0.x.y`：当前版本功能按目标宿主/项目清单通过验收，明确已知限制。

## 同步文件

`Directory.Build.props` 的 Version / InformationalVersion / FileVersion / AssemblyVersion；`mcp/pyproject.toml`；`mcp/yangcad_mcp/__init__.py`；`bundle/PackageContents.template.xml`；打包脚本与 `tools/verify_release_source.py` 检查版本。关于页必须从编译程序集读取版本，不能写死作为事实。

## 本版的注意事项

- 0.10.0-dev 是结构整理和开发阶段提升，不代表功能数量完成，也不等于 Windows Runtime GO。
- 与旧 `0.9.0` 安装版本不能混用 DLL；升级前关闭目标 CAD 宿主，依发行机制完成部署和 Loaded SHA 检查。
- 版本晋升前执行 `docs/RELEASE_CHECKLIST.md`、`tasks/CURRENT_TASK.md` 的实机验证。
