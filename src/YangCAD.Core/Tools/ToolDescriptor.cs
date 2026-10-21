namespace YangCAD.Core.Tools
{
    public sealed class ToolDescriptor
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsWrite { get; set; }
        public bool SupportsDryRun { get; set; }
        public string Owner { get; set; } = "AutoCAD";
    }
}
