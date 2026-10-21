using System;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.Core.Tools;

namespace YangCAD.AutoCADHost.Mcp.Tools
{
    public sealed class CadEntityUpdateTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        {
            Name = "cad.entity.update",
            Description = "修改图层/颜色/线型/文字。",
            IsWrite = true,
            SupportsDryRun = true
        };

        public object Execute(CadToolContext context)
        {
            var handle = ToolJson.RequiredString(context.Args, "handle");
            var layer = ToolJson.OptionalNonBlankString(context.Args, "layer");
            var color = ToolJson.AciColor(context.Args, "color_index");
            var linetype = ToolJson.OptionalNonBlankString(context.Args, "linetype");
            var text = ToolJson.String(context.Args, "text");
            if (layer == null && !color.HasValue && linetype == null && text == null)
                throw new ArgumentException("至少需要提供 layer、color_index、linetype、text 中的一项修改。");

            return CadTransactionRunner.Write(context.Document, context.DryRun, transaction =>
            {
                var entity = transaction.GetObject(EntityHelpers.FromHandle(context.Database, handle), OpenMode.ForWrite) as Entity
                    ?? throw new InvalidOperationException("Handle 不是实体。");

                if (!string.IsNullOrWhiteSpace(layer))
                {
                    var table = (LayerTable)transaction.GetObject(context.Database.LayerTableId, OpenMode.ForRead);
                    if (!table.Has(layer)) throw new InvalidOperationException($"图层不存在：{layer}");
                    entity.Layer = layer;
                }
                if (color.HasValue) entity.Color = Color.FromColorIndex(ColorMethod.ByAci, color.Value);
                if (!string.IsNullOrWhiteSpace(linetype)) entity.Linetype = linetype;
                if (text != null)
                {
                    if (entity is DBText dbText) dbText.TextString = text;
                    else if (entity is MText mText) mText.Contents = text;
                    else throw new InvalidOperationException("该实体不是 DBText/MText。");
                }
                return new { dry_run = context.DryRun, entity = EntityHelpers.Describe(entity) };
            });
        }
    }

    public sealed class CadEntityTransformTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        {
            Name = "cad.entity.transform",
            Description = "move/rotate/scale 实体。",
            IsWrite = true,
            SupportsDryRun = true
        };

        public object Execute(CadToolContext context)
        {
            var handle = ToolJson.RequiredString(context.Args, "handle");
            var operation = ToolJson.RequiredString(context.Args, "operation").ToLowerInvariant();

            return CadTransactionRunner.Write(context.Document, context.DryRun, transaction =>
            {
                var entity = transaction.GetObject(EntityHelpers.FromHandle(context.Database, handle), OpenMode.ForWrite) as Entity
                    ?? throw new InvalidOperationException("Handle 不是实体。");

                Matrix3d matrix;
                if (operation == "move")
                {
                    var vector = ToolJson.DoubleArray(context.Args, "vector");
                    if (vector == null || vector.Length < 2 || vector.Length > 3)
                        throw new ArgumentException("move 的 vector 必须是 [X,Y] 或 [X,Y,Z]。");
                    matrix = Matrix3d.Displacement(new Vector3d(vector[0], vector[1], vector.Length > 2 ? vector[2] : 0));
                }
                else if (operation == "rotate")
                {
                    var basePoint = EntityHelpers.ToPoint(ToolJson.DoubleArray(context.Args, "base_point"), "base_point");
                    var degrees = ToolJson.Double(context.Args, "angle_degrees") ?? throw new ArgumentException("缺少 angle_degrees");
                    matrix = Matrix3d.Rotation(degrees * Math.PI / 180.0, Vector3d.ZAxis, basePoint);
                }
                else if (operation == "scale")
                {
                    var basePoint = EntityHelpers.ToPoint(ToolJson.DoubleArray(context.Args, "base_point"), "base_point");
                    var factor = ToolJson.Double(context.Args, "factor") ?? throw new ArgumentException("缺少 factor");
                    if (factor <= 0) throw new ArgumentOutOfRangeException("factor", "scale factor 必须 > 0。");
                    matrix = Matrix3d.Scaling(factor, basePoint);
                }
                else
                {
                    throw new ArgumentException("operation 仅支持 move/rotate/scale");
                }

                entity.TransformBy(matrix);
                return new { dry_run = context.DryRun, entity = EntityHelpers.Describe(entity) };
            });
        }
    }

    public sealed class CadEntityEraseTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        {
            Name = "cad.entity.erase",
            Description = "删除实体；控制中心默认禁用。",
            IsWrite = true,
            SupportsDryRun = true
        };

        public object Execute(CadToolContext context)
        {
            var handle = ToolJson.RequiredString(context.Args, "handle");
            return CadTransactionRunner.Write(context.Document, context.DryRun, transaction =>
            {
                var entity = transaction.GetObject(EntityHelpers.FromHandle(context.Database, handle), OpenMode.ForWrite) as Entity
                    ?? throw new InvalidOperationException("Handle 不是实体。");
                var before = EntityHelpers.Describe(entity);
                entity.Erase();
                return new { dry_run = context.DryRun, erased = !context.DryRun, before };
            });
        }
    }

    public sealed class CadLayerEnsureTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        {
            Name = "cad.layer.ensure",
            Description = "确保图层存在并按需更新 ACI 颜色。",
            IsWrite = true,
            SupportsDryRun = true
        };

        public object Execute(CadToolContext context)
        {
            var name = ToolJson.RequiredString(context.Args, "name");
            var color = ToolJson.AciColor(context.Args, "color_index");

            return CadTransactionRunner.Write(context.Document, context.DryRun, transaction =>
            {
                var table = (LayerTable)transaction.GetObject(context.Database.LayerTableId, OpenMode.ForRead);
                var existedBefore = table.Has(name);
                LayerTableRecord layer;

                if (existedBefore)
                {
                    layer = (LayerTableRecord)transaction.GetObject(table[name], color.HasValue ? OpenMode.ForWrite : OpenMode.ForRead);
                }
                else
                {
                    table.UpgradeOpen();
                    layer = new LayerTableRecord { Name = name };
                    table.Add(layer);
                    transaction.AddNewlyCreatedDBObject(layer, true);
                }

                if (color.HasValue) layer.Color = Color.FromColorIndex(ColorMethod.ByAci, color.Value);
                return new
                {
                    dry_run = context.DryRun,
                    existed_before = existedBefore,
                    would_create = !existedBefore,
                    created = !existedBefore && !context.DryRun,
                    name = layer.Name,
                    color_index = layer.Color.ColorIndex
                };
            });
        }
    }
}
