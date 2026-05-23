# 开发环境说明

## 当前发现

本机 PowerShell 当前无法直接使用：

```powershell
python
py
```

但 Codex desktop 提供的 Python 可用：

```powershell
& 'C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -m compileall src tests
```

运行本项目 CLI 时需要临时设置：

```powershell
$env:PYTHONPATH='src'
& 'C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -m yang_cad_agent.cli doctor --json
```

运行测试：

```powershell
$env:PYTHONPATH='src'
& 'C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -m unittest discover -s tests
```

accoreconsole dry-run 冒烟测试格式：

```powershell
$env:PYTHONPATH='src'
& 'C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -m yang_cad_agent.cli accore-run --script .agent\tmp\smoke\safe.lsp --dry-run .agent\tmp\smoke
```

## 后续目标

后续应提供一键开发环境：

```powershell
scripts\bootstrap.ps1
scripts\doctor.ps1
```

目标是让用户无需理解 Python 环境，只要双击或让 AI 运行脚本即可。

## Windows 执行策略

如果 PowerShell 禁止运行 `.ps1`，可直接使用 `.cmd` 包装脚本：

```cmd
scripts\doctor.cmd
scripts\test.cmd
```

推荐小白用户优先阅读 [USER_GUIDE.md](USER_GUIDE.md)，不要直接运行带 `--execute` 的命令。
