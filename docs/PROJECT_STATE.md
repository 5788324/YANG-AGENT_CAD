# 项目状态

## 2026-05-24 图纸体检总报告汇总器

- 新增 CLI 命令 `summarize-reports`，用于把 `*.layer-report.csv`、`*.block-report.csv`、`*.annotation-report.csv` 汇总为 `CAD_REPORT_SUMMARY.md`。
- 新增模块 `src\yang_cad_agent\report_summary.py`。
- 已在 `.agent\tmp\sample-run` 验证成功，输出 `.agent\tmp\sample-run\CAD_REPORT_SUMMARY.md`。
- 当前测试副本汇总结果：图层 12、图层对象 93、普通块参照 1、文字/标注对象 24、CSV 文件 3。
- 新增单元测试 `tests\test_report_summary.py`，覆盖 Markdown 汇总生成。

## 2026-05-24 批量文字标注统计插件

- 新增内置插件 `batch.annotation_report`，路径：`toolbox\plugins\batch_annotation_report`。
- 插件用途：用 accoreconsole 批量统计 DWG 中的 `TEXT`、`MTEXT`、`DIMENSION`、`LEADER`、`MULTILEADER`、`ACAD_TABLE` 数量。
- 已在测试副本 `.agent\tmp\sample-run\S001-test.dwg` 验证成功，任务 `20260524-142334-134fdfb5`，成功 1、失败 0。
- 已生成报告 `.agent\tmp\sample-run\S001-test.dwg.annotation-report.csv`，统计到注释类对象总数 24，其中 `MTEXT` 为 24。
- 内置插件清单测试已补充 `batch.annotation_report`。

## 2026-05-24 批量块统计插件

- 新增内置插件 `batch.block_report`，路径：`toolbox\plugins\batch_block_report`。
- 插件用途：用 accoreconsole 批量读取 DWG 块表，输出 `*.block-report.csv`，包含块名、是否外部参照、是否布局块、插入数量。
- 已在测试副本 `.agent\tmp\sample-run\S001-test.dwg` 验证成功，任务 `20260524-141102-64b18755`，成功 1、失败 0。
- 已生成报告 `.agent\tmp\sample-run\S001-test.dwg.block-report.csv`，统计到普通块参照总数 1。
- 新增测试覆盖：内置插件清单必须包含 `batch.block_report`、`batch.layer_report`、`batch.smoke_qsave`、`current.smoke_test`。

## 2026-05-24 批量图层统计插件

- 新增内置插件 `batch.layer_report`，路径：`toolbox\plugins\batch_layer_report`。
- 插件用途：用 accoreconsole 批量读取 DWG 图层表，输出 `*.layer-report.csv`，包含图层名、颜色、锁定/冻结状态、对象数量。
- 已修复 accoreconsole runner：`/s` 现在使用自动生成的 `.scr` 包装脚本加载 LISP，避免 `.lsp` 被当成 `.scr` 查找。
- 已增强 accoreconsole 日志分析：忽略 AutoCAD 2027 启动时的 `acad2027` 加载噪声；真正的脚本缺失、加载取消会返回 `LISP_LOAD_FAILED`。
- 已在测试副本 `.agent\tmp\sample-run\S001-test.dwg` 验证成功，任务 `20260524-140529-fbc58d1a`，成功 1、失败 0。
- 已生成报告 `.agent\tmp\sample-run\S001-test.dwg.layer-report.csv`，统计总对象数 93。

## 2026-05-24 管理员修复脚本体验更新

- 用户反馈 `scripts\fix-acad-cfg.cmd` 管理员运行时窗口秒退。
- 脚本已更新为在所有成功/失败出口停住窗口，便于用户把结果反馈给 AI。
- 复查确认安装目录 `acad2027.cfg` 已存在，accoreconsole 环境预检恢复正常。
- 首个真实 accoreconsole 小样本执行成功：任务 `20260524-135319-fae4680f` 对测试副本 `.agent\tmp\sample-run\S001-test.dwg` 执行成功，成功 1、失败 0，并保留自动备份。

## 2026-05-24 accoreconsole 配置诊断更新

- 再次测试后确认 `C:\Program Files\Autodesk\AutoCAD 2027\acad2027.cfg` 仍不存在。
- 已找到用户配置文件：`C:\Users\YANG\AppData\Local\Autodesk\AutoCAD 2027\R26.0\chs\acad2027.cfg`。
- 直接复制到 `C:\Program Files\Autodesk\AutoCAD 2027\acad2027.cfg` 因权限不足失败。
- 新增 `scripts\fix-acad-cfg.cmd`，需要用户右键“以管理员身份运行”，用于把用户配置复制到 AutoCAD 安装目录。
- `doctor` 现在会报告 `user_cfg` / `user_cfg_exists`，并在可修复时提示管理员复制路径。
- 非管理员环境读取用户 cfg 也可能返回访问拒绝；doctor 已改为记录 `user_cfg_check_error`，不会崩溃。
- 最新复测：`batch-task --execute` 被 `ACCORE_CONFIG_LOCKED` 安全拦截，未启动 accoreconsole，未修改 DWG。

更新时间：2026-05-24

## 当前状态

项目处于安全可回滚 MVP 阶段。AI-first 开发规范、安全工作流、最小 Python CLI、备份/回滚基础模块、LISP 静态校验器、accoreconsole dry-run runner、完整批量任务入口、插件箱 manifest 校验、任务查询命令、MCP stdio 骨架、当前图 LISP dry-run 投喂原型、首个内置插件和小白操作文档已建立。

## 已确认需求

- 用户是代码小白和 CAD 新手，项目由 AI 全程开发和维护。
- 需要适配 Codex 和 Antigravity。
- 需要插件箱，类似源泉工具箱。
- 所有操作优先经过 MCP/插件能力。
- MCP 没有能力时，AI 可生成临时 LISP 执行。
- 用户认为有用的 LISP 可以沉淀进插件箱。
- 批量任务使用 accoreconsole。
- 必须支持撤销、回退、报错代码反馈。
- 每天要有工作日志和交接日志。
- 每次开始工作先从 GitHub 更新，每次结束工作提交并推送。

## 当前决策

- MCP 是控制平面，不只是执行轨道。
- 插件箱是能力沉淀层。
- LISP 是动作表达层。
- accoreconsole 是批量运行层。
- 所有修改任务必须进入 task ledger。
- 批量修改默认 dry-run + 备份 + 用户确认。

## 本机环境发现

- Codex runtime Python 可用：`C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe`
- 普通 `python` / `py` 命令当前不在 PATH。
- Git 可用：`git version 2.53.0.windows.2`
- AutoCAD 2027 accoreconsole 已发现：`C:\Program Files\Autodesk\AutoCAD 2027\accoreconsole.exe`
- sample 图纸目录可用：`D:\codex\Yang Agent_CAD\sample`，当前匹配到 11 个 DWG。

## 下一步

1. 解决 AutoCAD 2027 `acad2027.cfg` 锁定/只读问题。
2. 重新对 `.agent\tmp\sample-run\S001-test.dwg` 做真实 accoreconsole 小测试。
3. 在真实 AutoCAD 中执行 `current_smoke` 插件测试。
4. 给当前图 LISP 执行增加结果文件/完成标记监听。
5. 增加更多实用插件，例如图层统计、块统计、图框属性读取。

## 风险

- AutoCAD/accoreconsole 只能在本机实际安装环境中完整验证。
- 当前 accoreconsole 真实执行被预检阻止：AutoCAD 2027 `acad2027.cfg` 不存在且安装目录不可写。已验证备份和回滚安全链路可用。
- ezdxf 不能直接读取 DWG，必须避免把它当 DWG 引擎。
- AI 生成 LISP 有风险，必须先校验、记录、可回滚。

## 2026-05-24 一键图纸体检命令

- 新增模块 `src\yang_cad_agent\health_check.py`。
- 新增 CLI 命令 `health-check`，用于一次性串联 `batch.layer_report`、`batch.block_report`、`batch.annotation_report`，并在真实执行后自动调用 `summarize-reports` 生成 `CAD_REPORT_SUMMARY.md`。
- 默认模式为 dry-run，只检查匹配 DWG 和将要运行的 accoreconsole 命令；加 `--execute` 后才真实运行。
- 已在测试副本 `.agent\tmp\sample-run\S001-test.dwg` 验证成功：
  - dry-run 任务：`20260524-222829-6b48f206`、`20260524-222829-ff953b91`、`20260524-222830-8eb87f86`
  - execute 任务：`20260524-222837-175c7973`、`20260524-222915-9341bd0b`、`20260524-222924-210c5d45`
  - 汇总输出：`.agent\tmp\sample-run\CAD_REPORT_SUMMARY.md`
  - 汇总结果：图层 12、图层对象 93、普通块参照 1、文字/标注对象 24、CSV 文件 3
- 新增测试 `tests\test_health_check.py`，确认一键体检依赖的三个内置插件脚本存在。

## 2026-05-24 MCP 一键体检 dry-run 工具

- MCP stdio 新增工具 `health_check`。
- 工具参数：`root`、`folder`、`pattern`、`recursive`。
- MCP 层固定 `execute=false`，即使调用方传入 `execute:true` 也不会真实启动 accoreconsole。
- 已验证 `list_tools` 能返回 `health_check`。
- 已验证 stdio 调用 `health_check` 对 `.agent\tmp\sample-run` 只执行 dry-run，并生成三个 dry-run 任务记录：
  - `20260524-223512-d42cdea5`
  - `20260524-223512-c3989223`
  - `20260524-223512-ce2c04bb`
- 测试更新：`tests\test_mcp_stdio.py` 覆盖 `health_check` 工具存在，以及 MCP 层强制 dry-run。
