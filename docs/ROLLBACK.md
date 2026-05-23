# 撤销与回退机制

本项目必须默认保护用户图纸。任何修改型任务都要优先考虑如何撤销。

## 三层回退

### 1. AutoCAD UNDO Group

适用于当前图纸轻量操作。执行前开启 undo group，失败时尝试 undo。

### 2. 文件级备份

适用于当前图纸高风险操作和所有批量修改。执行前复制 DWG 到：

```text
.agent/backups/<task_id>/
```

同时写入 manifest：

```json
{
  "task_id": "",
  "files": [
    {
      "original": "",
      "backup": "",
      "sha256_before": "",
      "sha256_after": ""
    }
  ]
}
```

### 3. 任务级回滚

通过 task_id 找到 manifest，把备份文件恢复到原路径。

## 回滚命令目标

后续 CLI 应支持：

```powershell
yang-cad-agent rollback <task_id>
yang-cad-agent rollback <task_id> --dry-run
```

## 回滚规则

- 回滚前也要备份当前文件，避免二次损失。
- 如果原文件正在被 AutoCAD 打开，返回 `DWG_LOCKED`。
- 如果备份不存在，返回 `ROLLBACK_FAILED`。
- 回滚完成后重新计算 hash 并写入任务日志。

