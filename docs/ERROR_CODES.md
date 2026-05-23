# 错误码规范

所有失败都必须返回错误码、简短说明、日志路径和建议动作。

## 通用错误

| 错误码 | 含义 | 建议 |
| --- | --- | --- |
| UNKNOWN_ERROR | 未分类错误 | 查看完整日志 |
| VALIDATION_FAILED | 执行前校验失败 | 修正参数或脚本 |
| VERIFY_FAILED | 执行后验证失败 | 检查图纸状态并考虑回滚 |
| USER_CONFIRMATION_REQUIRED | 需要用户确认 | 等待用户确认 |

## MCP 错误

| 错误码 | 含义 | 建议 |
| --- | --- | --- |
| MCP_TOOL_MISSING | 没有可用 MCP 工具 | 尝试临时 LISP，或开发新 MCP tool |
| MCP_SERVER_UNAVAILABLE | MCP Server 不可用 | 重启 MCP Server |
| MCP_TOOL_FAILED | MCP tool 执行失败 | 查看 tool 日志 |

## AutoCAD / COM 错误

| 错误码 | 含义 | 建议 |
| --- | --- | --- |
| ACAD_COM_UNAVAILABLE | 无法连接 AutoCAD | 打开 AutoCAD 后重试 |
| ACAD_DOC_UNAVAILABLE | 无活动图纸 | 打开 DWG 后重试 |
| ACAD_COMMAND_TIMEOUT | 命令执行超时 | 检查命令是否卡在交互输入 |
| ACAD_RPC_FAILED | AutoCAD RPC 失败 | 重启 AutoCAD |

## LISP 错误

| 错误码 | 含义 | 建议 |
| --- | --- | --- |
| LISP_VALIDATE_FAILED | LISP 静态校验失败 | 修改危险函数或不兼容函数 |
| LISP_LOAD_FAILED | LISP 加载失败 | 检查路径、编码、TRUSTEDPATHS |
| LISP_RUNTIME_FAILED | LISP 运行时报错 | 查看 AutoCAD 命令行或日志 |
| LISP_INTERACTIVE_FORBIDDEN | 批量脚本含交互函数 | 改写为无交互版本 |

## accoreconsole 错误

| 错误码 | 含义 | 建议 |
| --- | --- | --- |
| ACCORE_NOT_FOUND | 未找到 accoreconsole | 检查 AutoCAD 安装路径 |
| ACCORE_NONZERO_EXIT | accoreconsole 返回非 0 | 查看 stdout/stderr |
| ACCORE_TIMEOUT | 执行超时 | 降低并行数或检查脚本死循环 |
| ACCORE_UNSUPPORTED_SCRIPT | 脚本不支持无头运行 | 改写 LISP 或走当前图轨道 |

## 文件错误

| 错误码 | 含义 | 建议 |
| --- | --- | --- |
| DWG_LOCKED | DWG 被占用 | 关闭图纸后重试 |
| FILE_NOT_FOUND | 文件不存在 | 检查路径 |
| BACKUP_FAILED | 备份失败 | 停止执行，检查权限和磁盘 |
| ROLLBACK_FAILED | 回滚失败 | 使用备份目录手动恢复 |

