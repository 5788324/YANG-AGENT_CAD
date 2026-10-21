using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace YangCAD.AutoCADHost.Infrastructure
{
    /// <summary>
    /// Minimal Autodesk execution context for new Toolbox services.
    /// Construction is intentionally internal so only CadTransactionRunner can create it.
    /// </summary>
    public sealed class CadExecutionContext
    {
        internal CadExecutionContext(Document document, Transaction transaction)
        {
            Document = document;
            Database = document.Database;
            Editor = document.Editor;
            Transaction = transaction;
        }

        public Document Document { get; }
        public Database Database { get; }
        public Editor Editor { get; }
        public Transaction Transaction { get; }
    }
}
