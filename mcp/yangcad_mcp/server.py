from __future__ import annotations
from typing import Any, Literal
from mcp.server import MCPServer
from .gateway import call
from .batch import status as batch_status_local, audit as batch_audit_local, hygiene as batch_hygiene_local
from .extensions import status as extension_status_local, build as extension_build_local, install as extension_install_local, artifact_list as extension_artifact_list_local

mcp = MCPServer("YangCAD Agent")

# TASK-007 Agent Extension Workflow (MCP-local build/install) -----------------
@mcp.tool(name="extension.status")
def extension_status(year: int = 2024, target_product: Literal["AutoCAD", "Civil3D"] = "AutoCAD") -> dict[str, Any]:
    """检查版本化 Extension 的本地编译环境；不加载 DLL、不访问 live Gateway。"""
    return extension_status_local(year=year, target_product=target_product)


@mcp.tool(name="extension.build")
def extension_build(
    name: str,
    version: str,
    description: str,
    execute_body: str,
    year: int = 2024,
    target_product: Literal["AutoCAD", "Civil3D"] = "AutoCAD",
    is_write: bool = True,
    requires_erase: bool = False,
    timeout_seconds: int = 120,
) -> dict[str, Any]:
    """从受控 Execute body 生成并编译版本化 Extension。写扩展必须 RecordChange；不直接加载进 CAD。"""
    return extension_build_local(
        name=name, version=version, description=description, execute_body=execute_body,
        year=year, target_product=target_product, is_write=is_write, requires_erase=requires_erase,
        timeout_seconds=timeout_seconds,
    )


@mcp.tool(name="extension.install")
def extension_install(build_id: str, allow_install: bool = False) -> dict[str, Any]:
    """把已编译且 SHA 校验通过的版本化 DLL 安装到 Extension 目录；必须显式 allow_install=true。"""
    return extension_install_local(build_id=build_id, allow_install=allow_install)


@mcp.tool(name="extension.artifact.list")
def extension_artifact_list(limit: int = 200) -> dict[str, Any]:
    """只读列出 Extension workspace/installed manifest 与 SHA 状态；不会加载 DLL。"""
    return extension_artifact_list_local(limit=limit)


# Strong typed baseline -------------------------------------------------------
@mcp.tool(name="cad.status")
def cad_status() -> dict[str, Any]:
    """读取 YangCAD / AutoCAD / Civil 3D 当前运行状态。"""
    return call("cad.status")

@mcp.tool(name="cad.document.info")
def cad_document_info() -> dict[str, Any]:
    """读取当前 DWG 基础信息。"""
    return call("cad.document.info")

@mcp.tool(name="cad.selection.read")
def cad_selection_read() -> dict[str, Any]:
    """读取当前 CAD PickFirst 选择集。"""
    return call("cad.selection.read")

@mcp.tool(name="cad.entity.get")
def cad_entity_get(handle: str) -> dict[str, Any]:
    """按 DWG Handle 读取实体。"""
    return call("cad.entity.get", {"handle": handle})

@mcp.tool(name="cad.entity.query")
def cad_entity_query(dxf_type: str | None = None, layer: str | None = None, text_contains: str | None = None, limit: int = 200) -> dict[str, Any]:
    """查询模型空间实体。"""
    return call("cad.entity.query", {"dxf_type": dxf_type, "layer": layer, "text_contains": text_contains, "limit": limit})

@mcp.tool(name="cad.entity.update")
def cad_entity_update(handle: str, layer: str | None = None, color_index: int | None = None, linetype: str | None = None, text: str | None = None, dry_run: bool = False) -> dict[str, Any]:
    """修改图层/颜色/线型/文字；写入前先读取当前 DWG 建立绑定，复杂任务建议先 dry_run。"""
    return call("cad.entity.update", {"handle": handle, "layer": layer, "color_index": color_index, "linetype": linetype, "text": text, "dry_run": dry_run})

@mcp.tool(name="cad.entity.transform")
def cad_entity_transform(handle: str, operation: Literal["move", "rotate", "scale"], vector: list[float] | None = None, base_point: list[float] | None = None, angle_degrees: float | None = None, factor: float | None = None, dry_run: bool = False) -> dict[str, Any]:
    """移动/旋转/缩放实体；写入前先读取当前 DWG 建立绑定。"""
    return call("cad.entity.transform", {"handle": handle, "operation": operation, "vector": vector, "base_point": base_point, "angle_degrees": angle_degrees, "factor": factor, "dry_run": dry_run})

@mcp.tool(name="cad.entity.erase")
def cad_entity_erase(handle: str, dry_run: bool = True) -> dict[str, Any]:
    """删除实体；控制中心默认禁用，Tool 默认 dry_run，且要求先读取当前 DWG 建立绑定。"""
    return call("cad.entity.erase", {"handle": handle, "dry_run": dry_run})

@mcp.tool(name="cad.layer.list")
def cad_layer_list() -> dict[str, Any]:
    """列出全部图层。"""
    return call("cad.layer.list")

@mcp.tool(name="cad.layer.ensure")
def cad_layer_ensure(name: str, color_index: int | None = None, dry_run: bool = False) -> dict[str, Any]:
    """确保图层存在；写入前先读取当前 DWG 建立绑定。"""
    return call("cad.layer.ensure", {"name": name, "color_index": color_index, "dry_run": dry_run})

# Product-level project tools ------------------------------------------------
@mcp.tool(name="project.revit.prepare")
def project_revit_prepare(
    output_folder: str | None = None,
    output_suffix: str = "_RVT",
    scan_blocks: bool = True,
    include_paperspace: bool = False,
    normalize_geometry: bool = True,
    coordinate_origin_mode: str = "local",
    dry_run: bool = True,
) -> dict[str, Any]:
    """Revit 图纸预处理。GUI/MCP 共用 RevitExchangeService；默认 dry_run，real 输出 RVT DWG/MAP/REPORT。"""
    return call("project.revit.prepare", {
        "output_folder": output_folder or "",
        "output_suffix": output_suffix,
        "scan_blocks": scan_blocks,
        "include_paperspace": include_paperspace,
        "normalize_geometry": normalize_geometry,
        "coordinate_origin_mode": coordinate_origin_mode,
        "dry_run": dry_run,
    })


@mcp.tool(name="project.image.embed")
def project_image_embed(
    input_path: str,
    output_folder: str | None = None,
    include_subfolders: bool = True,
    show_worker: bool = False,
    require_all_images: bool = True,
    command_timeout_seconds: int = 90,
    dry_run: bool = True,
) -> dict[str, Any]:
    """批量图片内嵌。GUI/MCP 共用 ImageEmbedBatchService；默认 dry_run，real 启动独立 AutoCAD worker。"""
    return call("project.image.embed", {
        "input_path": input_path,
        "output_folder": output_folder or "",
        "include_subfolders": include_subfolders,
        "show_worker": show_worker,
        "require_all_images": require_all_images,
        "command_timeout_seconds": command_timeout_seconds,
        "dry_run": dry_run,
    })


@mcp.tool(name="cad.scale.convert")
def cad_scale_convert(
    handles: list[str],
    old_scale: float,
    new_scale: float,
    mode: Literal["standard_1to1", "scaled_drawing"] = "standard_1to1",
    base_point: list[float] | None = None,
    preserve_text_paper_size: bool = True,
    preserve_dimension_paper_size: bool = True,
    preserve_leader_paper_size: bool = True,
    preserve_hatch_paper_scale: bool = True,
    preserve_linetype_paper_scale: bool = True,
    dry_run: bool = True,
) -> dict[str, Any]:
    """图纸比例转换。standard_1to1 不改真实几何；scaled_drawing 会按 old/new 缩放几何。默认 dry_run。"""
    return call("cad.scale.convert", {
        "handles": handles,
        "old_scale": old_scale,
        "new_scale": new_scale,
        "mode": mode,
        "base_point": base_point or [0.0, 0.0, 0.0],
        "preserve_text_paper_size": preserve_text_paper_size,
        "preserve_dimension_paper_size": preserve_dimension_paper_size,
        "preserve_leader_paper_size": preserve_leader_paper_size,
        "preserve_hatch_paper_scale": preserve_hatch_paper_scale,
        "preserve_linetype_paper_scale": preserve_linetype_paper_scale,
        "dry_run": dry_run,
    })


# TASK-003 CAD productivity ---------------------------------------------------
@mcp.tool(name="cad.line.create")
def cad_line_create(start: list[float], end: list[float], layer: str | None = None, color_index: int | None = None, dry_run: bool = True) -> dict[str, Any]:
    """创建 Line。默认 dry_run；real 前先读取当前 DWG 建立绑定。"""
    return call("cad.line.create", {"start": start, "end": end, "layer": layer, "color_index": color_index, "dry_run": dry_run})

@mcp.tool(name="cad.polyline.create")
def cad_polyline_create(vertices: list[list[float]], closed: bool = False, layer: str | None = None, color_index: int | None = None, dry_run: bool = True) -> dict[str, Any]:
    """创建 2D lightweight Polyline；顶点必须共面同 Z。默认 dry_run。"""
    return call("cad.polyline.create", {"vertices": vertices, "closed": closed, "layer": layer, "color_index": color_index, "dry_run": dry_run})

@mcp.tool(name="cad.text.create")
def cad_text_create(text: str, position: list[float], height: float = 2.5, kind: Literal["dbtext", "mtext"] = "dbtext", width: float | None = None, layer: str | None = None, color_index: int | None = None, dry_run: bool = True) -> dict[str, Any]:
    """创建 DBText/MText。默认 dry_run。"""
    return call("cad.text.create", {"text": text, "position": position, "height": height, "kind": kind, "width": width, "layer": layer, "color_index": color_index, "dry_run": dry_run})

@mcp.tool(name="cad.block.list")
def cad_block_list(include_anonymous: bool = False, include_xrefs: bool = False, limit: int = 500) -> dict[str, Any]:
    """列出可插入的块定义和属性定义数量。"""
    return call("cad.block.list", {"include_anonymous": include_anonymous, "include_xrefs": include_xrefs, "limit": limit})

@mcp.tool(name="cad.block.insert")
def cad_block_insert(name: str, position: list[float], scale: float = 1.0, rotation_degrees: float = 0.0, layer: str | None = None, attributes: dict[str, Any] | None = None, dry_run: bool = True) -> dict[str, Any]:
    """插入现有块定义并按 Tag 写非 Constant 属性。默认 dry_run。"""
    return call("cad.block.insert", {"name": name, "position": position, "scale": scale, "rotation_degrees": rotation_degrees, "layer": layer, "attributes": attributes or {}, "dry_run": dry_run})


# TASK-006A Production Hygiene ----------------------------------------------
@mcp.tool(name="cad.polyline.inspect")
def cad_polyline_inspect(handle: str, tolerance: float = 1e-6) -> dict[str, Any]:
    """检查 2D Lightweight Polyline 的重复点、零长段、共线冗余点及 cleanup 限制。"""
    return call("cad.polyline.inspect", {"handle": handle, "tolerance": tolerance})

@mcp.tool(name="cad.polyline.clean")
def cad_polyline_clean(
    handle: str,
    remove_duplicates: bool = True,
    remove_collinear: bool = False,
    tolerance: float = 1e-6,
    dry_run: bool = True,
) -> dict[str, Any]:
    """安全清理直线型 2D Polyline。默认 dry_run；弧段/逐段变宽 Polyline 会明确拒绝。"""
    return call("cad.polyline.clean", {
        "handle": handle,
        "remove_duplicates": remove_duplicates,
        "remove_collinear": remove_collinear,
        "tolerance": tolerance,
        "dry_run": dry_run,
    })

@mcp.tool(name="cad.layer.audit")
def cad_layer_audit() -> dict[str, Any]:
    """审计图层状态、模型空间实体数量和 AutoCAD Purge 可清理性。"""
    return call("cad.layer.audit")

@mcp.tool(name="cad.layer.cleanup")
def cad_layer_cleanup(
    names: list[str] | None = None,
    include_defpoints: bool = False,
    dry_run: bool = True,
) -> dict[str, Any]:
    """仅删除 AutoCAD Database.Purge 判定为可安全清理的未使用图层。默认 dry_run，且需要删除权限。"""
    return call("cad.layer.cleanup", {
        "names": names or [],
        "include_defpoints": include_defpoints,
        "dry_run": dry_run,
    })

@mcp.tool(name="cad.drawing.audit")
def cad_drawing_audit(
    entity_limit: int = 100000,
    issue_limit: int = 1000,
    tolerance: float = 1e-6,
) -> dict[str, Any]:
    """只读扫描模型空间，汇总实体/图层/块以及零长线、空文字、Polyline 零长段等常见问题。"""
    return call("cad.drawing.audit", {
        "entity_limit": entity_limit,
        "issue_limit": issue_limit,
        "tolerance": tolerance,
    })


# TASK-002 generic fallback layer -------------------------------------------
@mcp.tool(name="cad.object.describe")
def cad_object_describe(handle: str, include_values: bool = True, include_methods: bool = True, max_members: int = 120) -> dict[str, Any]:
    """查看任意 Autodesk DBObject 的运行时类型、公共属性以及通用层可调用的方法。"""
    return call("cad.object.describe", {"handle": handle, "include_values": include_values, "include_methods": include_methods, "max_members": max_members})

@mcp.tool(name="cad.property.get")
def cad_property_get(handle: str, property: str) -> dict[str, Any]:
    """读取对象的 JSON-safe 公共属性；碰到没有专用 Tool 的对象时优先先 describe/get。"""
    return call("cad.property.get", {"handle": handle, "property": property})

@mcp.tool(name="cad.property.set")
def cad_property_set(handle: str, property: str, value: Any, dry_run: bool = True) -> dict[str, Any]:
    """受控通用属性写入。默认 dry_run；仅公共且可安全转换的 Autodesk setter 可写。"""
    return call("cad.property.set", {"handle": handle, "property": property, "value": value, "dry_run": dry_run})

@mcp.tool(name="cad.api.describe")
def cad_api_describe(handle: str, method: str | None = None, max_members: int = 120) -> dict[str, Any]:
    """列出对象上的公共 Autodesk 方法、精确 signature 与是否允许通用调用。"""
    return call("cad.api.describe", {"handle": handle, "method": method, "max_members": max_members})

@mcp.tool(name="cad.api.invoke")
def cad_api_invoke(handle: str, method: str, arguments: list[Any] | None = None, signature: str | None = None, dry_run: bool = True) -> dict[str, Any]:
    """受控调用公共 Autodesk API。默认 dry_run；不支持 ref/out、危险基础设施方法和复杂未知参数。"""
    return call("cad.api.invoke", {"handle": handle, "method": method, "arguments": arguments or [], "signature": signature, "dry_run": dry_run})

@mcp.tool(name="cad.command.run")
def cad_command_run(command: str, arguments: list[Any] | None = None, dry_run: bool = True) -> dict[str, Any]:
    """实验性同步 AutoCAD 命令 fallback。默认 dry_run 且控制中心默认禁用；禁止交互 PauseToken/脚本拼接。"""
    return call("cad.command.run", {"command": command, "arguments": arguments or [], "dry_run": dry_run})

@mcp.tool(name="cad.extension.list")
def cad_extension_list() -> dict[str, Any]:
    """列出 %LOCALAPPDATA%/YangCADAgent/Extensions 中可发现的版本化扩展 operation。"""
    return call("cad.extension.list")

@mcp.tool(name="cad.extension.invoke")
def cad_extension_invoke(operation: str, arguments: dict[str, Any] | None = None, dry_run: bool = True) -> dict[str, Any]:
    """调用动态扩展 operation。默认 dry_run 且控制中心默认禁用；用于专用 Tool/通用 API 都覆盖不到的场景。"""
    return call("cad.extension.invoke", {"operation": operation, "arguments": arguments or {}, "dry_run": dry_run})

# Civil baseline -------------------------------------------------------------
@mcp.tool(name="civil.status")
def civil_status() -> dict[str, Any]:
    """读取 Civil 3D API 状态。"""
    return call("civil.status")

@mcp.tool(name="civil.surface.list")
def civil_surface_list() -> dict[str, Any]:
    """列出 Surface。"""
    return call("civil.surface.list")

@mcp.tool(name="civil.alignment.list")
def civil_alignment_list() -> dict[str, Any]:
    """列出 Alignment。"""
    return call("civil.alignment.list")

@mcp.tool(name="civil.alignment.station_offset")
def civil_alignment_station_offset(handle: str, x: float, y: float) -> dict[str, Any]:
    """Alignment XY → 桩号/偏距。"""
    return call("civil.alignment.station_offset", {"handle": handle, "x": x, "y": y})


# TASK-004 Civil gravity Pipe / Structure ------------------------------------
@mcp.tool(name="civil.structure.list")
def civil_structure_list(network: str | None = None, limit: int = 1000) -> dict[str, Any]:
    """列出 gravity pipe network Structure/检查井，含井顶、井底、自动贴合和连接管道。"""
    return call("civil.structure.list", {"network": network, "limit": limit})

@mcp.tool(name="civil.structure.get")
def civil_structure_get(handle: str) -> dict[str, Any]:
    """按 Handle 读取一个 gravity Structure/检查井。"""
    return call("civil.structure.get", {"handle": handle})

@mcp.tool(name="civil.structure.update")
def civil_structure_update(
    handle: str,
    rim_elevation: float | None = None,
    sump_elevation: float | None = None,
    automatic_rim_surface_adjustment: bool | None = None,
    surface_adjustment_value: float | None = None,
    description: str | None = None,
    apply_rules: bool = False,
    dry_run: bool = True,
) -> dict[str, Any]:
    """修改 gravity Structure。默认 dry_run；锁定井顶时应同时关闭 automatic_rim_surface_adjustment。"""
    return call("civil.structure.update", {
        "handle": handle,
        "rim_elevation": rim_elevation,
        "sump_elevation": sump_elevation,
        "automatic_rim_surface_adjustment": automatic_rim_surface_adjustment,
        "surface_adjustment_value": surface_adjustment_value,
        "description": description,
        "apply_rules": apply_rules,
        "dry_run": dry_run,
    })

@mcp.tool(name="civil.pipe.list")
def civil_pipe_list(network: str | None = None, limit: int = 1000) -> dict[str, Any]:
    """列出 gravity Pipe，含端点中心高程、管内底高程、尺寸、坡度和连接井。"""
    return call("civil.pipe.list", {"network": network, "limit": limit})

@mcp.tool(name="civil.pipe.get")
def civil_pipe_get(handle: str) -> dict[str, Any]:
    """按 Handle 读取一个 gravity Pipe。"""
    return call("civil.pipe.get", {"handle": handle})

@mcp.tool(name="civil.pipe.update")
def civil_pipe_update(
    handle: str,
    start_center_elevation: float | None = None,
    end_center_elevation: float | None = None,
    start_invert_elevation: float | None = None,
    end_invert_elevation: float | None = None,
    description: str | None = None,
    apply_rules: bool = False,
    dry_run: bool = True,
) -> dict[str, Any]:
    """修改 gravity Pipe 起终点中心/管内底高程，XY 保持不变。默认 dry_run。"""
    return call("civil.pipe.update", {
        "handle": handle,
        "start_center_elevation": start_center_elevation,
        "end_center_elevation": end_center_elevation,
        "start_invert_elevation": start_invert_elevation,
        "end_invert_elevation": end_invert_elevation,
        "description": description,
        "apply_rules": apply_rules,
        "dry_run": dry_run,
    })


# TASK-005 Civil Read / Design -------------------------------------------------
@mcp.tool(name="civil.surface.get")
def civil_surface_get(handle: str) -> dict[str, Any]:
    """按 Handle 读取 Surface 基本属性。"""
    return call("civil.surface.get", {"handle": handle})

@mcp.tool(name="civil.surface.elevation")
def civil_surface_elevation(handle: str, x: float, y: float) -> dict[str, Any]:
    """查询 Surface 指定 XY 的高程；范围外返回 on_surface=false。"""
    return call("civil.surface.elevation", {"handle": handle, "x": x, "y": y})

@mcp.tool(name="civil.surface.boundaries")
def civil_surface_boundaries(handle: str, vertex_limit: int = 5000) -> dict[str, Any]:
    """读取 Surface 边界定义和离散顶点，使用 vertex_limit 防止超大响应。"""
    return call("civil.surface.boundaries", {"handle": handle, "vertex_limit": vertex_limit})

@mcp.tool(name="civil.alignment.point_location")
def civil_alignment_point_location(handle: str, station: float, offset: float = 0.0) -> dict[str, Any]:
    """Alignment 桩号/偏距 → XY。"""
    return call("civil.alignment.point_location", {"handle": handle, "station": station, "offset": offset})

@mcp.tool(name="civil.profile.list")
def civil_profile_list(alignment_handle: str | None = None, limit: int = 1000) -> dict[str, Any]:
    """列出 Profile，可按 Alignment Handle 过滤。"""
    return call("civil.profile.list", {"alignment_handle": alignment_handle, "limit": limit})

@mcp.tool(name="civil.profile.get")
def civil_profile_get(handle: str) -> dict[str, Any]:
    """按 Handle 读取一个 Profile。"""
    return call("civil.profile.get", {"handle": handle})

@mcp.tool(name="civil.profile.elevation")
def civil_profile_elevation(handle: str, station: float) -> dict[str, Any]:
    """查询 Profile 指定桩号的高程与瞬时坡度。"""
    return call("civil.profile.elevation", {"handle": handle, "station": station})

@mcp.tool(name="civil.profile.pvi.list")
def civil_profile_pvi_list(handle: str, limit: int = 1000) -> dict[str, Any]:
    """列出 Profile 的 PVI 桩号、高程和进出坡度。"""
    return call("civil.profile.pvi.list", {"handle": handle, "limit": limit})

@mcp.tool(name="civil.cogo_point.list")
def civil_cogo_point_list(limit: int = 1000) -> dict[str, Any]:
    """列出 COGO Point。"""
    return call("civil.cogo_point.list", {"limit": limit})

@mcp.tool(name="civil.cogo_point.get")
def civil_cogo_point_get(handle: str) -> dict[str, Any]:
    """按 Handle 读取一个 COGO Point。"""
    return call("civil.cogo_point.get", {"handle": handle})

@mcp.tool(name="civil.pressure_network.list")
def civil_pressure_network_list(limit: int = 100) -> dict[str, Any]:
    """只读列出 Pressure Pipe Network；TASK-005 不提供 Pressure 写接口。"""
    return call("civil.pressure_network.list", {"limit": limit})

@mcp.tool(name="civil.pressure_network.get")
def civil_pressure_network_get(handle: str) -> dict[str, Any]:
    """只读读取 Pressure Pipe Network 及 PipeRun 摘要。"""
    return call("civil.pressure_network.get", {"handle": handle})


# TASK-006B Civil Production Read / Assist -------------------------------------
@mcp.tool(name="civil.feature_line.list")
def civil_feature_line_list(limit: int = 1000) -> dict[str, Any]:
    """列出 FeatureLine，包含 Site、长度、高程和坡度摘要。"""
    return call("civil.feature_line.list", {"limit": limit})

@mcp.tool(name="civil.feature_line.get")
def civil_feature_line_get(handle: str) -> dict[str, Any]:
    """按 Handle 深读一个 FeatureLine。"""
    return call("civil.feature_line.get", {"handle": handle})

@mcp.tool(name="civil.feature_line.points")
def civil_feature_line_points(handle: str, point_type: str = "all", limit: int = 5000) -> dict[str, Any]:
    """读取 FeatureLine 点；point_type=all/pi/elevation，使用 limit 控制响应。"""
    return call("civil.feature_line.points", {"handle": handle, "point_type": point_type, "limit": limit})

@mcp.tool(name="civil.feature_line.diagnose")
def civil_feature_line_diagnose(
    handle: str,
    tolerance: float = 1e-6,
    issue_limit: int = 20,
    max_abs_grade: float | None = None,
) -> dict[str, Any]:
    """只读诊断 FeatureLine 零长/近垂直段、点数和可选坡度阈值。"""
    return call("civil.feature_line.diagnose", {
        "handle": handle,
        "tolerance": tolerance,
        "issue_limit": issue_limit,
        "max_abs_grade": max_abs_grade,
    })

@mcp.tool(name="civil.corridor.list")
def civil_corridor_list(limit: int = 200) -> dict[str, Any]:
    """列出 Corridor，返回 baseline/region/out-of-date 摘要。"""
    return call("civil.corridor.list", {"limit": limit})

@mcp.tool(name="civil.corridor.get")
def civil_corridor_get(handle: str, include_codes: bool = True, code_limit: int = 500) -> dict[str, Any]:
    """深读 Corridor；可返回 point/link/shape code。"""
    return call("civil.corridor.get", {"handle": handle, "include_codes": include_codes, "code_limit": code_limit})

@mcp.tool(name="civil.corridor.baselines")
def civil_corridor_baselines(
    handle: str,
    baseline_limit: int = 100,
    region_limit_per_baseline: int = 1000,
) -> dict[str, Any]:
    """读取 Corridor 的 Baseline/Region/Assembly 结构，带双层 limit。"""
    return call("civil.corridor.baselines", {
        "handle": handle,
        "baseline_limit": baseline_limit,
        "region_limit_per_baseline": region_limit_per_baseline,
    })

@mcp.tool(name="civil.corridor.diagnose")
def civil_corridor_diagnose(handle: str, station_tolerance: float = 1e-6, issue_limit: int = 500) -> dict[str, Any]:
    """只读诊断 Corridor 的 out-of-date、引用、Region gap/overlap、Assembly 与 processing 状态。"""
    return call("civil.corridor.diagnose", {
        "handle": handle,
        "station_tolerance": station_tolerance,
        "issue_limit": issue_limit,
    })



# TASK-006C Batch DWG — MCP-local independent process host --------------------
@mcp.tool(name="batch.status")
def batch_status(year: int = 2024, product: str = "AutoCAD") -> dict[str, Any]:
    """检查独立 AcCoreConsole Batch Host 与 Worker DLL；不连接当前 acad.exe Gateway。"""
    return batch_status_local(year=year, product=product)


@mcp.tool(name="batch.dwg.audit")
def batch_dwg_audit(
    paths: list[str] | None = None,
    folder: str | None = None,
    recursive: bool = False,
    file_limit: int = 50,
    year: int = 2024,
    product: str = "AutoCAD",
    entity_limit: int = 200000,
    issue_limit: int = 20,
    tolerance: float = 1e-6,
    timeout_seconds: int = 180,
    continue_on_error: bool = True,
) -> dict[str, Any]:
    """独立进程批量审计 DWG。每个 DWG 使用单独 AcCoreConsole 进程；完全不占用当前 acad.exe Gateway。"""
    return batch_audit_local(
        paths=paths, folder=folder, recursive=recursive, file_limit=file_limit, year=year, product=product,
        entity_limit=entity_limit, issue_limit=issue_limit, tolerance=tolerance,
        timeout_seconds=timeout_seconds, continue_on_error=continue_on_error,
    )


@mcp.tool(name="batch.dwg.hygiene")
def batch_dwg_hygiene(
    paths: list[str] | None = None,
    folder: str | None = None,
    recursive: bool = False,
    file_limit: int = 50,
    year: int = 2024,
    product: str = "AutoCAD",
    cleanup_layers: bool = True,
    cleanup_polylines: bool = True,
    remove_collinear: bool = False,
    protect_defpoints: bool = True,
    allow_proxy_save: bool = False,
    tolerance: float = 1e-6,
    entity_limit: int = 200000,
    dry_run: bool = True,
    allow_write: bool = False,
    output_dir: str | None = None,
    timeout_seconds: int = 180,
    continue_on_error: bool = True,
) -> dict[str, Any]:
    """批量 Production Hygiene。默认 dry_run；real 只写 output_dir 新副本，首版拒绝原地覆盖 DWG。"""
    return batch_hygiene_local(
        paths=paths, folder=folder, recursive=recursive, file_limit=file_limit, year=year, product=product,
        cleanup_layers=cleanup_layers, cleanup_polylines=cleanup_polylines,
        remove_collinear=remove_collinear, protect_defpoints=protect_defpoints, allow_proxy_save=allow_proxy_save,
        tolerance=tolerance, entity_limit=entity_limit, dry_run=dry_run, allow_write=allow_write, output_dir=output_dir,
        timeout_seconds=timeout_seconds, continue_on_error=continue_on_error,
    )


def main() -> None:
    mcp.run(transport="stdio")


if __name__ == "__main__":
    main()
