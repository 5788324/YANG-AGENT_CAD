# sample 图纸测试说明

用户已准备 DWG 测试图纸目录：

```text
D:\codex\Yang Agent_CAD\sample
```

当前检测到 11 个 DWG：

| 文件 | 说明 |
| --- | --- |
| `S001.dwg` | 小型测试图纸 |
| `S100.dwg` | 测试图纸 |
| `S101.dwg` | 测试图纸 |
| `S102.dwg` | 测试图纸 |
| `S103.dwg` | 测试图纸 |
| `S104.dwg` | 测试图纸 |
| `S105.dwg` | 测试图纸 |
| `S106.dwg` | 测试图纸 |
| `S107.dwg` | 测试图纸 |
| `S108.dwg` | 测试图纸 |
| `S600.dwg` | 测试图纸 |

## 重要安全规则

1. `sample` 里的 DWG 也不要提交到 GitHub。
2. 第一次只做 dry-run。
3. 真实执行前必须确认备份目录。
4. 真实执行前建议先只测试一个小文件，例如复制 `S001.dwg` 到临时目录。

## 检查 sample 是否能被扫描

最简单方式：

```cmd
scripts\personal-health-check.cmd
```

该命令默认扫描 `sample`，只做 dry-run，不修改 DWG，并生成：

```text
.agent\reports\PERSONAL_HEALTH_PLAN.md
```

当前已验证：该命令能匹配 11 个 DWG，并为图层统计、块统计、文字标注统计三个只读插件生成 dry-run 任务。

## 当前测试副本真实只读体检结果

已复制：

```text
sample\S001.dwg -> .agent\tmp\sample-run\S001-test.dwg
```

已对测试副本运行：

```cmd
scripts\personal-health-check.cmd .agent\tmp\sample-run --execute
```

结果：

- 只匹配 1 个 DWG：`.agent\tmp\sample-run\S001-test.dwg`
- 5 个只读插件全部执行成功：
  - `batch.layer_report`
  - `batch.block_report`
  - `batch.annotation_report`
  - `batch.xref_image_report`
  - `batch.title_block_candidate_report`
- 已生成 5 个 CSV 和 1 个总报告：
  - `.layer-report.csv`
  - `.block-report.csv`
  - `.annotation-report.csv`
  - `.xref-image-report.csv`
  - `.title-block-candidate-report.csv`
  - `CAD_REPORT_SUMMARY.md`
- 总报告摘要：
  - 图层 12
  - 图层对象 93
  - 普通块参照 1
  - 文字/标注对象 24
  - 外参/图片/底图引用 4
  - 图框标题栏候选 0
- 回滚 dry-run 已验证成功，任务 `20260604-214229-d4f6c53d` 可预览恢复测试副本。

这个命令只预演，不修改 DWG：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli batch-task --script toolbox\plugins\batch_smoke\main.lsp sample
```

注意：不要加 `--execute`。这一步只确认系统能扫描 sample 目录，并生成将要执行的 accoreconsole 命令。

## 推荐的真实测试顺序

### 1. 复制一个小图纸到临时目录

```cmd
mkdir .agent\tmp\sample-run
copy sample\S001.dwg .agent\tmp\sample-run\S001-test.dwg
```

### 2. 准备一个 accore-safe LISP

创建文件：

```text
.agent\tmp\sample-run\noop-save.lsp
```

内容：

```lisp
(setvar "CMDDIA" 0)
(setvar "FILEDIA" 0)
(princ "\nYANG AGENT CAD batch smoke test")
(command "_.QSAVE")
(princ)
```

### 3. 先 dry-run

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli batch-task --script .agent\tmp\sample-run\noop-save.lsp .agent\tmp\sample-run
```

确认：

- `ok: true`
- `mode: dry_run`
- `file_count: 1`
- 文件列表里只有 `S001-test.dwg`

### 4. 再真实执行

确认上一步无误后再执行：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli batch-task --script .agent\tmp\sample-run\noop-save.lsp .agent\tmp\sample-run --execute
```

预期：

- 自动备份 `S001-test.dwg`
- 调用 AutoCAD 2027 accoreconsole
- 生成任务记录
- 返回 `rollback_command`

如果返回 `ACCORE_CONFIG_LOCKED` 或 `ACCORE_NONZERO_EXIT`，先不要继续批量执行。查看任务日志和 `.agent\logs\任务ID`。当前已知情况是 AutoCAD 2027 的 `acad2027.cfg` 可能被锁定或只读，导致 accoreconsole 返回非 0。

当前版本已经加入执行前预检。如果 `acad2027.cfg` 不存在且安装目录不可写，系统会在启动 accoreconsole 前直接停止。

### 5. 回滚预演

把上一步返回的任务 ID 填进去：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli rollback 任务ID --dry-run
```

### 6. 真实回滚

确认 dry-run 输出正确后：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli rollback 任务ID
```

## 交给其他 AI 的提示词

可以直接复制：

```text
请阅读 AGENTS.md、handoff/CURRENT_HANDOFF.md、docs/SAMPLE_DWGS.md。
使用 sample 目录里的 DWG 做测试，但不要提交 DWG。
先用 S001.dwg 的复制件做 dry-run，再考虑 --execute。
所有真实执行前必须确认备份和回滚命令。
```
