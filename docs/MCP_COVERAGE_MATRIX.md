# MCP_COVERAGE_MATRIX

更新时间：2026-09-07  
范围：YangCAD 0.9 CADTool ↔ YangTools 业务对齐候选。**每个公开 MCP Tool / 真实业务动作一行。**

> `PASS` 仅表示源码/静态产品契约已实现并被本候选静态门禁覆盖；不等于 Windows Runtime PASS。真实 AutoCAD/Civil/GUI↔MCP/E2E 证据另行验收。

| 功能 | GUI入口 | Business Service | MCP Tool | Read/Write | DryRun | Result Readback | 状态 |
|---|---|---|---|---|---|---|---|
| 检查版本化 Extension 的本地编译环境；不加载 DLL、不访问 live Gateway。 | 无（MCP 产品/高级入口） | `yangcad_mcp.extensions`（MCP-local Extension Workflow） | `extension.status` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 从受控 Execute body 生成并编译版本化 Extension。写扩展必须 RecordChange；不直接加载进 CAD。 | 无（MCP 产品/高级入口） | `yangcad_mcp.extensions`（MCP-local Extension Workflow） | `extension.build` | Write | 不适用 | 结构化 JSON/dict | PASS |
| 把已编译且 SHA 校验通过的版本化 DLL 安装到 Extension 目录；必须显式 allow_install=true。 | 无（MCP 产品/高级入口） | `yangcad_mcp.extensions`（MCP-local Extension Workflow） | `extension.install` | Write | 否（`allow_install` 显式门禁） | 结构化 JSON/dict | PASS |
| 只读列出 Extension workspace/installed manifest 与 SHA 状态；不会加载 DLL。 | 无（MCP 产品/高级入口） | `yangcad_mcp.extensions`（MCP-local Extension Workflow） | `extension.artifact.list` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 读取 YangCAD / AutoCAD / Civil 3D 当前运行状态。 | 无（MCP 产品/高级入口） | `Mcp/Tools/CadReadTools` | `cad.status` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 读取当前 DWG 基础信息。 | 无（MCP 产品/高级入口） | `Mcp/Tools/CadReadTools` | `cad.document.info` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 读取当前 CAD PickFirst 选择集。 | 无（MCP 产品/高级入口） | `Mcp/Tools/CadReadTools` | `cad.selection.read` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 按 DWG Handle 读取实体。 | 无（MCP 产品/高级入口） | `Mcp/Tools/CadReadTools` | `cad.entity.get` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 查询模型空间实体。 | 无（MCP 产品/高级入口） | `Mcp/Tools/CadReadTools` | `cad.entity.query` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 修改图层/颜色/线型/文字；写入前先读取当前 DWG 建立绑定，复杂任务建议先 dry_run。 | 无（MCP 产品/高级入口） | `Mcp/Tools/CadWriteTools` | `cad.entity.update` | Write | 是 | 结构化 JSON/dict | PASS |
| 移动/旋转/缩放实体；写入前先读取当前 DWG 建立绑定。 | 无（MCP 产品/高级入口） | `Mcp/Tools/CadWriteTools` | `cad.entity.transform` | Write | 是 | 结构化 JSON/dict | PASS |
| 删除实体；控制中心默认禁用，Tool 默认 dry_run，且要求先读取当前 DWG 建立绑定。 | 无（MCP 产品/高级入口） | `Mcp/Tools/CadWriteTools` | `cad.entity.erase` | Write | 是 | 结构化 JSON/dict | PASS |
| 列出全部图层。 | 无（MCP 产品/高级入口） | `Mcp/Tools/CadReadTools` | `cad.layer.list` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 确保图层存在；写入前先读取当前 DWG 建立绑定。 | 无（MCP 产品/高级入口） | `Mcp/Tools/CadWriteTools` | `cad.layer.ensure` | Write | 是 | 结构化 JSON/dict | PASS |
| Revit 图纸预处理。GUI/MCP 共用 RevitExchangeService；默认 dry_run，real 输出 RVT DWG/MAP/REPORT。 | 工具箱 → Revit 图纸预处理 | `RevitExchangeService` | `project.revit.prepare` | Write | 是 | `success/map_path/report_path/scanned_entities/classified_entities/unsupported_count/warnings` | PASS |
| 批量图片内嵌。GUI/MCP 共用 ImageEmbedBatchService；默认 dry_run，real 启动独立 AutoCAD worker。 | 工具箱 → 批量图片内嵌 | `ImageEmbedBatchService` | `project.image.embed` | Write | 是 | `success/job_id/status_path/output_folder/file_count/warnings` | PASS |
| 图纸比例转换。Ribbon/MCP 共用 ScaleConversionService；支持标准 1:1 与缩尺绘图两种模式，默认 dry_run。 | Ribbon → 比例转换（YGSCALE） | `ScaleConversionService` | `cad.scale.convert` | Write | 是 | `mode/old_scale/new_scale/geometry_factor/annotation_factor/processed/skipped/failed/issues/undo_semantics` | PASS |
| 创建 Line。默认 dry_run；real 前先读取当前 DWG 建立绑定。 | 无（MCP 产品/高级入口） | `Mcp/Tools/CadCreateTools` | `cad.line.create` | Write | 是 | 结构化 JSON/dict | PASS |
| 创建 2D lightweight Polyline；顶点必须共面同 Z。默认 dry_run。 | 无（MCP 产品/高级入口） | `Mcp/Tools/CadCreateTools` | `cad.polyline.create` | Write | 是 | 结构化 JSON/dict | PASS |
| 创建 DBText/MText。默认 dry_run。 | 无（MCP 产品/高级入口） | `Mcp/Tools/CadCreateTools` | `cad.text.create` | Write | 是 | 结构化 JSON/dict | PASS |
| 列出可插入的块定义和属性定义数量。 | 无（MCP 产品/高级入口） | `Mcp/Tools/CadCreateTools` | `cad.block.list` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 插入现有块定义并按 Tag 写非 Constant 属性。默认 dry_run。 | 无（MCP 产品/高级入口） | `Mcp/Tools/CadCreateTools` | `cad.block.insert` | Write | 是 | 结构化 JSON/dict | PASS |
| 检查 2D Lightweight Polyline 的重复点、零长段、共线冗余点及 cleanup 限制。 | 无（MCP 产品/高级入口） | `PolylineInspectionService` | `cad.polyline.inspect` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 安全清理直线型 2D Polyline。默认 dry_run；弧段/逐段变宽 Polyline 会明确拒绝。 | 无（MCP 产品/高级入口） | `PolylineCleanupService` | `cad.polyline.clean` | Write | 是 | 结构化 JSON/dict | PASS |
| 审计图层状态、模型空间实体数量和 AutoCAD Purge 可清理性。 | 无（MCP 产品/高级入口） | `LayerAuditService` | `cad.layer.audit` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 仅删除 AutoCAD Database.Purge 判定为可安全清理的未使用图层。默认 dry_run，且需要删除权限。 | 无（MCP 产品/高级入口） | `LayerCleanupService` | `cad.layer.cleanup` | Write | 是 | 结构化 JSON/dict | PASS |
| 只读扫描模型空间，汇总实体/图层/块以及零长线、空文字、Polyline 零长段等常见问题。 | 无（MCP 产品/高级入口） | `DrawingAuditService` | `cad.drawing.audit` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 查看任意 Autodesk DBObject 的运行时类型、公共属性以及通用层可调用的方法。 | 无（MCP 产品/高级入口） | `Mcp/Generic/GenericTools` | `cad.object.describe` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 读取对象的 JSON-safe 公共属性；碰到没有专用 Tool 的对象时优先先 describe/get。 | 无（MCP 产品/高级入口） | `Mcp/Generic/GenericTools` | `cad.property.get` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 受控通用属性写入。默认 dry_run；仅公共且可安全转换的 Autodesk setter 可写。 | 无（MCP 产品/高级入口） | `Mcp/Generic/GenericTools` | `cad.property.set` | Write | 是 | 结构化 JSON/dict | PASS |
| 列出对象上的公共 Autodesk 方法、精确 signature 与是否允许通用调用。 | 无（MCP 产品/高级入口） | `Mcp/Generic/GenericTools` | `cad.api.describe` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 受控调用公共 Autodesk API。默认 dry_run；不支持 ref/out、危险基础设施方法和复杂未知参数。 | 无（MCP 产品/高级入口） | `Mcp/Generic/GenericTools` | `cad.api.invoke` | Write | 是 | 结构化 JSON/dict | PASS |
| 实验性同步 AutoCAD 命令 fallback。默认 dry_run 且控制中心默认禁用；禁止交互 PauseToken/脚本拼接。 | 无（MCP 产品/高级入口） | `Mcp/Generic/GenericTools` | `cad.command.run` | Write | 是 | 结构化 JSON/dict | PASS |
| 列出 %LOCALAPPDATA%/YangCADAgent/Extensions 中可发现的版本化扩展 operation。 | 无（MCP 产品/高级入口） | `Mcp/Generic/GenericTools` | `cad.extension.list` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 调用动态扩展 operation。默认 dry_run 且控制中心默认禁用；用于专用 Tool/通用 API 都覆盖不到的场景。 | 无（MCP 产品/高级入口） | `Mcp/Generic/GenericTools` | `cad.extension.invoke` | Write | 是 | 结构化 JSON/dict | PASS |
| 读取 Civil 3D API 状态。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilPipeStructureTools` / `CivilTools` | `civil.status` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 列出 Surface。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilReadDesignTools` / `CivilProductionReadAssistTools` | `civil.surface.list` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 列出 Alignment。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilReadDesignTools` / `CivilProductionReadAssistTools` | `civil.alignment.list` | Read | 不适用 | 结构化 JSON/dict | PASS |
| Alignment XY → 桩号/偏距。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilReadDesignTools` / `CivilProductionReadAssistTools` | `civil.alignment.station_offset` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 列出 gravity pipe network Structure/检查井，含井顶、井底、自动贴合和连接管道。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilPipeStructureTools` / `CivilTools` | `civil.structure.list` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 按 Handle 读取一个 gravity Structure/检查井。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilPipeStructureTools` / `CivilTools` | `civil.structure.get` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 修改 gravity Structure。默认 dry_run；锁定井顶时应同时关闭 automatic_rim_surface_adjustment。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilPipeStructureTools` / `CivilTools` | `civil.structure.update` | Write | 是 | 结构化 JSON/dict | PASS |
| 列出 gravity Pipe，含端点中心高程、管内底高程、尺寸、坡度和连接井。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilPipeStructureTools` / `CivilTools` | `civil.pipe.list` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 按 Handle 读取一个 gravity Pipe。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilPipeStructureTools` / `CivilTools` | `civil.pipe.get` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 修改 gravity Pipe 起终点中心/管内底高程，XY 保持不变。默认 dry_run。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilPipeStructureTools` / `CivilTools` | `civil.pipe.update` | Write | 是 | 结构化 JSON/dict | PASS |
| 按 Handle 读取 Surface 基本属性。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilReadDesignTools` / `CivilProductionReadAssistTools` | `civil.surface.get` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 查询 Surface 指定 XY 的高程；范围外返回 on_surface=false。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilReadDesignTools` / `CivilProductionReadAssistTools` | `civil.surface.elevation` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 读取 Surface 边界定义和离散顶点，使用 vertex_limit 防止超大响应。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilReadDesignTools` / `CivilProductionReadAssistTools` | `civil.surface.boundaries` | Read | 不适用 | 结构化 JSON/dict | PASS |
| Alignment 桩号/偏距 → XY。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilReadDesignTools` / `CivilProductionReadAssistTools` | `civil.alignment.point_location` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 列出 Profile，可按 Alignment Handle 过滤。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilReadDesignTools` / `CivilProductionReadAssistTools` | `civil.profile.list` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 按 Handle 读取一个 Profile。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilReadDesignTools` / `CivilProductionReadAssistTools` | `civil.profile.get` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 查询 Profile 指定桩号的高程与瞬时坡度。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilReadDesignTools` / `CivilProductionReadAssistTools` | `civil.profile.elevation` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 列出 Profile 的 PVI 桩号、高程和进出坡度。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilReadDesignTools` / `CivilProductionReadAssistTools` | `civil.profile.pvi.list` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 列出 COGO Point。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilReadDesignTools` / `CivilProductionReadAssistTools` | `civil.cogo_point.list` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 按 Handle 读取一个 COGO Point。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilReadDesignTools` / `CivilProductionReadAssistTools` | `civil.cogo_point.get` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 只读列出 Pressure Pipe Network；TASK-005 不提供 Pressure 写接口。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilReadDesignTools` / `CivilProductionReadAssistTools` | `civil.pressure_network.list` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 只读读取 Pressure Pipe Network 及 PipeRun 摘要。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilReadDesignTools` / `CivilProductionReadAssistTools` | `civil.pressure_network.get` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 列出 FeatureLine，包含 Site、长度、高程和坡度摘要。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilReadDesignTools` / `CivilProductionReadAssistTools` | `civil.feature_line.list` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 按 Handle 深读一个 FeatureLine。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilReadDesignTools` / `CivilProductionReadAssistTools` | `civil.feature_line.get` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 读取 FeatureLine 点；point_type=all/pi/elevation，使用 limit 控制响应。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilReadDesignTools` / `CivilProductionReadAssistTools` | `civil.feature_line.points` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 只读诊断 FeatureLine 零长/近垂直段、点数和可选坡度阈值。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilReadDesignTools` / `CivilProductionReadAssistTools` | `civil.feature_line.diagnose` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 列出 Corridor，返回 baseline/region/out-of-date 摘要。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilReadDesignTools` / `CivilProductionReadAssistTools` | `civil.corridor.list` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 深读 Corridor；可返回 point/link/shape code。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilReadDesignTools` / `CivilProductionReadAssistTools` | `civil.corridor.get` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 读取 Corridor 的 Baseline/Region/Assembly 结构，带双层 limit。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilReadDesignTools` / `CivilProductionReadAssistTools` | `civil.corridor.baselines` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 只读诊断 Corridor 的 out-of-date、引用、Region gap/overlap、Assembly 与 processing 状态。 | 无（MCP 产品/高级入口） | `YangCAD.Civil3D/Tools/CivilReadDesignTools` / `CivilProductionReadAssistTools` | `civil.corridor.diagnose` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 检查独立 AcCoreConsole Batch Host 与 Worker DLL；不连接当前 acad.exe Gateway。 | 无（MCP 产品/高级入口） | `yangcad_mcp.batch`（独立 AcCoreConsole Batch Host） | `batch.status` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 独立进程批量审计 DWG。每个 DWG 使用单独 AcCoreConsole 进程；完全不占用当前 acad.exe Gateway。 | 无（MCP 产品/高级入口） | `yangcad_mcp.batch`（独立 AcCoreConsole Batch Host） | `batch.dwg.audit` | Read | 不适用 | 结构化 JSON/dict | PASS |
| 批量 Production Hygiene。默认 dry_run；real 只写 output_dir 新副本，首版拒绝原地覆盖 DWG。 | 无（MCP 产品/高级入口） | `yangcad_mcp.batch`（独立 AcCoreConsole Batch Host） | `batch.dwg.hygiene` | Write | 是 | 结构化 JSON/dict | PASS |

## 合同统计

- MCP stdio：**68** 个公开 Tool。
- AutoCAD live Gateway：**31** 个 Tool。
- Civil 3D live Gateway：**61** 个 Tool。
- Toolbox 可见业务功能：**2** 个；另有 Ribbon 生产功能 **比例转换**。三项正式 GUI 业务均有产品 MCP Tool，并共享 Business Service。

## 覆盖规则

- GUI 与 MCP 不互相调用；GUI Adapter 与 MCP Adapter 共同进入 Request / Business Service / Result。
- `execute_python`、任意命令执行、通用反射 API 只能作为高级能力，不能替代已有产品功能的专用 MCP Coverage。
- 新产品功能如果只有 GUI、只有“打开窗口”，或只有通用执行器，状态必须是 `PARTIAL` / `NOT_IMPLEMENTED`。
- 写操作必须遵守正确 Document/PID 绑定、CAD 主线程/DocumentLock/Transaction、失败回滚和明确 Undo 语义；批处理输出副本按独立作业语义处理。
- `project.revit.prepare` 与 `project.image.embed` 是当前两项可见 Toolbox 业务，因此必须同时通过 GUI 路径、MCP 产品路径、结构化结果读回和 GUI↔MCP 对账。

- `cad.scale.convert`：标准模式保持 1:1 几何不变并调整注释显示尺度；缩尺绘图模式按 old/new 缩放几何并恢复纸面表现。
