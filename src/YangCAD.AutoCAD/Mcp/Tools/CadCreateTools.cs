using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.Core.Tools;

namespace YangCAD.AutoCADHost.Mcp.Tools
{
    internal static class CadCreateHelpers
    {
        public static BlockTableRecord ModelSpace(Transaction transaction, Database database, OpenMode mode = OpenMode.ForWrite)
        {
            var table = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            return (BlockTableRecord)transaction.GetObject(table[BlockTableRecord.ModelSpace], mode);
        }

        public static void ApplyCommon(Entity entity, Transaction transaction, Database database, string? layer, short? color)
        {
            entity.SetDatabaseDefaults(database);
            if (!string.IsNullOrWhiteSpace(layer))
            {
                var layers = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
                if (!layers.Has(layer)) throw new InvalidOperationException("图层不存在：" + layer + "。请先调用 cad.layer.ensure。");
                entity.Layer = layer;
            }
            if (color.HasValue) entity.Color = Color.FromColorIndex(ColorMethod.ByAci, color.Value);
        }

        public static object Result(Entity entity, bool dryRun, object? extra = null)
        {
            var description = EntityHelpers.Describe(entity);
            if (dryRun) description["handle"] = null;
            return new
            {
                dry_run = dryRun,
                created = !dryRun,
                entity = description,
                extra
            };
        }

        public static List<double[]> PointList(IReadOnlyDictionary<string, object?> args, string name, int minCount)
        {
            if (!args.TryGetValue(name, out var raw) || raw == null || raw is string || !(raw is IEnumerable source))
                throw new ArgumentException($"参数 {name} 必须是坐标数组列表。");

            var points = new List<double[]>();
            foreach (var item in source)
            {
                if (item == null || item is string || !(item is IEnumerable values))
                    throw new ArgumentException($"参数 {name} 的每一项必须是 [X,Y] 或 [X,Y,Z]。");
                var point = new List<double>();
                foreach (var value in values)
                {
                    if (value == null) throw new ArgumentException($"参数 {name} 的坐标不能包含 null。");
                    try { point.Add(Convert.ToDouble(value, CultureInfo.InvariantCulture)); }
                    catch { throw new ArgumentException($"参数 {name} 必须全部为数值。"); }
                }
                if (point.Count < 2 || point.Count > 3)
                    throw new ArgumentException($"参数 {name} 的每个坐标必须是 [X,Y] 或 [X,Y,Z]。");
                points.Add(point.ToArray());
            }
            if (points.Count < minCount) throw new ArgumentException($"参数 {name} 至少需要 {minCount} 个点。");
            return points;
        }
    }

    public sealed class CadLineCreateTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        {
            Name = "cad.line.create",
            Description = "在模型空间创建 Line。",
            IsWrite = true,
            SupportsDryRun = true
        };

        public object Execute(CadToolContext context)
        {
            var start = EntityHelpers.ToPoint(ToolJson.DoubleArray(context.Args, "start"), "start");
            var end = EntityHelpers.ToPoint(ToolJson.DoubleArray(context.Args, "end"), "end");
            if (start.DistanceTo(end) <= 1e-9) throw new ArgumentException("Line 起点与终点不能重合。");
            var layer = ToolJson.OptionalNonBlankString(context.Args, "layer");
            var color = ToolJson.AciColor(context.Args, "color_index");

            return CadTransactionRunner.Write(context.Document, context.DryRun, tr =>
            {
                var modelSpace = CadCreateHelpers.ModelSpace(tr, context.Database);
                var line = new Line(start, end);
                CadCreateHelpers.ApplyCommon(line, tr, context.Database, layer, color);
                modelSpace.AppendEntity(line);
                tr.AddNewlyCreatedDBObject(line, true);
                return CadCreateHelpers.Result(line, context.DryRun);
            });
        }
    }

    public sealed class CadPolylineCreateTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        {
            Name = "cad.polyline.create",
            Description = "创建 2D lightweight Polyline；支持闭合，所有顶点必须共用同一 Z 高程。",
            IsWrite = true,
            SupportsDryRun = true
        };

        public object Execute(CadToolContext context)
        {
            var vertices = CadCreateHelpers.PointList(context.Args, "vertices", 2);
            var closed = ToolJson.Bool(context.Args, "closed") ?? false;
            if (closed && vertices.Count < 3) throw new ArgumentException("闭合 Polyline 至少需要 3 个顶点。");
            var elevation = vertices[0].Length > 2 ? vertices[0][2] : 0.0;
            foreach (var point in vertices)
            {
                var z = point.Length > 2 ? point[2] : 0.0;
                if (Math.Abs(z - elevation) > 1e-9)
                    throw new ArgumentException("当前 cad.polyline.create 创建 2D Polyline，所有顶点 Z 必须一致；3D Polyline 后续使用专用 Tool。 ");
            }
            var layer = ToolJson.OptionalNonBlankString(context.Args, "layer");
            var color = ToolJson.AciColor(context.Args, "color_index");

            return CadTransactionRunner.Write(context.Document, context.DryRun, tr =>
            {
                var modelSpace = CadCreateHelpers.ModelSpace(tr, context.Database);
                var polyline = new Polyline { Elevation = elevation };
                CadCreateHelpers.ApplyCommon(polyline, tr, context.Database, layer, color);
                for (var i = 0; i < vertices.Count; i++)
                    polyline.AddVertexAt(i, new Point2d(vertices[i][0], vertices[i][1]), 0.0, 0.0, 0.0);
                polyline.Closed = closed;
                modelSpace.AppendEntity(polyline);
                tr.AddNewlyCreatedDBObject(polyline, true);
                return CadCreateHelpers.Result(polyline, context.DryRun);
            });
        }
    }

    public sealed class CadTextCreateTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        {
            Name = "cad.text.create",
            Description = "创建 DBText 或 MText。",
            IsWrite = true,
            SupportsDryRun = true
        };

        public object Execute(CadToolContext context)
        {
            var kind = (ToolJson.String(context.Args, "kind") ?? "dbtext").Trim().ToLowerInvariant();
            if (kind != "dbtext" && kind != "mtext") throw new ArgumentException("kind 仅支持 dbtext/mtext。");
            var text = ToolJson.RequiredString(context.Args, "text");
            if (kind == "dbtext" && (text.IndexOf('\r') >= 0 || text.IndexOf('\n') >= 0))
                throw new ArgumentException("DBText 仅支持单行文字；多行请使用 kind=mtext。");
            var position = EntityHelpers.ToPoint(ToolJson.DoubleArray(context.Args, "position"), "position");
            var height = ToolJson.Double(context.Args, "height") ?? 2.5;
            if (height <= 0) throw new ArgumentOutOfRangeException("height", "文字高度必须 > 0。");
            var width = ToolJson.Double(context.Args, "width");
            if (width.HasValue && width.Value <= 0) throw new ArgumentOutOfRangeException("width", "MText width 必须 > 0。");
            var layer = ToolJson.OptionalNonBlankString(context.Args, "layer");
            var color = ToolJson.AciColor(context.Args, "color_index");

            return CadTransactionRunner.Write(context.Document, context.DryRun, tr =>
            {
                var modelSpace = CadCreateHelpers.ModelSpace(tr, context.Database);
                Entity entity;
                if (kind == "dbtext")
                {
                    var dbText = new DBText { Position = position, Height = height, TextString = text };
                    entity = dbText;
                }
                else
                {
                    var mText = new MText { Location = position, TextHeight = height, Contents = text };
                    if (width.HasValue) mText.Width = width.Value;
                    entity = mText;
                }

                CadCreateHelpers.ApplyCommon(entity, tr, context.Database, layer, color);
                modelSpace.AppendEntity(entity);
                tr.AddNewlyCreatedDBObject(entity, true);
                return CadCreateHelpers.Result(entity, context.DryRun, new { kind });
            });
        }
    }

    public sealed class CadBlockListTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        {
            Name = "cad.block.list",
            Description = "列出可插入的块定义；默认排除布局、匿名块和外部参照。"
        };

        public object Execute(CadToolContext context)
        {
            var includeAnonymous = ToolJson.Bool(context.Args, "include_anonymous") ?? false;
            var includeXrefs = ToolJson.Bool(context.Args, "include_xrefs") ?? false;
            var limit = Math.Max(1, Math.Min(2000, ToolJson.Int(context.Args, "limit") ?? 500));
            return CadTransactionRunner.Read(context.Document, tr =>
            {
                var table = (BlockTable)tr.GetObject(context.Database.BlockTableId, OpenMode.ForRead);
                var rows = new List<object>();
                foreach (ObjectId id in table)
                {
                    if (rows.Count >= limit) break;
                    var record = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    if (record.IsLayout) continue;
                    if (!includeAnonymous && record.IsAnonymous) continue;
                    var isXref = record.IsFromExternalReference || record.IsFromOverlayReference;
                    if (!includeXrefs && isXref) continue;
                    var attributeCount = 0;
                    foreach (ObjectId childId in record)
                        if (tr.GetObject(childId, OpenMode.ForRead) is AttributeDefinition) attributeCount++;
                    rows.Add(new
                    {
                        name = record.Name,
                        anonymous = record.IsAnonymous,
                        xref = isXref,
                        attribute_definitions = attributeCount
                    });
                }
                return new { count = rows.Count, limit, blocks = rows };
            });
        }
    }

    public sealed class CadBlockInsertTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        {
            Name = "cad.block.insert",
            Description = "插入现有块定义，支持统一缩放、旋转、图层和非 Constant 属性值。",
            IsWrite = true,
            SupportsDryRun = true
        };

        public object Execute(CadToolContext context)
        {
            var name = ToolJson.RequiredString(context.Args, "name");
            var position = EntityHelpers.ToPoint(ToolJson.DoubleArray(context.Args, "position"), "position");
            var scale = ToolJson.Double(context.Args, "scale") ?? 1.0;
            if (scale <= 0) throw new ArgumentOutOfRangeException("scale", "block scale 必须 > 0。");
            var rotationDegrees = ToolJson.Double(context.Args, "rotation_degrees") ?? 0.0;
            var layer = ToolJson.OptionalNonBlankString(context.Args, "layer");
            var attributes = ToolJson.Object(context.Args, "attributes") ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            return CadTransactionRunner.Write(context.Document, context.DryRun, tr =>
            {
                var blockTable = (BlockTable)tr.GetObject(context.Database.BlockTableId, OpenMode.ForRead);
                if (!blockTable.Has(name)) throw new InvalidOperationException("块定义不存在：" + name + "。请先调用 cad.block.list。");
                var definition = (BlockTableRecord)tr.GetObject(blockTable[name], OpenMode.ForRead);
                if (definition.IsLayout) throw new InvalidOperationException("不能把布局 BlockTableRecord 当普通块插入：" + name);
                if (definition.IsFromExternalReference || definition.IsFromOverlayReference)
                    throw new InvalidOperationException("cad.block.insert 默认不直接插入 Xref 定义：" + name);

                var modelSpace = CadCreateHelpers.ModelSpace(tr, context.Database);
                var block = new BlockReference(position, definition.ObjectId)
                {
                    ScaleFactors = new Scale3d(scale),
                    Rotation = rotationDegrees * Math.PI / 180.0
                };
                CadCreateHelpers.ApplyCommon(block, tr, context.Database, layer, null);
                modelSpace.AppendEntity(block);
                tr.AddNewlyCreatedDBObject(block, true);

                var applied = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var availableTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var constantTagsIgnored = new List<string>();
                foreach (ObjectId childId in definition)
                {
                    if (!(tr.GetObject(childId, OpenMode.ForRead) is AttributeDefinition definitionAttribute)) continue;
                    availableTags.Add(definitionAttribute.Tag);
                    if (definitionAttribute.Constant)
                    {
                        if (attributes.ContainsKey(definitionAttribute.Tag)) constantTagsIgnored.Add(definitionAttribute.Tag);
                        continue;
                    }

                    var reference = new AttributeReference();
                    reference.SetAttributeFromBlock(definitionAttribute, block.BlockTransform);
                    reference.Position = definitionAttribute.Position.TransformBy(block.BlockTransform);
                    if (attributes.TryGetValue(definitionAttribute.Tag, out var supplied) && supplied != null)
                    {
                        reference.TextString = Convert.ToString(supplied, CultureInfo.InvariantCulture) ?? "";
                        applied[definitionAttribute.Tag] = reference.TextString;
                    }
                    block.AttributeCollection.AppendAttribute(reference);
                    tr.AddNewlyCreatedDBObject(reference, true);
                }

                var unknown = attributes.Keys.Where(key => !availableTags.Contains(key)).OrderBy(key => key).ToArray();
                return CadCreateHelpers.Result(block, context.DryRun, new
                {
                    block_name = name,
                    attributes_applied = applied,
                    constant_attribute_tags_ignored = constantTagsIgnored.OrderBy(tag => tag).ToArray(),
                    unknown_attribute_tags = unknown
                });
            });
        }
    }
}
