# 当前交接日志

## 2026-05-24 图纸体检总报告汇总器进展

- 新增模块 `src\yang_cad_agent\report_summary.py`。
- 新增 CLI 命令 `summarize-reports`，读取图层、块、文字标注三类 CSV，输出 `CAD_REPORT_SUMMARY.md`。
- 已在 `.agent\tmp\sample-run` 验证成功：
  - 输出 `.agent\tmp\sample-run\CAD_REPORT_SUMMARY.md`
  - 图层数量 12
  - 图层对象总数 93
  - 普通块参照总数 1
  - 文字/标注对象总数 24
  - CSV 报告文件数 3
- 新增测试 `tests\test_report_summary.py`。
- 下一步建议：做一个一键体检命令，自动依次跑三个只读插件并生成总报告。

## 2026-05-24 批量文字标注统计插件进展

- 新增插件 `batch.annotation_report`：
  - `toolbox\plugins\batch_annotation_report\plugin.json`
  - `toolbox\plugins\batch_annotation_report\main.lsp`
- 插件为只读报告插件，不修改 DWG；输出 `*.annotation-report.csv`。
- dry-run 已确认只匹配 `.agent\tmp\sample-run\S001-test.dwg` 测试副本。
- 真实执行任务 `20260524-142334-134fdfb5` 成功，成功 1、失败 0。
- 输出报告 `.agent\tmp\sample-run\S001-test.dwg.annotation-report.csv`，统计到注释类对象总数 24，其中 `MTEXT` 为 24。
- 已补充内置插件清单测试，当前必须包含五个插件。
- 下一步建议：把三份 CSV 合并为一个“图纸体检总报告”，或增加按图层分组的文字/标注统计。

## 2026-05-24 批量块统计插件进展

- 新增插件 `batch.block_report`：
  - `toolbox\plugins\batch_block_report\plugin.json`
  - `toolbox\plugins\batch_block_report\main.lsp`
- 插件为只读报告插件，不修改 DWG；输出 `*.block-report.csv`。
- dry-run 已确认只匹配 `.agent\tmp\sample-run\S001-test.dwg` 测试副本。
- 真实执行任务 `20260524-141102-64b18755` 成功，成功 1、失败 0。
- 输出报告 `.agent\tmp\sample-run\S001-test.dwg.block-report.csv`，统计到普通块参照总数 1。
- 已补充测试：内置插件清单必须包含当前四个插件。
- 下一步建议：开发“文字/标注体检报告”或增强块报告，加入块属性统计。

## 2026-05-24 批量图层统计插件进展

- 新增插件 `batch.layer_report`：
  - `toolbox\plugins\batch_layer_report\plugin.json`
  - `toolbox\plugins\batch_layer_report\main.lsp`
- 插件为只读报告插件，不修改 DWG；输出 `*.layer-report.csv`。
- 修复 accoreconsole runner：
  - `/s` 改为指向自动生成的 `.scr` 包装脚本。
  - 包装脚本设置 `SECURELOAD=0` 后加载真实 LISP。
  - `.agent\scripts/` 已加入 `.gitignore`。
- 增强错误识别：
  - 返回码为 0 但日志显示脚本未找到、加载取消时，返回 `LISP_LOAD_FAILED`。
  - 忽略 AutoCAD 2027 启动时固定出现的 `acad2027` 加载噪声。
- 验证：
  - `scripts\test.cmd` 通过 24 个测试。
  - dry-run 显示绝对 DWG 路径和 `.scr` 包装脚本路径。
  - 真实执行任务 `20260524-140529-fbc58d1a` 成功处理 `.agent\tmp\sample-run\S001-test.dwg`，成功 1、失败 0。
  - 输出报告 `.agent\tmp\sample-run\S001-test.dwg.layer-report.csv`，总对象数 93。
- 下一步建议：继续开发“块统计报告”或“文字/标注体检报告”，仍只在测试副本上验证。

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

## 2026-05-24 一键图纸体检命令进展

- 新增 `src\yang_cad_agent\health_check.py`。
- 新增 CLI 命令 `health-check`，默认 dry-run；加 `--execute` 后依次运行：
  - `batch.layer_report`
  - `batch.block_report`
  - `batch.annotation_report`
  - `summarize-reports`
- 已在 `.agent\tmp\sample-run\S001-test.dwg` 测试副本上验证真实 accoreconsole 链路成功。
- 真实执行任务：
  - `20260524-222837-175c7973`
  - `20260524-222915-9341bd0b`
  - `20260524-222924-210c5d45`
- 输出总报告：`.agent\tmp\sample-run\CAD_REPORT_SUMMARY.md`。
- 当前汇总结果：图层 12，图层对象 93，普通块参照 1，文字/标注对象 24，CSV 文件 3。
- 新增测试 `tests\test_health_check.py`。
- 下一步建议：把 `health-check` 暴露到 MCP stdio，方便 Codex/Antigravity 直接以 MCP 工具调用，而不是只走 CLI。

## 2026-05-24 MCP 一键体检 dry-run 工具进展

- 已把 `health-check` 的 dry-run 能力暴露为 MCP stdio 工具 `health_check`。
- MCP 工具固定 `execute=false`，不能通过 MCP 直接真实执行 accoreconsole。
- stdio 实测成功：
  - `list_tools` 能看到 `health_check`。
  - 调用 `health_check` 处理 `.agent\tmp\sample-run` 返回 `mode: dry_run`。
  - 即使参数传入 `execute:true`，仍然只 dry-run。
- dry-run 任务 ID：
  - `20260524-223512-d42cdea5`
  - `20260524-223512-c3989223`
  - `20260524-223512-ce2c04bb`
- 新增/更新测试 `tests\test_mcp_stdio.py`，当前测试数 28。
- 下一步建议：为 MCP 增加 `summarize_reports` 只读工具，或增加正式 MCP SDK server 包装层。

## 2026-05-24 MCP 报告汇总工具进展

- 已新增 MCP stdio 工具 `summarize_reports`。
- 该工具不启动 accoreconsole，不修改 DWG，只读取现有 CSV 报告并生成 Markdown 总报告。
- stdio 实测成功：
  - `list_tools` 能看到 `summarize_reports`。
  - 调用 `summarize_reports` 汇总 `.agent\tmp\sample-run` 成功。
  - 输出 `.agent\tmp\sample-run\CAD_REPORT_SUMMARY.md`。
  - 汇总结果：图层 12，图层对象 93，普通块参照 1，文字/标注对象 24，CSV 文件 3。
- 新增/更新测试 `tests\test_mcp_stdio.py`，当前测试数 29。
- 下一步建议：增加正式 MCP SDK server 包装层，或给 MCP 增加 `rollback_dry_run` 安全工具。

## 2026-05-24 MCP 回滚预演工具进展

- 已新增 MCP stdio 工具 `rollback_dry_run`。
- 该工具固定 dry-run，不会真实恢复或覆盖文件。
- 即使调用参数传入 `dry_run:false`，底层仍按 `dry_run=True` 调用。
- stdio 实测成功：
  - `list_tools` 能看到 `rollback_dry_run`。
  - 调用 `rollback_dry_run` 预览任务 `20260524-135319-fae4680f` 成功。
  - 返回 `dry_run: true` 和 `would_restore: true` 动作列表。
- 新增/更新测试 `tests\test_mcp_stdio.py`，当前测试数 30。
- 下一步建议：增加正式 MCP SDK server 包装层，或给 MCP 增加 `task_recent_failures` 只读工具，帮助 AI 快速定位最近错误。

## 2026-05-24 MCP 最近失败任务工具进展

- 新增只读模块 `src\yang_cad_agent\task_query.py`。
- 已新增 MCP stdio 工具 `task_recent_failures`。
- 该工具只读 `.agent\tasks`，不修改 DWG、不启动 AutoCAD、不写入任务记录。
- stdio 实测成功：
  - `list_tools` 能看到 `task_recent_failures`。
  - 调用 `task_recent_failures` 返回最近 3 个失败任务和错误码。
  - 当前返回：
    - `20260524-140406-3c2fbf05`：`LISP_LOAD_FAILED`
    - `20260524-140210-10f1cc04`：`LISP_LOAD_FAILED`
    - `20260524-134601-55f2f41a`：`ACCORE_CONFIG_LOCKED`
- 新增/更新测试：
  - `tests\test_task_query.py`
  - `tests\test_mcp_stdio.py`
- 当前测试数 32。
- 下一步建议：增加正式 MCP SDK server 包装层，或者做 `task_error_detail` 工具，把失败任务、accore 日志路径和回滚预演合并成一个排障包。

## 2026-06-03 MCP 失败任务排障包工具

- 已新增 MCP stdio 工具 `task_error_detail`。
- 工具用途：输入任务 ID，返回任务记录、错误码、状态、日志路径、日志尾部和回滚 dry-run 预演。
- 安全边界：只读，不修改 DWG，不启动 AutoCAD，不执行真实回滚。
- 实测任务：`20260524-140406-3c2fbf05`。
  - 返回错误码：`LISP_LOAD_FAILED`。
  - 返回日志尾部：包含 `文件加载已取消:D:/codex/Yang Agent_CAD/toolbox/plugins/batch_layer_report/main.lsp`。
  - 返回回滚预演：`dry_run: true`，`would_restore: true`。
- 验证：
  - `tests.test_mcp_stdio` 通过。
  - `compileall src tests` 通过。
  - `scripts\doctor.cmd` 通过。
  - 全量 `unittest discover` 当前被 Codex 沙箱临时目录权限拦截，报 `PermissionError`，不是功能断言失败。
- 环境提示：
  - `doctor` 检测到 AutoCAD 2027 accoreconsole 可用。
  - `acad2027.cfg` 已存在。
  - 当前有 `acad.exe` 进程运行；真实批量执行前应关闭 AutoCAD。
- 下一步建议：
  1. 为 MCP stdio 增加正式 MCP SDK server 包装层。
  2. 或继续增强 `task_error_detail`，增加错误码解释和建议动作字段。

## 2026-06-03 MCP 错误码结构化解释

- 已增强 `task_error_detail`。
- 除旧字段 `error_code` 外，现在返回结构化 `error`：
  - `code`
  - `meaning`
  - `suggestion`
  - `severity`
- 错误码解释集中在 `src\yang_cad_agent\error_codes.py` 的 `ERROR_DETAILS` 和 `explain_error_code(...)`。
- 未知错误码会保留原始 code，并回退为未分类错误解释。
- 实测任务 `20260524-140406-3c2fbf05` 返回：
  - `error.code`: `LISP_LOAD_FAILED`
  - `error.meaning`: `AutoCAD failed to load the LISP file.`
  - `error.suggestion`: `Check the script path, file encoding, secure load settings, and trusted paths.`
- 验证：
  - `tests.test_task_query tests.test_mcp_stdio` 通过。
  - `compileall src tests` 通过。
  - `scripts\doctor.cmd` 通过。
- 下一步建议：
  1. 把 `docs\ERROR_CODES.md` 和 `ERROR_DETAILS` 做一致性测试，防止文档和代码漂移。
  2. 或开发正式 MCP SDK server 包装层。

## 2026-06-03 错误码文档一致性校验

- 已新增 `tests\test_error_codes.py`。
- 测试会校验：
  - 所有错误码常量必须存在于 `ERROR_DETAILS`。
  - 所有错误码常量必须存在于 `docs\ERROR_CODES.md`。
  - 每个 `ERROR_DETAILS` 条目必须包含非空 `meaning`、`suggestion`，且 `severity` 只能是 `info`、`warning`、`error`。
- 已补齐文档缺口：`ACAD_COM_DEPENDENCY_MISSING`。
- 验证结果：
  - `tests.test_error_codes` 通过。
  - 全量 `unittest discover -s tests` 通过，当前 38 个测试。
  - `compileall src tests` 通过。
- 下一步建议：
  1. 开发正式 MCP SDK server 包装层。
  2. 或继续增强 `task_error_detail`，增加基于日志关键字的自动诊断原因。

## 2026-06-03 MCP 日志关键字诊断

- 已增强 `task_error_detail`，新增 `diagnostics` 字段。
- 当前诊断规则：
  - `acad_startup_noise`
  - `lisp_load_canceled`
  - `acad_config_locked`
  - `referenced_file_missing`
  - `no_log_rule_match`
- 实测任务 `20260524-140406-3c2fbf05` 返回：
  - `acad_startup_noise`
  - `lisp_load_canceled`
- 安全边界：只读分析日志尾部，不修改 DWG，不启动 AutoCAD，不执行回滚。
- 验证：
  - `tests.test_task_query tests.test_mcp_stdio` 通过。
  - `compileall src tests` 通过。
- 下一步建议：
  1. 把日志诊断规则拆成独立模块并增加更多 accoreconsole 失败样本。
  2. 或开发正式 MCP SDK server 包装层。
