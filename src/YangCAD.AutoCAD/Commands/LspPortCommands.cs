using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Runtime;
using YangCAD.AutoCADHost.Services.Cad;

[assembly: CommandClass(typeof(YangCAD.AutoCADHost.Commands.LspPortCommands))]

namespace YangCAD.AutoCADHost.Commands
{
    public sealed class LspPortCommands
    {
        [CommandMethod("BMK", CommandFlags.Modal | CommandFlags.UsePickSet)]
        [CommandMethod("YGBMK", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void BeamMark() => StructuralMarkService.RunBeamMark(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("BMKFIX", CommandFlags.Modal | CommandFlags.UsePickSet)]
        [CommandMethod("YGBMKFIX", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void BeamTextFix() => DimTextToolsService.RunTextUp(Application.DocumentManager.MdiActiveDocument, askRef: false);

        [CommandMethod("CMK", CommandFlags.Modal | CommandFlags.UsePickSet)]
        [CommandMethod("YGCMK", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void ColumnMark() => StructuralMarkService.RunColumnMark(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("SLB")]
        [CommandMethod("YGSLB")]
        public void SlabPlace() => SlabSymbolService.RunSlabPlace(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("SLBP")]
        public void SlabPreview() => SlabSymbolService.RunSlabPreview(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("SLBFIX", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void SlabFix() => SlabSymbolService.RunSlabFix(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("SLBLAYER")]
        public void SlabLayer() => SlabSymbolService.RunSlabLayerSelect(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("SLBSET")]
        public void SlabSettings() => SlabSymbolService.RunSlabSettings(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("DIMCHK", CommandFlags.Modal | CommandFlags.UsePickSet)]
        [CommandMethod("YGDIMCHK", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void DimCheck() => DimTextToolsService.RunDimCheck(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("DIMFIX", CommandFlags.Modal | CommandFlags.UsePickSet)]
        [CommandMethod("YGDIMFIX", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void DimFix() => DimTextToolsService.RunDimFix(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("DIMRE", CommandFlags.Modal | CommandFlags.UsePickSet)]
        [CommandMethod("YGDIMRE", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void DimRestore() => DimTextToolsService.RunDimRestore(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("DIMFZ", CommandFlags.Modal | CommandFlags.UsePickSet)]
        [CommandMethod("YGDIMFZ", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void DimFlip() => DimTextToolsService.RunDimFlip(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("TXTUP", CommandFlags.Modal | CommandFlags.UsePickSet)]
        [CommandMethod("YGTXTUP", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void TextUp() => DimTextToolsService.RunTextUp(Application.DocumentManager.MdiActiveDocument, askRef: true);

        [CommandMethod("DFSET")]
        public void DimFixSettings() => DimTextToolsService.RunDimSet(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("DIMINFO")]
        public void DimInfo() => DimTextToolsService.RunDimInfo(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("TXTINFO")]
        public void TxtInfo() => DimTextToolsService.RunTxtInfo(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("TXAL")]
        [CommandMethod("YGTXAL")]
        public void TextAlign() => DimTextToolsService.RunTextAlign(Application.DocumentManager.MdiActiveDocument);

        // ============================================================
        // 贱人工具箱经典文字增强 (对齐/递增/拆合/空格换行/大小写/标点/刷换/加框)
        // ============================================================

        [CommandMethod("YGTL")]
        public void TextAlignLeft() => JianrenTextService.RunBoxAlign(Application.DocumentManager.MdiActiveDocument, "L");

        [CommandMethod("YGTC")]
        public void TextAlignCenter() => JianrenTextService.RunBoxAlign(Application.DocumentManager.MdiActiveDocument, "C");

        [CommandMethod("YGTR")]
        public void TextAlignRight() => JianrenTextService.RunBoxAlign(Application.DocumentManager.MdiActiveDocument, "R");

        [CommandMethod("YGTT")]
        public void TextAlignTop() => JianrenTextService.RunBoxAlign(Application.DocumentManager.MdiActiveDocument, "T");

        [CommandMethod("YGTB")]
        public void TextAlignBottom() => JianrenTextService.RunBoxAlign(Application.DocumentManager.MdiActiveDocument, "B");

        [CommandMethod("YGTV")]
        public void TextAlignMiddle() => JianrenTextService.RunBoxAlign(Application.DocumentManager.MdiActiveDocument, "V");

        [CommandMethod("YGTLINE")]
        public void TextAlignToLine() => JianrenTextService.RunAlignToLine(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("YGTARC")]
        public void TextAlignToArc() => JianrenTextService.RunAlignToArc(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("YGINC")]
        public void TextIncrementCopy() => JianrenTextService.RunIncrementCopy(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("YGTJOIN")]
        public void TextJoin() => JianrenTextService.RunJoinText(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("YGTS2M")]
        public void TextSingleToMulti() => JianrenTextService.RunSingleToMulti(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("YGTM2S")]
        public void TextMultiToSingle() => JianrenTextService.RunMultiToSingle(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("YGTEXP")]
        public void TextExplodeChars() => JianrenTextService.RunExplodeToChars(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("YGTSPACE")]
        public void TextSpaceClean() => JianrenTextService.RunSpaceClean(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("YGTSPACEM")]
        public void TextSpaceCleanMulti() => JianrenTextService.RunSpaceClean(Application.DocumentManager.MdiActiveDocument, "M");

        [CommandMethod("YGTNOLINE")]
        public void TextRemoveNewlines() => JianrenTextService.RunSpaceClean(Application.DocumentManager.MdiActiveDocument, "N");

        [CommandMethod("YGTTRIMLINE")]
        public void TextTrimExtraNewlines() => JianrenTextService.RunSpaceClean(Application.DocumentManager.MdiActiveDocument, "L");

        [CommandMethod("YGTCASE")]
        public void TextCaseConvert() => JianrenTextService.RunCaseConvert(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("YGTUPPER")]
        public void TextCaseUpper() => JianrenTextService.RunCaseConvert(Application.DocumentManager.MdiActiveDocument, "U");

        [CommandMethod("YGTLOWER")]
        public void TextCaseLower() => JianrenTextService.RunCaseConvert(Application.DocumentManager.MdiActiveDocument, "L");

        [CommandMethod("YGTTITLE")]
        public void TextCaseTitle() => JianrenTextService.RunCaseConvert(Application.DocumentManager.MdiActiveDocument, "T");

        [CommandMethod("YGTCAMEL")]
        public void TextCaseCamel() => JianrenTextService.RunCaseConvert(Application.DocumentManager.MdiActiveDocument, "C");

        [CommandMethod("YGTPUNC")]
        public void TextPunctuationSwitch() => JianrenTextService.RunPunctuationSwitch(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("YGTPUNCE")]
        public void TextPunctuationToEn() => JianrenTextService.RunPunctuationSwitch(Application.DocumentManager.MdiActiveDocument, "E");

        [CommandMethod("YGTPUNCC")]
        public void TextPunctuationToCn() => JianrenTextService.RunPunctuationSwitch(Application.DocumentManager.MdiActiveDocument, "C");

        [CommandMethod("YGTAFFIX")]
        public void TextAffix() => JianrenTextService.RunAffix(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("YGTBRUSH")]
        public void TextContentBrush() => JianrenTextService.RunContentBrush(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("YGTSWAP")]
        public void TextContentSwap() => JianrenTextService.RunContentSwap(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("YGTPROP")]
        public void TextPropertyBrush() => JianrenTextService.RunPropertyBrush(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("YGTBOXSLOT")]
        public void TextBoxSlot() => JianrenTextService.RunTextBoxAdvanced(Application.DocumentManager.MdiActiveDocument, true, false);

        [CommandMethod("YGTBOXMASK")]
        public void TextBoxMask() => JianrenTextService.RunTextBoxAdvanced(Application.DocumentManager.MdiActiveDocument, false, true);

        [CommandMethod("YGTSET")]
        public void TextToolsSettings() => JianrenTextService.RunTextSettings(Application.DocumentManager.MdiActiveDocument);

        // ============================================================
        // 源泉设计风格填充图案增强工具 (HHJ / HHX / HHU / HHGL / HHB / HHI)
        // ============================================================

        [CommandMethod("YGHCLIP")]
        [CommandMethod("HHJ")]
        public void HatchClip() => YqHatchToolsService.RunHatchClip(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("YGHCLIPSPLIT")]
        [CommandMethod("HHJK")]
        public void HatchClipSplit() => YqHatchToolsService.RunHatchClip(Application.DocumentManager.MdiActiveDocument, "K");

        [CommandMethod("YGHCLIPINT")]
        [CommandMethod("HHJI")]
        public void HatchClipIntersect() => YqHatchToolsService.RunHatchClip(Application.DocumentManager.MdiActiveDocument, "I");

        [CommandMethod("YGHSPLIT")]
        [CommandMethod("HHX")]
        public void HatchSplit() => YqHatchToolsService.RunHatchSplit(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("YGHUNION")]
        [CommandMethod("HHU")]
        public void HatchUnion() => YqHatchToolsService.RunHatchUnion(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("YGHDIS")]
        [CommandMethod("HHGL")]
        public void HatchDisassociate() => YqHatchToolsService.RunHatchDisassociate(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("YGHBOUND")]
        [CommandMethod("HHB")]
        public void HatchBoundary() => YqHatchToolsService.RunHatchBoundary(Application.DocumentManager.MdiActiveDocument);

        [CommandMethod("YGHBASE")]
        [CommandMethod("HHI")]
        public void HatchOrigin() => YqHatchToolsService.RunHatchOrigin(Application.DocumentManager.MdiActiveDocument);
    }
}
