using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Mcp.Extensions
{
    public sealed class YangCadExtensionDescriptor
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsWrite { get; set; } = true;
        public bool SupportsDryRun { get; set; } = true;
        public bool RequiresErase { get; set; } = false;
    }

    public sealed class YangCadExtensionArtifactManifest
    {
        public int SchemaVersion { get; set; }
        public string ManagedBy { get; set; } = "";
        public string BuildId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string Operation { get; set; } = "";
        public string Description { get; set; } = "";
        public int Year { get; set; }
        public string TargetProduct { get; set; } = "";
        public string TargetFramework { get; set; } = "";
        public bool IsWrite { get; set; }
        public bool SupportsDryRun { get; set; }
        public bool RequiresErase { get; set; }
        public string SourceSha256 { get; set; } = "";
        public string ExecuteBodySha256 { get; set; } = "";
        public string BuildFingerprintSha256 { get; set; } = "";
        public string DllSha256 { get; set; } = "";
        public string DllFile { get; set; } = "";
        public string AssemblyName { get; set; } = "";
        public string HostDllSha256 { get; set; } = "";
    }

    public sealed class YangCadExtensionChange
    {
        public string Action { get; set; } = "";
        public string Target { get; set; } = "";
        public string? Before { get; set; }
        public string? After { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>
    /// Contract for versioned extension DLLs placed under %LOCALAPPDATA%\YangCADAgent\Extensions.
    /// Extensions must use the supplied transaction; the host owns commit/abort and Undo scope.
    /// TASK-007 generated write extensions also record a JSON-safe change ledger for preview/diff evidence.
    /// </summary>
    public interface IYangCadExtensionOperation
    {
        YangCadExtensionDescriptor Descriptor { get; }
        object? Execute(YangCadExtensionContext context);
    }

    public sealed class YangCadExtensionContext
    {
        private const int MaxChanges = 500;
        private readonly List<YangCadExtensionChange> _changes = new List<YangCadExtensionChange>();

        public Document Document { get; }
        public Database Database => Document.Database;
        public Transaction Transaction { get; }
        public bool DryRun { get; }
        public IReadOnlyDictionary<string, object?> Args { get; }
        public IReadOnlyList<YangCadExtensionChange> Changes => _changes;

        internal YangCadExtensionContext(Document document, Transaction transaction, bool dryRun, IReadOnlyDictionary<string, object?> args)
        {
            Document = document;
            Transaction = transaction;
            DryRun = dryRun;
            Args = args;
        }

        public DBObject Open(string handle, OpenMode mode)
            => Transaction.GetObject(EntityHelpers.FromHandle(Database, handle), mode);

        /// <summary>
        /// Records an extension-reported, JSON-safe change item. This is evidence for preview/diff,
        /// not an independent host snapshot of every Autodesk property mutation.
        /// </summary>
        public void RecordChange(string action, string target, string? before = null, string? after = null, string? note = null)
        {
            if (string.IsNullOrWhiteSpace(action)) throw new ArgumentException("change action 不能为空。", nameof(action));
            if (string.IsNullOrWhiteSpace(target)) throw new ArgumentException("change target 不能为空。", nameof(target));
            if (_changes.Count >= MaxChanges) throw new InvalidOperationException($"Extension change ledger 超过上限 {MaxChanges}。请缩小任务范围。");
            _changes.Add(new YangCadExtensionChange
            {
                Action = action.Trim(),
                Target = target.Trim(),
                Before = before,
                After = after,
                Note = note
            });
        }
    }
}
