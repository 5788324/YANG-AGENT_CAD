namespace YangCAD.Setup
{
    internal sealed class ReleaseManifest
    {
        public int schema { get; set; }
        public string product { get; set; }
        public string version { get; set; }
        public int[] years { get; set; }
        public ReleaseFile[] files { get; set; }
        public ReleaseInstaller installer { get; set; }
        public ReleaseSigning signing { get; set; }
    }

    internal sealed class ReleaseFile
    {
        public string path { get; set; }
        public string sha256 { get; set; }
        public long size { get; set; }
    }

    internal sealed class ReleaseInstaller
    {
        public string file { get; set; }
        public string scope_default { get; set; }
        public bool requires_admin { get; set; }
        public string ui_language { get; set; }
        public string mode { get; set; }
        public string trust_strategy { get; set; }
    }

    internal sealed class ReleaseSigning
    {
        public string mode { get; set; }
        public string publisher_thumbprint { get; set; }
        public string publisher_subject { get; set; }
        public string certificate_file { get; set; }
    }
}
