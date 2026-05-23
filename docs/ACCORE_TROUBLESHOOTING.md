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
3. 检查 `acad2027.cfg` 是否只读。
4. 如果 AutoCAD 是首次运行，先正常打开一次 AutoCAD，让它完成初始化。
5. 再回到本项目，重新对 `.agent\tmp\sample-run\S001-test.dwg` 的副本运行测试。

## 重要

不要直接对 `sample` 目录整批加 `--execute`。先只对副本测试。

