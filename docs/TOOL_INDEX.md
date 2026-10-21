> **2026-09-07 急用功能：图纸比例转换 V1（STATIC DEVELOPMENT）**：新增 Ribbon `比例转换` / 命令 `YGSCALE` / MCP `cad.scale.convert`。支持 `standard_1to1`（真实 1:1，几何不变、注释倍率 New/Old）与 `scaled_drawing`（非 1:1，几何倍率 Old/New、纸面注释恢复）两种模式。第一版对 Annotative/Xref/Underlay/Civil/AEC/Proxy 安全跳过；标准模式普通块因无法判定几何块/注释块而先跳过。当前 MCP additive contract 变为 stdio **68** / AutoCAD **31** / Civil 3D **61**。尚未 Windows Build/Runtime/Undo 实机证明。

> **2026-09-07 CADTool ↔ YangTools 业务对齐**：`project.revit.prepare` 与 `project.image.embed` 已补产品级 MCP Adapter，GUI/MCP 共用业务 Service/Request/Result；当前静态合同调整为 stdio **67** / AutoCAD live **30** / Civil 3D live **60**。Exchange 继续锁定 `yangcad-rvt-exchange/1.0`，`LWPOLYLINE vertices[].point + bulge` 不回退。Windows Build/Runtime/真实 11/11 E2E 尚需独立验收。

# Tool Index

## 2026-09-01 产品入口说明

本表列出的是 **MCP / shared backend 能力 contract**，不是 Toolbox GUI 菜单。

当前 Toolbox GUI 只显示：

- `project.revit.prepare` / Revit 图纸预处理；
- `project.image.embed` / 批量图片内嵌。

旧 CAD/Civil GUI 卡片已从 Toolbox 退休，但下列 MCP Tool 继续保留，因为 MCP 是 YangCAD 核心能力。当前 additive Contract 为 stdio 68 / AutoCAD 31 / Civil 61。

状态：`VERIFIED_2024` = 已在 AutoCAD/Civil 3D 2024 实机验证。

| Tool | Owner | R/W | dry_run | 状态 |
|---|---|---|---|---|
| project.revit.prepare | Project / RVT Exchange | W(output files) | ✓ | STATIC PASS / RUNTIME RETEST REQUIRED |
| project.image.embed | Project / Image Embed | W(output copy) | ✓ | STATIC PASS / OLE SMOKE REQUIRED |
| cad.scale.convert | AutoCAD Scale Converter | W | ✓（默认 true） | STATIC PASS / AUTOCAD 2024 TEST REQUIRED |
| cad.status | AutoCAD | R | - | VERIFIED_2024 |
| cad.document.info | AutoCAD | R | - | VERIFIED_2024 |
| cad.selection.read | AutoCAD | R | - | VERIFIED_2024 |
| cad.entity.get | AutoCAD | R | - | VERIFIED_2024 |
| cad.entity.query | AutoCAD | R | - | VERIFIED_2024 |
| cad.entity.update | AutoCAD | W | ✓ | VERIFIED_2024 |
| cad.entity.transform | AutoCAD | W | ✓ | VERIFIED_2024 |
| cad.entity.erase | AutoCAD | W | ✓ | VERIFIED_2024 |
| cad.layer.list | AutoCAD | R | - | VERIFIED_2024 |
| cad.layer.ensure | AutoCAD | W | ✓ | VERIFIED_2024 |
| cad.object.describe | AutoCAD Generic | R | - | VERIFIED_2024 |
| cad.property.get | AutoCAD Generic | R | - | VERIFIED_2024 |
| cad.property.set | AutoCAD Generic | W | ✓（默认 true） | VERIFIED_2024 |
| cad.api.describe | AutoCAD Generic | R | - | VERIFIED_2024 |
| cad.api.invoke | AutoCAD Generic | W | ✓（默认 true） | VERIFIED_2024 |
| cad.command.run | AutoCAD Generic | W | ✓（默认 true） | VERIFIED_2024 / EXPERIMENTAL |
| cad.extension.list | AutoCAD Generic | R | - | VERIFIED_2024 |
| cad.extension.invoke | AutoCAD Generic | W | ✓（默认 true） | VERIFIED_2024 / EXPERIMENTAL |
| cad.line.create | AutoCAD Productivity | W | ✓（默认 true） | VERIFIED_2024 |
| cad.polyline.create | AutoCAD Productivity | W | ✓（默认 true） | VERIFIED_2024 |
| cad.text.create | AutoCAD Productivity | W | ✓（默认 true） | VERIFIED_2024 |
| cad.block.list | AutoCAD Productivity | R | - | VERIFIED_2024 |
| cad.block.insert | AutoCAD Productivity | W | ✓（默认 true） | VERIFIED_2024 |
| cad.polyline.inspect | AutoCAD Production Hygiene | R | - | VERIFIED_2024 |
| cad.polyline.clean | AutoCAD Production Hygiene | W | ✓（默认 true） | VERIFIED_2024 |
| cad.layer.audit | AutoCAD Production Hygiene | R | - | VERIFIED_2024 |
| cad.layer.cleanup | AutoCAD Production Hygiene | W | ✓（默认 true） | VERIFIED_2024 |
| cad.drawing.audit | AutoCAD Production Hygiene | R | - | VERIFIED_2024 |
| civil.status | Civil3D | R | - | VERIFIED_2024 |
| civil.surface.list | Civil3D | R | - | VERIFIED_2024 |
| civil.alignment.list | Civil3D | R | - | VERIFIED_2024 |
| civil.alignment.station_offset | Civil3D | R | - | VERIFIED_2024 |
| civil.structure.list | Civil3D | R | - | VERIFIED_2024 |
| civil.structure.get | Civil3D | R | - | VERIFIED_2024 |
| civil.structure.update | Civil3D | W | ✓（默认 true） | VERIFIED_2024 |
| civil.pipe.list | Civil3D | R | - | VERIFIED_2024 |
| civil.pipe.get | Civil3D | R | - | VERIFIED_2024 |
| civil.pipe.update | Civil3D | W | ✓（默认 true） | VERIFIED_2024 |
| civil.surface.get | Civil3D Read | R | - | VERIFIED_2024 |
| civil.surface.elevation | Civil3D Read | R | - | VERIFIED_2024 |
| civil.surface.boundaries | Civil3D Read | R | - | VERIFIED_2024 |
| civil.alignment.point_location | Civil3D Read | R | - | VERIFIED_2024 |
| civil.profile.list | Civil3D Read | R | - | VERIFIED_2024 |
| civil.profile.get | Civil3D Read | R | - | VERIFIED_2024 |
| civil.profile.elevation | Civil3D Read | R | - | VERIFIED_2024 |
| civil.profile.pvi.list | Civil3D Read | R | - | VERIFIED_2024 |
| civil.cogo_point.list | Civil3D Read | R | - | VERIFIED_2024 |
| civil.cogo_point.get | Civil3D Read | R | - | VERIFIED_2024 |
| civil.pressure_network.list | Civil3D Read | R | - | VERIFIED_2024 |
| civil.pressure_network.get | Civil3D Read | R | - | VERIFIED_2024 |

| civil.feature_line.list | Civil3D Production Read | R | - | VERIFIED_2024 |
| civil.feature_line.get | Civil3D Production Read | R | - | VERIFIED_2024 |
| civil.feature_line.points | Civil3D Production Read | R | - | VERIFIED_2024 |
| civil.feature_line.diagnose | Civil3D Production Assist | R | - | VERIFIED_2024 |
| civil.corridor.list | Civil3D Production Read | R | - | VERIFIED_2024 |
| civil.corridor.get | Civil3D Production Read | R | - | VERIFIED_2024 |
| civil.corridor.baselines | Civil3D Production Read | R | - | VERIFIED_2024 |
| civil.corridor.diagnose | Civil3D Production Assist | R | - | VERIFIED_2024 |
| batch.status | MCP Local Batch | R | - | VERIFIED_2024 |
| batch.dwg.audit | MCP Local Batch | R | - | VERIFIED_2024 |
| batch.dwg.hygiene | MCP Local Batch | W(copy-only) | ✓ + allow_write | VERIFIED_2024 |
| extension.status | MCP Local Extension Workflow | R(local env) | - | VERIFIED_2024 |
| extension.build | MCP Local Extension Workflow | W(local workspace) | - | VERIFIED_2024 |
| extension.install | MCP Local Extension Workflow | W(local install) | allow_install | VERIFIED_2024 |
| extension.artifact.list | MCP Local Extension Workflow | R(local manifest) | - | VERIFIED_2024 |

## TASK-004 语义

- 范围仅为 **gravity pipe network** 的 `Structure` / `Pipe`，不把 Pressure Network 混入同一契约。
- `civil.structure.update` 可控制井顶、井底、Surface 自动贴合与 adjustment；设置 `sump_elevation` 时显式切到 `StructureControlSumpType.ByElevation`。
- 设置固定 `rim_elevation` 时，最终 `automatic_rim_surface_adjustment` 必须为 false，避免 Civil 规则重新覆盖用户目标。
- `civil.pipe.update` 第一版只改起终点 **Z 高程**，保持 XY 不变；支持中心高程或按 `InnerHeight/2` 换算的管内底高程，二者同端不得同时传。
- `apply_rules` 默认 false；启用时必须以返回的 after 状态为准，不假设规则不会二次调整。
- Data Shortcut/Vault reference object 明确拒绝写入。
- 高频业务优先强类型 Tool；没有专用接口时仍按 `object/property → api → command/extension` 降级。

## TASK-005 语义

- 本阶段新增 12 个 **只读** Tool，已在 Windows/Civil 3D 2024 实机全部验证通过。
- `surface.elevation` 使用 Civil `FindElevationAtXY`；Surface 范围外返回 `on_surface=false`。
- `surface.boundaries` 读取 `BoundariesDefinition`/`SurfaceOperationAddBoundary`/`SurfaceBoundary.Vertices`，并强制 `vertex_limit`。
- `alignment.point_location` 与既有 `alignment.station_offset` 构成双向坐标转换。
- Profile 提供列表、摘要、桩号高程/坡度、PVI 读取。
- COGO Point 提供编号、名称、坐标、高程、描述、点组/锁定状态。
- Pressure Network 本阶段只读，并新增 `AeccPressurePipesMgd.dll` 编译引用；禁止写 Pipe/Fitting/Appurtenance。


## TASK-006A 语义

- `cad.polyline.inspect` / `clean` 仅面向 2D Lightweight Polyline。
- `cad.polyline.clean` 对 bulge 弧段或非零 segment width 直接拒绝，避免 generic cleanup 改变几何/宽度语义。
- `remove_duplicates=true` 默认开启；`remove_collinear` 默认 false。
- `cad.layer.audit` 中 `modelspace_entity_count` 仅表示模型空间直接实体数量；真正能否删除以 `Database.Purge` 为准。
- `cad.layer.cleanup` 只处理 `Database.Purge` 返回的可删除 LayerTableRecord；Layer 0 / current / xref-dependent / locked 永远保护，Defpoints 默认保护。
- `cad.layer.cleanup` 复用删除权限开关，默认 dry_run。
- `cad.drawing.audit` 只读、有限扫描；主要定位零长 Line、空 DBText/MText、Polyline 零长段，并返回 type/layer 汇总。

## TASK-006B 语义

- 本轮 8 个新 Tool 全部 read-only；不新增 Civil 写 Tool。
- FeatureLine discovery 同时覆盖 siteless 与 Site 内对象；points 用 `point_type=all/pi/elevation`，所有大集合有 limit/truncated。
- `civil.feature_line.diagnose` 的 `max_abs_grade` 是用户显式阈值；未提供时不把坡度大小武断判坏。
- Corridor 深读覆盖 Baseline / Region / Assembly、point/link/shape codes。
- `civil.corridor.diagnose` 只报告 out-of-date/reference/processing/Region gap-overlap/Assembly 状态；禁止自动 `Rebuild()`。
- TASK-006B 8 个 Tool 已在 Civil 3D 2024 最小实机闭环通过，状态为 `VERIFIED_2024`。

- FeatureLine `point_count_mismatch` 仅表示 Civil API `PointsCount` 与 `GetPoints(AllPoints)` 的观察差异；不得仅凭该项认定 DWG 或 FeatureLine 损坏。


## TASK-006C 语义

- `batch.*` 是 Python MCP 本地 Tool，不进入 AutoCAD/Civil `/health`；006C contract = stdio 61 / live 28/58。
- `batch.status` 仅检查 AcCoreConsole、BatchWorker 与产品 runtime。
- `batch.dwg.audit` 每个 DWG 一个独立 AcCoreConsole 进程，永远 read-only。
- `batch.dwg.hygiene` 默认 dry_run；real 需 `allow_write=true`，只 SaveAs 到新的 output_dir，拒绝覆盖输入或已有文件。
- 输入 DWG before/after SHA 必须相同；Layer 删除只接受 `Database.Purge` 返回对象；bulge/width Polyline 只 skip 不修改。

- Batch real 输出优先保持源 `OriginalFileVersion`；检测到 ProxyEntity 时默认拒绝 real save，除非显式 `allow_proxy_save=true`。
- TASK-006C hygiene 的 Polyline cleanup 仅处理 2D Lightweight `Polyline`；不宣称处理 `Polyline2d/Polyline3d`。bulge 或任意非零 segment width 均 skip。
- Batch v1 是“每 DWG 独立结果”，不是跨多 DWG 全局事务；整批只对确定性的输出路径冲突做启动前 preflight。
- real `folder` 模式要求 output_dir 位于输入 folder 树之外；避免下一次 recursive run 把上次输出重新吃入。

- TASK-006C 实机发现过一次 `_N` QUIT 应答导致源 DWG 被退出流程回写；最终 `_Y` 修复后重新验证输入 SHA 不变。input SHA guard 是事后完整性门禁，不应描述成预写入保护。


## TASK-007 语义

- `extension.*` 4 个 workflow Tool 仅在 MCP stdio；live `/health` 继续 28/58。
- `extension.build` 生成 fixed wrapper + versioned operation/assembly；不接受任意 project/shell。
- `extension.install` 显式 `allow_install=true`，禁止覆盖既有版本。
- managed DLL 必须匹配 manifest DLL SHA、target year/product、current Host SHA、runtime Descriptor capability。
- `cad.extension.list/invoke` Tool 名不变；权限关闭时 list 只列文件、不加载程序集。
- write `cad.extension.invoke(dry_run=true)` = Host Transaction Abort；real = single Host Transaction；异常自动 rollback。
- `changes[]` 是 extension-reported ledger，不是独立 Host snapshot。
- `RequiresErase=true` 仍受 AllowErase 独立权限。


## RELEASE-002 compatibility status

Tool 名与 contract 在 RELEASE-002 不变化。  
2021–2024 build compatibility 已证明；runtime compatibility 仍按年份逐步验证：

- 2021：plain AutoCAD/Batch runtime READY；
- 2022/2023：AutoCAD/Civil/Batch runtime READY；
- 2024：既有 VERIFIED_2024，Civil compatibility helper 需 regression smoke；
- 2025–2027：NOT_INSTALLED。
