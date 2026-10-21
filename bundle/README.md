# Bundle

通用 AutoCAD 模块使用 `Platform="AutoCAD*"`，Civil 专业扩展使用 `Platform="Civil3D"`。构建脚本按年份生成精确 `SeriesMin=SeriesMax`，防止新版本宿主误加载旧目标框架 DLL。
