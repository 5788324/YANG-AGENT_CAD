# Agent Runtime Contract

本文件定义 Codex、Antigravity、Claude 和 MCP Server 都要遵守的统一运行契约。

## 轨道定义

| 轨道 | 名称 | 用途 | 是否需要 AutoCAD UI |
| --- | --- | --- | --- |
| MCP | 控制平面 | 能力发现、插件调度、任务记录、回滚 | 视工具而定 |
| A | COM/MCP 实时 | 当前图查询、预览、交互 | 是 |
| B | LISP 当前图 | 当前图快速修改 | 是 |
| C | accoreconsole | 多 DWG 批量处理 | 否 |

推荐执行链：

```text
自然语言
→ MCP 能力发现
→ 插件箱命中则执行插件
→ 未命中则生成临时 LISP
→ 当前图走 B，批量走 C
→ 验证结果
→ 用户认可后沉淀为插件
```

## 风险分级

| 风险 | 示例 | 是否需要确认 | 是否需要备份 |
| --- | --- | --- | --- |
| read | 查询图层、查询块 | 否 | 否 |
| current_light | 当前图标注、颜色、小范围修改 | 视情况 | 建议 |
| current_destructive | purge、删除、替换大量对象 | 是 | 是 |
| batch_modify | 批量改 DWG | 是 | 是 |
| batch_export | 批量导 PDF/DXF | 是 | 输出目录检查 |
| destructive | 删除文件、覆盖原文件、清理备份 | 是 | 是 |

## 任务执行必备字段

每个任务必须生成 task_id，并写入 `.agent/tasks/<task_id>.json`：

```json
{
  "task_id": "20260523-000001",
  "status": "pending",
  "risk": "batch_modify",
  "track": "C",
  "user_goal": "",
  "files": [],
  "script_path": "",
  "params": {},
  "backup_dir": "",
  "started_at": "",
  "finished_at": "",
  "error_code": null,
  "rollback_available": false
}
```

## 结束条件

任务只有在以下条件满足时才能汇报完成：

1. 执行进程结束。
2. 日志已写入。
3. 成功/失败数量已统计。
4. 如果是修改任务，验证步骤已完成或明确说明无法验证。
5. 如失败，必须返回 error_code 和日志路径。

