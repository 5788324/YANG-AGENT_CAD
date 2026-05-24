# accoreconsole 排错说明

## 当前已知问题：ACCORE_CONFIG_LOCKED

真实执行 accoreconsole 时，如果返回：

```text
ACCORE_CONFIG_LOCKED
```

或日志中出现：

```text
acad2027.cfg
配置文件可能被其他进程锁定，或者已设置为只读
无法处理配置文件
```

说明 AutoCAD 2027 的配置文件无法写入或被占用。

当前检测到的路径：

```text
C:\Program Files\Autodesk\AutoCAD 2027\acad2027.cfg
```

## 处理建议

1. 关闭所有 AutoCAD 和 accoreconsole 进程。
2. 用任务管理器确认没有 `acad.exe`、`accoreconsole.exe`。
3. 运行 `scripts\doctor.cmd`，确认 `autocad_processes.status` 是 `ok`。
4. 检查 `acad2027.cfg` 是否存在、是否只读。
5. 如果 `acad2027.cfg` 不存在，并且 `install_dir_writable` 是 `false`，说明普通用户不能在 AutoCAD 安装目录创建配置文件。
6. 如果 AutoCAD 是首次运行，先正常打开一次 AutoCAD，让它完成初始化。
7. 必要时用管理员权限运行 AutoCAD 一次，让配置文件完成创建。
8. 再回到本项目，重新对 `.agent\tmp\sample-run\S001-test.dwg` 的副本运行测试。

## 当前保护机制

项目现在会在 `--execute` 前预检 accoreconsole 环境：

- 如果发现 `acad2027.cfg` 不存在且安装目录不可写，会直接返回 `ACCORE_CONFIG_LOCKED`。
- 这种情况下不会启动 accoreconsole。
- 这种情况下不会新建备份。
- 这样可以避免重复失败和残留进程。

## 重要

不要直接对 `sample` 目录整批加 `--execute`。先只对副本测试。
