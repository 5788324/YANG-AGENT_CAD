using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.ApplicationServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.Core.Rpc;

namespace YangCAD.AutoCADHost.Mcp
{
    public static class InstanceRegistry
    {
        private static readonly object Sync = new object();
        private static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YangCADAgent", "instances");
        public static string CurrentPath => Path.Combine(Root, Process.GetCurrentProcess().Id + ".json");

        private sealed class DocumentIdentity
        {
            public string Id { get; } = Guid.NewGuid().ToString("N");
        }

        private static readonly ConditionalWeakTable<Document, DocumentIdentity> DocumentIdentities = new ConditionalWeakTable<Document, DocumentIdentity>();

        private static int _port;
        private static string _token = "";
        private static string _startedAt = "";
        private static bool _tracking;
        private static InstanceDescriptor? _lastDescriptor;

        public static void Write(int port, string token)
        {
            lock (Sync)
            {
                _port = port;
                _token = token;
                if (string.IsNullOrWhiteSpace(_startedAt)) _startedAt = DateTimeOffset.Now.ToString("O");
                if (!_tracking)
                {
                    AcApp.DocumentManager.DocumentActivated += OnDocumentActivated;
                    _tracking = true;
                }
                WriteCurrentCore();
            }
        }

        public static void RefreshCurrent()
        {
            lock (Sync)
            {
                if (_port <= 0 || string.IsNullOrWhiteSpace(_token)) return;
                WriteCurrentCore();
            }
        }

        /// <summary>
        /// Refresh only when host/document identity actually changed. This catches SaveAs and
        /// zero-document transitions that do not necessarily raise DocumentActivated, without
        /// rewriting the instance JSON on every Broker timer tick.
        /// </summary>
        public static void RefreshCurrentIfChanged()
        {
            lock (Sync)
            {
                if (_port <= 0 || string.IsNullOrWhiteSpace(_token)) return;
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                var document = doc?.Name ?? "";
                var documentId = doc == null ? "" : GetDocumentId(doc);
                var previous = _lastDescriptor;
                // Zero-document state is deliberately system-variable-free; keep the last known host
                // metadata instead of calling Autodesk GetSystemVariable from the Start page.
                var product = doc == null ? (previous?.Product ?? "AutoCAD") : CadHostInfo.Product;
                var version = doc == null ? (previous?.Version ?? "") : CadHostInfo.Version;
                if (previous != null
                    && string.Equals(previous.Document, document, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(previous.DocumentId, documentId, StringComparison.Ordinal)
                    && string.Equals(previous.Product, product, StringComparison.Ordinal)
                    && string.Equals(previous.Version, version, StringComparison.Ordinal))
                    return;
                WriteCurrentCore();
            }
        }

        private static void OnDocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            RefreshCurrent();
        }

        private static void WriteCurrentCore()
        {
            Directory.CreateDirectory(Root);
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var now = DateTimeOffset.Now.ToString("O");
            var previous = _lastDescriptor;
            var descriptor = new InstanceDescriptor
            {
                ProcessId = Process.GetCurrentProcess().Id,
                Port = _port,
                Token = _token,
                // Never query system variables after the last DWG has closed. Preserve last known
                // host metadata while publishing an empty document/document_id.
                Product = doc == null ? (previous?.Product ?? "AutoCAD") : CadHostInfo.Product,
                Version = doc == null ? (previous?.Version ?? "") : CadHostInfo.Version,
                Document = doc?.Name ?? "",
                DocumentId = doc == null ? "" : GetDocumentId(doc),
                StartedAt = _startedAt,
                UpdatedAt = now
            };

            _lastDescriptor = descriptor;
            var json = JsonCodec.Serialize(descriptor, indented: true);
            var temp = CurrentPath + ".tmp";
            File.WriteAllText(temp, json);
            try
            {
                if (File.Exists(CurrentPath)) File.Replace(temp, CurrentPath, null);
                else File.Move(temp, CurrentPath);
            }
            catch
            {
                File.Copy(temp, CurrentPath, true);
                try { File.Delete(temp); } catch { }
            }
        }

        public static InstanceDescriptor Snapshot()
        {
            lock (Sync)
            {
                var d = _lastDescriptor;
                if (d == null) return new InstanceDescriptor();
                return new InstanceDescriptor
                {
                    ProcessId = d.ProcessId, Port = d.Port, Token = d.Token, Product = d.Product,
                    Version = d.Version, Document = d.Document, DocumentId = d.DocumentId, StartedAt = d.StartedAt, UpdatedAt = d.UpdatedAt
                };
            }
        }

        internal static string GetDocumentId(Document document)
            => DocumentIdentities.GetValue(document, _ => new DocumentIdentity()).Id;

        public static void DeleteCurrent()
        {
            lock (Sync)
            {
                if (_tracking)
                {
                    try { AcApp.DocumentManager.DocumentActivated -= OnDocumentActivated; } catch { }
                    _tracking = false;
                }
                _port = 0;
                _token = "";
                _startedAt = "";
                _lastDescriptor = null;
                try { if (File.Exists(CurrentPath)) File.Delete(CurrentPath); } catch { }
                try { if (File.Exists(CurrentPath + ".tmp")) File.Delete(CurrentPath + ".tmp"); } catch { }
            }
        }
    }
}
