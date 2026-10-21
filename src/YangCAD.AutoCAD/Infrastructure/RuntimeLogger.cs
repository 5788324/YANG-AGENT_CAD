using System;
using System.IO;
using System.Text.RegularExpressions;

namespace YangCAD.AutoCADHost.Infrastructure
{
    public static class RuntimeLogger
    {
        private static readonly object Sync = new object();
        private static readonly Regex BearerRegex = new Regex(@"Bearer\s+[A-Za-z0-9+/=_-]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static string LogPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YangCADAgent", "logs", "runtime.log");

        public static void Info(string message) => Write("INFO", message);

        public static void Error(string message, Exception? exception = null)
        {
            if (exception == null)
            {
                Write("ERROR", message);
                return;
            }

            // Full managed stack is required for Autodesk-host crash diagnosis. Redact bearer-like
            // material defensively so detailed diagnostics never turn runtime.log into a token sink.
            Write("ERROR", message + Environment.NewLine + Redact(exception.ToString()));
        }

        private static string Redact(string value)
            => BearerRegex.Replace(value ?? "", "Bearer [REDACTED]");

        private static void Write(string level, string message)
        {
            try
            {
                var path = LogPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var line = DateTimeOffset.Now.ToString("O") + " [" + level + "] " + Redact(message) + Environment.NewLine;
                lock (Sync) File.AppendAllText(path, line);
            }
            catch { }
        }
    }
}
