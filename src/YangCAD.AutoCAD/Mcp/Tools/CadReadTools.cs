using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.Core.Tools;

namespace YangCAD.AutoCADHost.Mcp.Tools
{
    public sealed class CadStatusTool:ICadTool
    {
        public ToolDescriptor Descriptor{get;}=new ToolDescriptor{Name="cad.status",Description="读取宿主、文档、Gateway 和 Tool 状态。"};
        public object Execute(CadToolContext c)=>new{product=CadHostInfo.Product,version=CadHostInfo.Version,document=c.Document.Name,gateway_port=RuntimeHost.Gateway?.Port??0,tool_count=RuntimeHost.Tools.Describe().Length,tools=RuntimeHost.Tools.Describe()};
    }
    public sealed class CadDocumentInfoTool:ICadTool
    {
        public ToolDescriptor Descriptor{get;}=new ToolDescriptor{Name="cad.document.info",Description="读取当前 DWG 基础信息。"};
        public object Execute(CadToolContext c)=>new{name=c.Document.Name,filename=c.Database.Filename,insunits=c.Database.Insunits.ToString(),measurement=c.Database.Measurement.ToString(),tilemode=c.Database.TileMode};
    }
    public sealed class CadSelectionReadTool:ICadTool
    {
        public ToolDescriptor Descriptor{get;}=new ToolDescriptor{Name="cad.selection.read",Description="读取当前 PickFirst 选择集。"};
        public object Execute(CadToolContext c)
        {
            var s=c.Document.Editor.SelectImplied();if(s.Status!=PromptStatus.OK||s.Value==null)return new{count=0,entities=new object[0]};
            return CadTransactionRunner.Read(c.Document,tr=>{var rows=new List<object>();foreach(var id in s.Value.GetObjectIds())if(tr.GetObject(id,OpenMode.ForRead) is Entity e)rows.Add(EntityHelpers.Describe(e));return new{count=rows.Count,entities=rows};});
        }
    }
    public sealed class CadEntityGetTool:ICadTool
    {
        public ToolDescriptor Descriptor{get;}=new ToolDescriptor{Name="cad.entity.get",Description="按 DWG Handle 读取实体。"};
        public object Execute(CadToolContext c){var h=ToolJson.RequiredString(c.Args,"handle");return CadTransactionRunner.Read(c.Document,tr=>{var e=tr.GetObject(EntityHelpers.FromHandle(c.Database,h),OpenMode.ForRead) as Entity??throw new InvalidOperationException("Handle 不是实体。");return EntityHelpers.Describe(e);});}
    }
    public sealed class CadEntityQueryTool:ICadTool
    {
        public ToolDescriptor Descriptor{get;}=new ToolDescriptor{Name="cad.entity.query",Description="按 DXF 类型、图层、文字过滤模型空间实体。"};
        public object Execute(CadToolContext c)
        {
            var type=ToolJson.String(c.Args,"dxf_type");var layer=ToolJson.String(c.Args,"layer");var text=ToolJson.String(c.Args,"text_contains");var limit=Math.Max(1,Math.Min(1000,ToolJson.Int(c.Args,"limit")??200));
            return CadTransactionRunner.Read(c.Document,tr=>{var bt=(BlockTable)tr.GetObject(c.Database.BlockTableId,OpenMode.ForRead);var ms=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead);var rows=new List<object>();foreach(ObjectId id in ms){if(rows.Count>=limit)break;if(!(tr.GetObject(id,OpenMode.ForRead) is Entity e))continue;if(!string.IsNullOrWhiteSpace(type)&&!string.Equals(e.GetRXClass().DxfName,type,StringComparison.OrdinalIgnoreCase))continue;if(!string.IsNullOrWhiteSpace(layer)&&!string.Equals(e.Layer,layer,StringComparison.OrdinalIgnoreCase))continue;if(!string.IsNullOrWhiteSpace(text)){var t=e is DBText dt?dt.TextString:e is MText mt?mt.Contents:null;if(t==null||t.IndexOf(text,StringComparison.OrdinalIgnoreCase)<0)continue;}rows.Add(EntityHelpers.Describe(e));}return new{count=rows.Count,limit,entities=rows};});
        }
    }
    public sealed class CadLayerListTool:ICadTool
    {
        public ToolDescriptor Descriptor{get;}=new ToolDescriptor{Name="cad.layer.list",Description="列出全部图层及状态。"};
        public object Execute(CadToolContext c)=>CadTransactionRunner.Read(c.Document,tr=>{var table=(LayerTable)tr.GetObject(c.Database.LayerTableId,OpenMode.ForRead);var rows=new List<object>();foreach(ObjectId id in table){var l=(LayerTableRecord)tr.GetObject(id,OpenMode.ForRead);rows.Add(new{name=l.Name,color_index=l.Color.ColorIndex,off=l.IsOff,frozen=l.IsFrozen,locked=l.IsLocked});}return new{count=rows.Count,layers=rows};});
    }
}
