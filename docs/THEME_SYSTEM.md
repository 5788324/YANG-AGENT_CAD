# Theme System

0.2.0 起只保留 3 套：

- `Obsidian` — 墨曜，深色默认；
- `Porcelain` — 月瓷，暖白；
- `Mist` — 雾蓝，浅冷色。

每套必须定义：`WindowBackgroundBrush`、`SurfaceBrush`、`SurfaceAltBrush`、`TextPrimaryBrush`、`TextSecondaryBrush`、`AccentBrush`、`AccentSoftBrush`、`AccentTextBrush`、`BorderBrush`、`DangerBrush`、`SuccessBrush`。

业务 XAML 使用 DynamicResource；主题只作用于 YangCAD Window.Resources，不创建第二个 WPF Application、不修改 AutoCAD 全局 Resources。

`verify_source.py` 将主题集合锁定为这 3 个，旧 Midnight/Jade/Aubergine/Amber/Graphite 文件已删除。
