# YangCAD 结构

| 部分 | 路径 | 职责 |
|---|---|---|
| AutoCAD 命令 | src/YangCAD.AutoCAD/Commands | 原生命令、拾点 |
| 绘图和图纸服务 | src/YangCAD.AutoCAD/Services/Cad | 几何、图元、输出副本 |
| 界面和目录 | src/YangCAD.AutoCAD/UI、Toolbox | Ribbon、面板、命令提示 |
| 本地自动化 | src/YangCAD.AutoCAD/Mcp、mcp | Gateway、请求调度、Python stdio |
| 共享契约 | src/YangCAD.Core | RPC 与公共数据 |
| Civil 3D | src/YangCAD.Civil3D | Civil 宿主入口和 API |
| 文件处理 | src/YangCAD.BatchWorker、workers/ImageEmbed | Core Console 批处理、引用内嵌 |
| 构建与安装 | tools、bundle、src/YangCAD.Setup | 编译、封包、加载、安装 |

界面通过原生命令进入 CAD，命令与 MCP 共用已有服务，写入由合法宿主上下文和事务执行。多边形大样与边界外裁切共享 PolygonBoundary2d。实机范围见 [当前任务](../tasks/CURRENT_TASK.md)。
