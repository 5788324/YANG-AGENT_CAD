# 当前交接日志

更新时间：2026-05-23

## 当前任务

继续安全可回滚 MVP，建立完整批量任务入口和插件箱 manifest 校验。

## 已完成

- 克隆 GitHub 仓库到本地。
- 安装辅助 Codex skills：
  - cli-creator
  - security-best-practices
  - security-threat-model
  - gh-fix-ci
  - pdf
- 创建项目入口文档和工作规范。
- 创建工作计划、项目状态、运行契约、错误码、回滚、插件箱和 Antigravity 适配文档。
- 创建最小 Python CLI 骨架：
  - `yang-cad-agent doctor`
  - `yang-cad-agent new-task`
  - 错误码常量
  - task ledger 基础写入
- 使用 Codex runtime Python 验证 CLI 和语法检查。
- 实现备份/回滚基础模块：
  - `yang-cad-agent backup`
  - `yang-cad-agent rollback --dry-run`
- 实现 LISP 静态校验器：
  - 允许 `(ssget "X" ...)`
  - 禁止 accoreconsole 中的 `vla-*` / `vlax-*` / 交互输入
- 实现 accoreconsole runner scaffold：
  - `yang-cad-agent accore-run --dry-run`
  - 自动校验 LISP
  - 自动扫描 DWG
  - 自动写 task ledger
- 单元测试改为 Python 标准库 `unittest`，避免依赖 pytest。
- 实现完整批量任务入口：
  - `yang-cad-agent batch-task`
  - 默认 dry-run
  - 只有加 `--execute` 才会执行
  - 执行前自动备份
  - 无匹配 DWG 时拒绝继续
- 实现插件箱 manifest 工具：
  - `yang-cad-agent toolbox-list`
  - `yang-cad-agent toolbox-validate`

## 下一步

1. 增加开发环境 bootstrap 脚本，让用户机器上的普通命令也能运行。
2. 实现 MCP Server 最小工具。
3. 准备 AutoCAD 本地实测脚本和测试样例。
4. 实现当前图 LISP 投喂原型。
5. 增加首批实用插件。

## 注意事项

- 用户是代码小白和 CAD 新手，所有步骤要尽量由 AI 主动完成。
- 任何批量修改 DWG 的功能必须等备份/回滚机制完成后再做。
- 本地 AutoCAD 能力需要在用户机器上实测。
- 当前 PowerShell 中 `python` 和 `py` 不在 PATH。可临时使用 Codex runtime Python：
  `C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe`
- `yang-cad-agent doctor` 已发现 AutoCAD 2027 accoreconsole。
- dry-run 冒烟测试生成过 `.agent/tasks/20260523-233502-d45edb72.json`，该目录被 gitignore 忽略。
- `batch-task` dry-run 冒烟测试生成过 `.agent/tasks/20260523-234016-c9285ed6.json`，该目录被 gitignore 忽略。
- 新安装的 skills 需要重启 Codex 后才会完整生效。
