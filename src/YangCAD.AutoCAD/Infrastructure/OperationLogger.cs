using System;
using System.IO;
using YangCAD.Core.Logging;

namespace YangCAD.AutoCADHost.Infrastructure
{
    public static class OperationLogger
    {
        private static readonly object Sync = new object();
        public static void Write(OperationLogEntry e)
        {
            try
            {
                var dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"YangCADAgent","logs","operations");
                Directory.CreateDirectory(dir);
                var path=Path.Combine(dir,DateTime.Now.ToString("yyyy-MM-dd")+".jsonl");
                lock(Sync) File.AppendAllText(path,JsonCodec.Serialize(e)+Environment.NewLine);
            }
            catch { }
        }
    }
}
