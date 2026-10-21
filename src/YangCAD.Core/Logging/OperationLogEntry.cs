namespace YangCAD.Core.Logging
{
    public sealed class OperationLogEntry
    {
        public string Time { get; set; } = "";
        public string RequestId { get; set; } = "";
        public string Tool { get; set; } = "";
        public bool Ok { get; set; }
        public bool DryRun { get; set; }
        public bool IsWrite { get; set; }
        public string Product { get; set; } = "";
        public string Document { get; set; } = "";
        public long DurationMs { get; set; }
        public string? Error { get; set; }
    }
}
