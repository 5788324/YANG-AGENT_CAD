using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace YangCAD.BatchWorker
{
    internal static class BatchOperations
    {
        private static BlockTableRecord ModelSpace(Transaction tr, Database db)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            return (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        }

        private static bool Near(Point2d a, Point2d b, double tolerance) => a.GetDistanceTo(b) <= tolerance;

        private static bool CollinearAndBetween(Point2d a, Point2d b, Point2d c, double tolerance)
        {
            var ab = b - a;
            var ac = c - a;
            var acLength = ac.Length;
            if (acLength <= tolerance) return false;
            var cross = Math.Abs(ab.X * ac.Y - ab.Y * ac.X);
            var perpendicularDistance = cross / acLength;
            if (perpendicularDistance > tolerance) return false;
            var dot = (b.X - a.X) * (b.X - c.X) + (b.Y - a.Y) * (b.Y - c.Y);
            return dot <= tolerance * Math.Max(1.0, acLength);
        }

        private static bool HasNonZeroWidths(Polyline p)
        {
            if (p.HasWidth) return true;
            try { if (p.ConstantWidth != 0.0) return true; } catch { }
            for (var i = 0; i < p.NumberOfVertices; i++)
            {
                if (p.GetStartWidthAt(i) != 0.0 || p.GetEndWidthAt(i) != 0.0) return true;
            }
            return false;
        }

        private static object InspectPolyline(Polyline p, double tolerance)
        {
            var duplicate = 0;
            var zero = 0;
            var collinear = 0;
            var n = p.NumberOfVertices;
            var segmentCount = p.Closed ? n : Math.Max(0, n - 1);
            for (var i = 0; i < segmentCount; i++)
            {
                var j = (i + 1) % n;
                if (Near(p.GetPoint2dAt(i), p.GetPoint2dAt(j), tolerance))
                {
                    duplicate++;
                    zero++;
                }
            }

            var hasBulges = p.HasBulges;
            var hasWidths = HasNonZeroWidths(p);
            if (!hasBulges && !hasWidths && n >= 3)
            {
                var start = p.Closed ? 0 : 1;
                var end = p.Closed ? n : n - 1;
                for (var i = start; i < end; i++)
                {
                    var prev = (i - 1 + n) % n;
                    var next = (i + 1) % n;
                    var a = p.GetPoint2dAt(prev);
                    var b = p.GetPoint2dAt(i);
                    var c = p.GetPoint2dAt(next);
                    if (Near(a, b, tolerance) || Near(b, c, tolerance)) continue;
                    if (CollinearAndBetween(a, b, c, tolerance)) collinear++;
                }
            }

            return new
            {
                handle = p.Handle.ToString(),
                layer = p.Layer,
                vertex_count = n,
                duplicate_vertices = duplicate,
                zero_length_segments = zero,
                collinear_vertices = collinear,
                has_bulges = hasBulges,
                has_nonzero_segment_widths = hasWidths
            };
        }

        public static object Audit(Database db, IReadOnlyDictionary<string, object?> options)
        {
            var entityLimit = BatchJson.Int(options, "entity_limit", 200000);
            var issueLimit = BatchJson.Int(options, "issue_limit", 200);
            var tolerance = BatchJson.Double(options, "tolerance", 1e-6);
            if (entityLimit < 1 || entityLimit > 1000000) throw new ArgumentOutOfRangeException("entity_limit");
            if (issueLimit < 0 || issueLimit > 5000) throw new ArgumentOutOfRangeException("issue_limit");
            if (tolerance <= 0 || tolerance > 1.0) throw new ArgumentOutOfRangeException("tolerance");

            using var tr = db.TransactionManager.StartOpenCloseTransaction();
            var model = ModelSpace(tr, db);
            var typeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var issueSamples = new List<object>();
            var total = 0;
            var zeroLengthLines = 0;
            var emptyText = 0;
            var polylineIssues = 0;
            var proxyEntities = 0;
            var scanned = 0;

            foreach (ObjectId id in model)
            {
                total++;
                if (scanned >= entityLimit) continue;
                scanned++;
                var obj = tr.GetObject(id, OpenMode.ForRead, false);
                var typeName = obj.GetType().Name;
                if (obj is ProxyEntity) proxyEntities++;
                typeCounts[typeName] = typeCounts.TryGetValue(typeName, out var current) ? current + 1 : 1;

                if (obj is Line line && line.Length <= tolerance)
                {
                    zeroLengthLines++;
                    if (issueSamples.Count < issueLimit) issueSamples.Add(new { type = "zero_length_line", handle = line.Handle.ToString(), layer = line.Layer });
                }
                else if (obj is DBText text && string.IsNullOrWhiteSpace(text.TextString))
                {
                    emptyText++;
                    if (issueSamples.Count < issueLimit) issueSamples.Add(new { type = "empty_dbtext", handle = text.Handle.ToString(), layer = text.Layer });
                }
                else if (obj is MText mtext && string.IsNullOrWhiteSpace(mtext.Contents))
                {
                    emptyText++;
                    if (issueSamples.Count < issueLimit) issueSamples.Add(new { type = "empty_mtext", handle = mtext.Handle.ToString(), layer = mtext.Layer });
                }
                else if (obj is Polyline p)
                {
                    var info = InspectPolyline(p, tolerance);
                    var map = BatchJson.ParseObject(BatchJson.Serialize(info));
                    var hasIssue = Convert.ToInt32(map["duplicate_vertices"]) > 0 || Convert.ToInt32(map["zero_length_segments"]) > 0;
                    if (hasIssue)
                    {
                        polylineIssues++;
                        if (issueSamples.Count < issueLimit) issueSamples.Add(new { type = "polyline_issue", detail = info });
                    }
                }
            }

            var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            var layerIds = new ObjectIdCollection();
            var layerCount = 0;
            foreach (ObjectId id in layerTable)
            {
                layerCount++;
                layerIds.Add(id);
            }
            db.Purge(layerIds);
            var purgable = new List<string>();
            foreach (ObjectId id in layerIds)
            {
                var layer = tr.GetObject(id, OpenMode.ForRead, false) as LayerTableRecord;
                if (layer != null) purgable.Add(layer.Name);
            }

            tr.Commit();
            return new
            {
                read_only = true,
                entity_count = total,
                scanned_entity_count = scanned,
                entity_limit_reached = total > scanned,
                layer_count = layerCount,
                type_counts = typeCounts.OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key, kv => kv.Value),
                zero_length_lines = zeroLengthLines,
                empty_text = emptyText,
                polylines_with_duplicate_or_zero_segments = polylineIssues,
                proxy_entity_count = proxyEntities,
                purgable_layer_count = purgable.Count,
                purgable_layers = purgable.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
                issue_samples = issueSamples.ToArray(),
                tolerance
            };
        }

        public static object Hygiene(Database db, IReadOnlyDictionary<string, object?> options, bool dryRun)
        {
            var cleanupLayers = BatchJson.Bool(options, "cleanup_layers", true);
            var cleanupPolylines = BatchJson.Bool(options, "cleanup_polylines", true);
            var removeCollinear = BatchJson.Bool(options, "remove_collinear", false);
            var protectDefpoints = BatchJson.Bool(options, "protect_defpoints", true);
            var entityLimit = BatchJson.Int(options, "entity_limit", 200000);
            var tolerance = BatchJson.Double(options, "tolerance", 1e-6);
            if (!cleanupLayers && !cleanupPolylines) throw new ArgumentException("At least one cleanup operation must be enabled.");
            if (entityLimit < 1 || entityLimit > 1000000) throw new ArgumentOutOfRangeException("entity_limit");
            if (tolerance <= 0 || tolerance > 1.0) throw new ArgumentOutOfRangeException("tolerance");

            using var tr = db.TransactionManager.StartTransaction();
            var model = ModelSpace(tr, db);
            var ids = model.Cast<ObjectId>().ToArray();
            if (ids.Length > entityLimit)
                throw new InvalidOperationException($"ModelSpace entity count {ids.Length} exceeds entity_limit={entityLimit}; refuse partial batch write.");

            var allowProxySave = BatchJson.Bool(options, "allow_proxy_save", false);
            var proxyEntityCount = 0;
            foreach (var id in ids)
            {
                if (tr.GetObject(id, OpenMode.ForRead, false) is ProxyEntity) proxyEntityCount++;
            }
            if (!dryRun && proxyEntityCount > 0 && !allowProxySave)
                throw new InvalidOperationException($"Drawing contains {proxyEntityCount} ProxyEntity object(s); real hygiene refuses save unless allow_proxy_save=true or the matching vertical product is used.");

            var changedPolylines = 0;
            var removedDuplicateVertices = 0;
            var removedCollinearVertices = 0;
            var skippedBulge = 0;
            var skippedWidth = 0;

            if (cleanupPolylines)
            {
                foreach (var id in ids)
                {
                    var p = tr.GetObject(id, OpenMode.ForRead, false) as Polyline;
                    if (p == null) continue;
                    if (p.HasBulges) { skippedBulge++; continue; }
                    if (HasNonZeroWidths(p)) { skippedWidth++; continue; }
                    var minVertices = p.Closed ? 3 : 2;
                    if (p.NumberOfVertices <= minVertices) continue;

                    p.UpgradeOpen();
                    var beforeVertices = p.NumberOfVertices;
                    for (var i = p.NumberOfVertices - 1; i >= 1; i--)
                    {
                        if (p.NumberOfVertices <= minVertices) break;
                        if (!Near(p.GetPoint2dAt(i - 1), p.GetPoint2dAt(i), tolerance)) continue;
                        p.RemoveVertexAt(i);
                        removedDuplicateVertices++;
                    }
                    if (p.Closed && p.NumberOfVertices > minVertices && Near(p.GetPoint2dAt(p.NumberOfVertices - 1), p.GetPoint2dAt(0), tolerance))
                    {
                        p.RemoveVertexAt(p.NumberOfVertices - 1);
                        removedDuplicateVertices++;
                    }

                    if (removeCollinear && p.NumberOfVertices > minVertices)
                    {
                        var start = p.Closed ? p.NumberOfVertices - 1 : p.NumberOfVertices - 2;
                        var stop = p.Closed ? 0 : 1;
                        for (var i = start; i >= stop && p.NumberOfVertices > minVertices; i--)
                        {
                            if (i >= p.NumberOfVertices) continue;
                            var n = p.NumberOfVertices;
                            var prev = (i - 1 + n) % n;
                            var next = (i + 1) % n;
                            if (prev == next) continue;
                            var a = p.GetPoint2dAt(prev);
                            var b = p.GetPoint2dAt(i);
                            var c = p.GetPoint2dAt(next);
                            if (Near(a, b, tolerance) || Near(b, c, tolerance)) continue;
                            if (!CollinearAndBetween(a, b, c, tolerance)) continue;
                            p.RemoveVertexAt(i);
                            removedCollinearVertices++;
                        }
                    }

                    if (p.NumberOfVertices < beforeVertices) changedPolylines++;
                }
            }

            var deletedLayers = new List<string>();
            if (cleanupLayers)
            {
                var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                var candidates = new ObjectIdCollection();
                foreach (ObjectId id in layerTable)
                {
                    var layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    if (string.Equals(layer.Name, "0", StringComparison.OrdinalIgnoreCase)) continue;
                    if (protectDefpoints && string.Equals(layer.Name, "Defpoints", StringComparison.OrdinalIgnoreCase)) continue;
                    if (id == db.Clayer) continue;
                    if (layer.IsDependent || layer.IsLocked) continue;
                    candidates.Add(id);
                }
                db.Purge(candidates);
                foreach (ObjectId id in candidates)
                {
                    var layer = tr.GetObject(id, OpenMode.ForWrite, false) as LayerTableRecord;
                    if (layer == null || layer.IsErased) continue;
                    deletedLayers.Add(layer.Name);
                    layer.Erase(true);
                }
            }

            if (dryRun) tr.Abort(); else tr.Commit();
            return new
            {
                dry_run = dryRun,
                changed_polylines = changedPolylines,
                removed_duplicate_vertices = removedDuplicateVertices,
                removed_collinear_vertices = removedCollinearVertices,
                skipped_polylines_with_bulge = skippedBulge,
                skipped_polylines_with_width = skippedWidth,
                deleted_layer_count = deletedLayers.Count,
                proxy_entity_count = proxyEntityCount,
                allow_proxy_save = allowProxySave,
                deleted_layers = deletedLayers.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
                tolerance,
                remove_collinear = removeCollinear
            };
        }
    }
}
