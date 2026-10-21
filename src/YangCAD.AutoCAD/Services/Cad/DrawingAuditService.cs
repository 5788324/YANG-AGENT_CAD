using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Services.Cad
{
    public static class DrawingAuditService
    {
        public static object Audit(Database db, Transaction tr, string documentName, int entityLimit, int issueLimit, double tolerance)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var model = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            var typeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var layerCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var issues = new List<object>();
            var scanned = 0;
            var totalIssues = 0;
            var zeroLengthLines = 0;
            var emptyText = 0;
            var polylineDuplicateVertices = 0;
            var truncated = false;

            foreach (ObjectId id in model)
            {
                if (scanned >= entityLimit) { truncated = true; break; }
                scanned++;
                if (!(tr.GetObject(id, OpenMode.ForRead) is Entity entity)) continue;

                var dxf = entity.GetRXClass().DxfName;
                typeCounts.TryGetValue(dxf, out var tc);
                typeCounts[dxf] = tc + 1;
                layerCounts.TryGetValue(entity.Layer, out var lc);
                layerCounts[entity.Layer] = lc + 1;

                string? issue = null;
                if (entity is Line line && line.Length <= tolerance)
                {
                    zeroLengthLines++;
                    issue = "zero-length-line";
                }
                else if (entity is DBText dbText && string.IsNullOrWhiteSpace(dbText.TextString))
                {
                    emptyText++;
                    issue = "empty-dbtext";
                }
                else if (entity is MText mText && string.IsNullOrWhiteSpace(mText.Contents))
                {
                    emptyText++;
                    issue = "empty-mtext";
                }
                else if (entity is Polyline polyline)
                {
                    var dup = 0;
                    var segmentCount = polyline.Closed ? polyline.NumberOfVertices : Math.Max(0, polyline.NumberOfVertices - 1);
                    for (var i = 0; i < segmentCount; i++)
                    {
                        var j = (i + 1) % polyline.NumberOfVertices;
                        if (PolylineInspectionService.Near(polyline.GetPoint2dAt(i), polyline.GetPoint2dAt(j), tolerance)) dup++;
                    }
                    if (dup > 0)
                    {
                        polylineDuplicateVertices += dup;
                        issue = "polyline-zero-length-segment";
                    }
                }

                if (issue != null)
                {
                    totalIssues++;
                    if (issues.Count < issueLimit)
                        issues.Add(new { handle = entity.Handle.ToString(), dxf_type = dxf, layer = entity.Layer, issue });
                }
            }

            var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var xrefCount = 0;
            var blockDefinitionCount = 0;
            foreach (ObjectId id in blockTable)
            {
                var record = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                if (record.IsLayout) continue;
                blockDefinitionCount++;
                if (record.IsFromExternalReference || record.IsFromOverlayReference) xrefCount++;
            }

            var layerCount = 0;
            foreach (ObjectId id in layerTable) layerCount++;

            return new
            {
                document = documentName,
                scanned_entities = scanned,
                scan_truncated = truncated,
                entity_limit = entityLimit,
                layer_count = layerCount,
                block_definition_count = blockDefinitionCount,
                xref_count = xrefCount,
                type_counts = typeCounts,
                layer_counts = layerCounts,
                issue_summary = new
                {
                    issue_entity_count = totalIssues,
                    issue_occurrence_count = zeroLengthLines + emptyText + polylineDuplicateVertices,
                    zero_length_lines = zeroLengthLines,
                    empty_text = emptyText,
                    polyline_zero_length_segments = polylineDuplicateVertices
                },
                issue_limit = issueLimit,
                issues_returned = issues.Count,
                issues = issues.ToArray()
            };
        }
    }
}
