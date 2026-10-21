using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Services.Cad
{
    public static class LayerAuditService
    {
        public static object Audit(Database db, Transaction tr)
        {
            var table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var model = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            var counts = new Dictionary<ObjectId, int>();
            foreach (ObjectId id in model)
            {
                if (!(tr.GetObject(id, OpenMode.ForRead) is Entity e)) continue;
                counts.TryGetValue(e.LayerId, out var n);
                counts[e.LayerId] = n + 1;
            }

            var candidate = new ObjectIdCollection();
            foreach (ObjectId id in table) candidate.Add(id);
            db.Purge(candidate);
            var purgable = new HashSet<ObjectId>();
            foreach (ObjectId id in candidate) purgable.Add(id);

            var rows = new List<object>();
            foreach (ObjectId id in table)
            {
                var layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                counts.TryGetValue(id, out var modelCount);
                rows.Add(new
                {
                    name = layer.Name,
                    color_index = layer.Color.ColorIndex,
                    off = layer.IsOff,
                    frozen = layer.IsFrozen,
                    locked = layer.IsLocked,
                    dependent = layer.IsDependent,
                    current = id == db.Clayer,
                    modelspace_entity_count = modelCount,
                    purgable = purgable.Contains(id)
                });
            }
            return new
            {
                count = rows.Count,
                purgable_count = purgable.Count,
                layers = rows
            };
        }
    }
}
