using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace YangCAD.AutoCADHost.Infrastructure
{
    public static class CadTransactionRunner
    {
        public static T Read<T>(Document doc, Func<Transaction,T> body)
        {
            using (doc.LockDocument())
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var result = body(tr);
                tr.Commit();
                return result;
            }
        }

        public static T Write<T>(Document doc, bool dryRun, Func<Transaction,T> body)
        {
            using (doc.LockDocument())
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var result = body(tr);
                if (dryRun) tr.Abort(); else tr.Commit();
                return result;
            }
        }

        // Toolbox-only additive entry points. Keep the established Read/Write(Transaction) overloads
        // untouched so the existing MCP tool call sites stay source/binary-stable.
        public static T ReadContext<T>(Document doc, Func<CadExecutionContext,T> body)
        {
            return Read(doc, tr => body(new CadExecutionContext(doc, tr)));
        }

        public static T WriteContext<T>(Document doc, bool dryRun, Func<CadExecutionContext,T> body)
        {
            return Write(doc, dryRun, tr => body(new CadExecutionContext(doc, tr)));
        }
    }
}
