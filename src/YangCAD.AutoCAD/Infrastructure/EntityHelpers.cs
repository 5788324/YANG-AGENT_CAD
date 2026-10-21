using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace YangCAD.AutoCADHost.Infrastructure
{
    public static class EntityHelpers
    {
        public static ObjectId FromHandle(Database db, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("handle 不能为空。");
            return db.GetObjectId(false, new Handle(Convert.ToInt64(value, 16)), 0);
        }

        public static Dictionary<string,object?> Describe(Entity ent)
        {
            var d = new Dictionary<string,object?>
            {
                ["handle"] = ent.Handle.ToString(), ["dxf_type"] = ent.GetRXClass().DxfName,
                ["layer"] = ent.Layer, ["color_index"] = ent.ColorIndex, ["linetype"] = ent.Linetype
            };
            if (ent is Line l) { d["start"] = Pt(l.StartPoint); d["end"] = Pt(l.EndPoint); d["length"] = l.Length; }
            else if (ent is Polyline p)
            {
                d["closed"] = p.Closed; d["length"] = p.Length;
                var pts = new List<double[]>(); for (var i=0;i<p.NumberOfVertices;i++) pts.Add(Pt(p.GetPoint3dAt(i))); d["vertices"] = pts;
            }
            else if (ent is DBText t) { d["text"] = t.TextString; d["position"] = Pt(t.Position); d["height"] = t.Height; }
            else if (ent is MText mt) { d["text"] = mt.Contents; d["location"] = Pt(mt.Location); d["text_height"] = mt.TextHeight; }
            else if (ent is BlockReference br) { d["position"] = Pt(br.Position); d["rotation"] = br.Rotation; d["rotation_degrees"] = br.Rotation * 180.0 / Math.PI; }
            return d;
        }

        public static double[] Pt(Point3d p) => new[] { p.X,p.Y,p.Z };
        public static Point3d ToPoint(double[]? v, string name)
        {
            if (v == null || v.Length < 2 || v.Length > 3) throw new ArgumentException($"{name} 必须是 [X,Y] 或 [X,Y,Z]。");
            return new Point3d(v[0],v[1],v.Length>2?v[2]:0.0);
        }
    }
}
