# 当前交接日志

## 2026-05-24 管理员脚本秒退处理

- 用户反馈右键“以管理员身份运行” `scripts\fix-acad-cfg.cmd` 后窗口秒退。
- 已更新脚本：打印源路径、目标路径、结果信息，并在成功/失败时 `pause`，避免窗口自动关闭。
- 复查发现用户管理员运行旧脚本实际已复制成功，`scripts\doctor.cmd` 显示安装目录 `acad2027.cfg` 已存在且 notes 为空。
- 已继续运行 accoreconsole 小样本真实测试，任务 `20260524-135319-fae4680f` 成功处理 `.agent\tmp\sample-run\S001-test.dwg`，成功 1、失败 0，并生成备份 `.agent\backups\20260524-135319-fae4680f`。
- 下一步：可以在测试副本上继续扩展更真实的批量插件验证；不要直接对 `sample` 原图目录整批 `--execute`。

## 2026-05-24 accoreconsole 配置修复进展

- 用户要求“再试试”后，重新运行 doctor 和 batch-task 预检。
- accoreconsole 真实执行仍被预检拦截为 `ACCORE_CONFIG_LOCKED`，未启动 accoreconsole，未修改测试 DWG。
- 安装目录缺少：`C:\Program Files\Autodesk\AutoCAD 2027\acad2027.cfg`。
- 用户配置已存在：`C:\Users\YANG\AppData\Local\Autodesk\AutoCAD 2027\R26.0\chs\acad2027.cfg`。
- 直接复制到 Program Files 因权限不足失败。
- 已新增 `scripts\fix-acad-cfg.cmd`，下一步需要用户右键该脚本并选择“以管理员身份运行”，然后再运行 `scripts\doctor.cmd`。
- doctor 已增强：会输出 `user_cfg`、`user_cfg_exists`，并提示可用管理员权限从用户配置复制到安装目录。
- 验证结果：`scripts\test.cmd` 通过 21 个测试，`scripts\doctor.cmd` 能输出可读诊断，`compileall src tests` 通过。
- 最新真实执行入口复测任务 `20260524-134601-55f2f41a` 被预检拦截为 `ACCORE_CONFIG_LOCKED`，未启动 accoreconsole，未修改 DWG。

更新时间：2026-05-24

## 当前任务

补充面向代码小白和 CAD 新手的操作文档、功能说明文档，并把用户提供的 `sample` DWG 目录纳入安全测试说明。

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
- 增加 Windows 包装脚本：
  - `scripts\doctor.cmd`
  - `scripts\test.cmd`
- 增加任务查询 CLI：
  - `yang-cad-agent task-list`
  - `yang-cad-agent task-show`
- 增加 MCP stdio 骨架：
  - `doctor`
  - `toolbox_list`
  - `task_list`
  - `task_show`
- 增加当前图 LISP 投喂原型：
  - `yang-cad-agent current-lisp --script ...`
  - 默认 dry-run
  - 只有加 `--execute` 才尝试 AutoCAD COM
  - 缺少 `pywin32` 时返回 `ACAD_COM_DEPENDENCY_MISSING`
- 增加本地 AutoCAD 实测清单：`docs/CAD_LOCAL_TESTS.md`
- 增加首个内置插件：
  - `toolbox/plugins/current_smoke`
  - 风险等级 `read`
  - 用于验证当前图 LISP 投喂链路
- 用户已提供 sample 图纸目录：`D:\codex\Yang Agent_CAD\sample`
- sample 当前匹配到 11 个 DWG。
- 新增小白操作文档：`docs/USER_GUIDE.md`
- 新增功能使用说明：`docs/FEATURES.md`
- 新增 sample 图纸测试说明：`docs/SAMPLE_DWGS.md`
- 新增批量烟测插件：`toolbox/plugins/batch_smoke`

## 下一步

1. 运行文档和代码验证：`scripts\test.cmd`、`scripts\doctor.cmd`。
2. 对 sample 目录做 dry-run 扫描。
3. 复制 `sample\S001.dwg` 到 `.agent\tmp\sample-run`，使用 `toolbox\plugins\batch_smoke\main.lsp` 做真实 accoreconsole 小测试。
4. 在真实 AutoCAD 中执行 `current_smoke` 插件测试。
5. 给当前图 LISP 执行增加结果文件/完成标记监听。

## 注意事项

- 用户是代码小白和 CAD 新手，所有步骤要尽量由 AI 主动完成。
- 任何批量修改 DWG 的功能必须等备份/回滚机制完成后再做。
- 本地 AutoCAD 能力需要在用户机器上实测。
- 当前 PowerShell 中 `python` 和 `py` 不在 PATH。可临时使用 Codex runtime Python：
  `C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe`
- PowerShell 当前禁止直接运行 `.ps1`，可使用 `.cmd` 包装脚本。
- `yang-cad-agent doctor` 已发现 AutoCAD 2027 accoreconsole。
- dry-run 冒烟测试生成过 `.agent/tasks/20260523-233502-d45edb72.json`，该目录被 gitignore 忽略。
- `batch-task` dry-run 冒烟测试生成过 `.agent/tasks/20260523-234016-c9285ed6.json`，该目录被 gitignore 忽略。
- `current-lisp` dry-run 冒烟测试生成过 `.agent/tasks/20260523-235227-1facdea4.json`，该目录被 gitignore 忽略。
- sample 目录中的 DWG 不要提交到 GitHub，`.gitignore` 已忽略 `*.dwg`。
- 第一次真实 accoreconsole 测试建议只用 `S001.dwg` 的复制件，不要直接跑完整 sample 目录。
- 已复制 `sample\S001.dwg` 到 `.agent\tmp\sample-run\S001-test.dwg` 做测试副本。
- 真实 accoreconsole 小测试任务 `20260524-003535-27fae317` 已运行：
  - 备份成功
  - accoreconsole 返回 `ACCORE_NONZERO_EXIT`
  - 日志显示 AutoCAD 配置/消息框初始化提示
  - rollback dry-run 成功
  - 真实 rollback 成功
- 已修复后续 accoreconsole 日志 UTF-16 解码逻辑。
- 复测任务 `20260524-004144-a4ee1196` 已运行：
  - 备份成功
  - accoreconsole 失败
  - 日志明确显示 `C:\Program Files\Autodesk\AutoCAD 2027\acad2027.cfg` 被锁定或只读
  - rollback dry-run 成功
  - 真实 rollback 成功
- 已新增错误码 `ACCORE_CONFIG_LOCKED` 和日志分析器。
- 已新增 accoreconsole 执行前预检。当前 `--execute` 会直接返回 `ACCORE_CONFIG_LOCKED`，不会启动 accoreconsole，因为：
  - `C:\Program Files\Autodesk\AutoCAD 2027\acad2027.cfg` 不存在
  - `C:\Program Files\Autodesk\AutoCAD 2027` 对普通用户不可写
- 已清理残留 accoreconsole 进程。
- 用户要求再次测试后，任务 `20260524-121718-3bda32fb` 仍被预检拦截为 `ACCORE_CONFIG_LOCKED`；没有启动 accoreconsole，没有残留进程。
- 新安装的 skills 需要重启 Codex 后才会完整生效。
