# 项目状态

更新时间：2026-05-23

## 当前状态

项目处于安全可回滚 MVP 阶段。AI-first 开发规范、安全工作流、最小 Python CLI、备份/回滚基础模块、LISP 静态校验器、accoreconsole dry-run runner、完整批量任务入口和插件箱 manifest 校验已建立。

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

## 下一步

1. 增加开发环境 bootstrap 脚本，让用户机器上的普通命令也能运行。
2. 实现 MCP Server 最小工具：doctor、插件列表、任务查询。
3. 准备一套不含真实客户图纸的本地 AutoCAD 测试样例。
4. 实现当前图 LISP 投喂原型。
5. 增加首批实用插件，而不是测试插件。

## 风险

- AutoCAD/accoreconsole 只能在本机实际安装环境中完整验证。
- ezdxf 不能直接读取 DWG，必须避免把它当 DWG 引擎。
- AI 生成 LISP 有风险，必须先校验、记录、可回滚。
