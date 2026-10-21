using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Services.Cad
{
    public static class LayerCleanupService
    {
        public static object Cleanup(
            Database db,
            Transaction tr,
            IReadOnlyCollection<string>? requestedNames,
            bool includeDefpoints,
            bool dryRun)
        {
            var table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            var requested = new HashSet<string>(requestedNames ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (requested.Count > 0)
            {
                foreach (var name in requested)
                    if (!table.Has(name)) throw new InvalidOperationException("图层不存在：" + name);
            }

            var candidates = new ObjectIdCollection();
            var skipped = new List<object>();
            foreach (ObjectId id in table)
            {
                var layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                if (requested.Count > 0 && !requested.Contains(layer.Name)) continue;

                string? reason = null;
                if (string.Equals(layer.Name, "0", StringComparison.OrdinalIgnoreCase)) reason = "protected-layer-0";
                else if (!includeDefpoints && string.Equals(layer.Name, "Defpoints", StringComparison.OrdinalIgnoreCase)) reason = "protected-defpoints";
                else if (id == db.Clayer) reason = "current-layer";
                else if (layer.IsDependent) reason = "xref-dependent";
                else if (layer.IsLocked) reason = "locked";

                if (reason != null)
                {
                    skipped.Add(new { name = layer.Name, reason });
                    continue;
                }
                candidates.Add(id);
            }

            db.Purge(candidates);
            var purgable = new List<ObjectId>();
            foreach (ObjectId id in candidates) purgable.Add(id);
            var names = purgable
                .Select(id => ((LayerTableRecord)tr.GetObject(id, OpenMode.ForRead)).Name)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (!dryRun)
            {
                foreach (ObjectId id in purgable)
                {
                    var layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                    layer.Erase(true);
                }
            }

            return new
            {
                dry_run = dryRun,
                requested = (requestedNames ?? Array.Empty<string>()).ToArray(),
                include_defpoints = includeDefpoints,
                would_delete = names,
                deleted = dryRun ? Array.Empty<string>() : names,
                skipped = skipped.ToArray()
            };
        }
    }
}
