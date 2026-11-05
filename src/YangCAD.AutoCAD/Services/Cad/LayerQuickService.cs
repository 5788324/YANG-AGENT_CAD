using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Services.Cad
{
    /// <summary>
    /// One business implementation for UI commands and MCP. Layer state is stored in the
    /// current DWG, including host-side XREF|layer overrides; xref source files are not opened.
    /// </summary>
    public static class LayerQuickService
    {
        public sealed class LayerInfo
        {
            public string name { get; set; } = "";
            public bool off { get; set; }
            public bool frozen { get; set; }
            public bool locked { get; set; }
            public bool xref_dependent { get; set; }
            public bool current { get; set; }
        }
        public sealed class Result
        {
            public bool success { get; set; }
            public bool dry_run { get; set; }
            public string operation { get; set; } = "";
            public string source_layer { get; set; } = "";
            public string target_layer { get; set; } = "";
            public int moved { get; set; }
            public int skipped { get; set; }
            public bool removed_source_layer { get; set; }
            public bool off { get; set; }
            public bool frozen { get; set; }
            public bool locked { get; set; }
            public string message { get; set; } = "";
            public string undo_semantics { get; set; } = "single_transaction_expected";
        }

        public static LayerInfo[] List(CadExecutionContext ctx)
        {
            var lt = (LayerTable)ctx.Transaction.GetObject(ctx.Database.LayerTableId, OpenMode.ForRead);
            return lt.Cast<ObjectId>().Select(id =>
            {
                var layer = (LayerTableRecord)ctx.Transaction.GetObject(id, OpenMode.ForRead);
                return new LayerInfo
                {
                    name = layer.Name, off = layer.IsOff, frozen = layer.IsFrozen,
                    locked = layer.IsLocked, xref_dependent = layer.IsDependent,
                    current = id == ctx.Database.Clayer
                };
            }).OrderBy(x => x.name, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public static Result State(CadExecutionContext ctx, string layerName, string kind, bool value, bool dryRun)
        {
            var layer = OpenLayer(ctx, layerName);
            var operation = (kind ?? "").Trim().ToLowerInvariant();
            if (operation != "off" && operation != "freeze" && operation != "lock")
                throw new ArgumentException("图层状态只能是 off / freeze / lock。");
            if (layer.ObjectId == ctx.Database.Clayer && value && operation != "lock")
                throw new InvalidOperationException("不能隐藏或冻结当前绘图层。请先切换当前层，或操作其他图层。");
            // Xref-dependent layers live in this host drawing. State overrides are permitted,
            // unlike editing or merging the referenced entities themselves.
            var before = operation == "off" ? layer.IsOff : operation == "freeze" ? layer.IsFrozen : layer.IsLocked;
            if (!dryRun && before != value)
            {
                layer.UpgradeOpen();
                if (operation == "off") layer.IsOff = value;
                else if (operation == "freeze") layer.IsFrozen = value;
                else layer.IsLocked = value;
            }
            return new Result
            {
                success = true, dry_run = dryRun, operation = operation,
                source_layer = layer.Name, off = operation == "off" ? value : layer.IsOff,
                frozen = operation == "freeze" ? value : layer.IsFrozen,
                locked = operation == "lock" ? value : layer.IsLocked,
                message = before == value ? "图层已经处于所需状态，无须修改。" :
                    (dryRun ? "预览：将修改当前 DWG 的图层状态。" : "已修改当前 DWG 的图层状态。")
            };
        }

        public static Result Merge(CadExecutionContext ctx, string sourceName, string targetName, bool dryRun)
        {
            var source = OpenLayer(ctx, sourceName);
            var target = OpenLayer(ctx, targetName);
            if (source.ObjectId == target.ObjectId) throw new ArgumentException("来源图层和目标图层不能相同。");
            if (source.IsDependent || target.IsDependent)
                throw new InvalidOperationException("外部参照依赖图层不可合并；可在图层工具中关闭、冻结或锁定它。");
            if (string.Equals(source.Name, "0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source.Name, "Defpoints", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("不允许合并来源图层 0 或 Defpoints。");
            if (target.IsFrozen || target.IsOff)
                throw new InvalidOperationException("目标图层已关闭或冻结，请先显示并解冻目标图层。");

            var tr = ctx.Transaction;
            var bt = (BlockTable)tr.GetObject(ctx.Database.BlockTableId, OpenMode.ForRead);
            var candidates = new List<ObjectId>();
            var skipped = 0;
            foreach (ObjectId blockId in bt)
            {
                var record = (BlockTableRecord)tr.GetObject(blockId, OpenMode.ForRead);
                // Never edit a referenced DWG's definition or xref overlay. Normal blocks (and
                // ordinary dynamic/anonymous definitions owned by this DWG) are included.
                if (record.IsFromExternalReference || record.IsFromOverlayReference) continue;
                foreach (ObjectId entityId in record)
                {
                    if (!(tr.GetObject(entityId, OpenMode.ForRead, false) is Entity e) || e.IsErased) continue;
                    if (e.LayerId != source.ObjectId) continue;
                    candidates.Add(entityId);
                }
            }
            // AttributeReference is owned by a BlockReference, not necessarily enumerated
            // as a direct BTR entity. Include ATTRIB layers to honor "all objects".
            // This second pass covers model/layout and ordinary block definitions.
            foreach (ObjectId blockId in bt)
            {
                var record = (BlockTableRecord)tr.GetObject(blockId, OpenMode.ForRead);
                if (record.IsFromExternalReference || record.IsFromOverlayReference) continue;
                foreach (ObjectId entityId in record)
                {
                    if (!(tr.GetObject(entityId, OpenMode.ForRead, false) is BlockReference br) || br.IsErased) continue;
                    foreach (ObjectId attributeId in br.AttributeCollection)
                    {
                        if (tr.GetObject(attributeId, OpenMode.ForRead, false) is AttributeReference attr &&
                            !attr.IsErased && attr.LayerId == source.ObjectId)
                            candidates.Add(attributeId);
                    }
                }
            }
            if (candidates.Count > 200000) throw new InvalidOperationException("单次合并超过 20 万对象，请分批处理。");
            if (!dryRun)
            {
                // Do not create half-merged results: callers must abort the enclosing transaction
                // on any exception. Locked source layer needs temporary unlock for editing.
                var wasLocked = source.IsLocked;
                if (wasLocked) { source.UpgradeOpen(); source.IsLocked = false; }
                foreach (var id in candidates)
                {
                    var entity = (Entity)tr.GetObject(id, OpenMode.ForWrite);
                    entity.LayerId = target.ObjectId;
                }
                if (ctx.Database.Clayer == source.ObjectId)
                    ctx.Database.Clayer = target.ObjectId;
                if (wasLocked) source.IsLocked = true;
            }
            return new Result
            {
                success = true, dry_run = dryRun, operation = "merge", source_layer = source.Name,
                target_layer = target.Name, moved = candidates.Count, skipped = skipped,
                // Source layer is not erased automatically: table records can be referenced by
                // other non-entity database objects; deleting blindly is lossy. We report that.
                removed_source_layer = false,
                message = dryRun ? "预览：将移动所有可编辑的本地图元（包括普通块定义内的图元）。" :
                    "已迁移本地图元；来源空图层暂时保留，必要时可执行 AutoCAD PURGE。"
            };
        }

        public static LayerTableRecord OpenLayer(CadExecutionContext ctx, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("请选择图层。");
            var lt = (LayerTable)ctx.Transaction.GetObject(ctx.Database.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(name.Trim())) throw new ArgumentException("当前 DWG 中不存在该图层：" + name);
            return (LayerTableRecord)ctx.Transaction.GetObject(lt[name.Trim()], OpenMode.ForRead);
        }
    }
}
