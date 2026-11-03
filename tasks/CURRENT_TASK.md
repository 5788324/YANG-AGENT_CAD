# 当前任务：0.10.0-dev 项目正规化与 UI 整理

日期：2026-11-03

## 本轮已实现（静态候选）

- 统一 README、PROJECT、AGENTS、CLAUDE、CURRENT_STATE、HANDOFF、FEATURE_CATALOG 和 Git/版本/UI 文档。
- 新建 `references/`，合法整理源泉/贱人工具箱参考来源、权限与功能对照。
- 控制中心“关于”升级，显示 runtime Version/DLL/宿主/用户目录/仓库链接。
- Ribbon 分组；Palette 常用命令快捷入口，两个长任务型工具继续保留。
- 应用、MCP、bundle、发布脚本版本对齐 `0.10.0-dev`，不是正式 Release。

## 下一步：Windows / AutoCAD 2024 定点验收

1. 从本功能分支 SHA 构建 `tools/Build.ps1 -Year 2024`，保存构建原始日志与 DLL SHA。
2. 实际 AutoCAD 2024 加载：检查 `Loaded DLL SHA` 与 Build/Deploy 一致；控制中心显示 `0.10.0-dev` 和真实 DLL 路径。
3. Ribbon 默认保留 CAD 原生 Home/常用；常用工具/连接设置分组正确；Palette dock/float、快捷入口真实可调用。
4. 控制中心打开 GitHub/日志目录；复制诊断信息不含 Token。
5. 检查 MCP `74/37/67` 的可发现性（Civil 仅装有 Civil 3D 才测）。
6. 其余开发候选按对应 `tasks/*WINDOWS_SMOKE*` 测：图片内嵌、比例转换、引线修复、图层/QG、快捷键；真实 Ctrl+Z、原始 DWG readback 不能省略。

**验收边界：**没有实际 Autodesk 运行证据，一律 `NOT_TESTED`；若实机修源码，必须提交新 commit/SHA，并重新测受影响项目。

## 功能规划但尚未实现

任意闭合多段线大样裁切、复杂块/填充裁切选项、删除边界外对象、直线变 Z 折断线。不得因为出现在参考矩阵就算功能实现。
