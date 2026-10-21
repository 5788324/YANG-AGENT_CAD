using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

namespace YangCAD.BatchWorker
{
    public static class BatchWorkerCommand
    {
        [CommandMethod("YANGCAD_BATCH_RUN", CommandFlags.Session)]
        public static void Run()
        {
            var jobPath = Environment.GetEnvironmentVariable("YANGCAD_BATCH_JOB") ?? "";
            string? resultPath = null;
            object result;
            try
            {
                if (string.IsNullOrWhiteSpace(jobPath) || !File.Exists(jobPath))
                    throw new InvalidOperationException("YANGCAD_BATCH_JOB is missing or invalid.");

                var job = BatchJson.ParseObject(File.ReadAllText(jobPath));
                resultPath = BatchJson.RequiredString(job, "result_path");
                var inputPath = Path.GetFullPath(BatchJson.RequiredString(job, "input_path"));
                var mode = BatchJson.RequiredString(job, "mode").ToLowerInvariant();
                var dryRun = BatchJson.Bool(job, "dry_run", true);
                var outputPath = BatchJson.String(job, "output_path");
                var options = job.TryGetValue("options", out var rawOptions) && rawOptions is Dictionary<string, object?> typed
                    ? typed
                    : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                var doc = Application.DocumentManager.MdiActiveDocument
                    ?? throw new InvalidOperationException("AcCoreConsole has no active document.");
                var db = doc.Database;
                var activeName = string.IsNullOrWhiteSpace(db.Filename) ? doc.Name : db.Filename;
                var activePath = Path.GetFullPath(activeName);
                if (!string.Equals(activePath, inputPath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Active DWG mismatch. expected='{inputPath}' actual='{activePath}'.");
                var started = DateTime.UtcNow;
                var sourceDwgVersion = db.OriginalFileVersion.ToString();
                object data;
                string? savedDwgVersion = null;
                if (mode == "audit")
                {
                    if (!dryRun) throw new InvalidOperationException("audit mode is read-only and must use dry_run=true.");
                    data = BatchOperations.Audit(db, options);
                }
                else if (mode == "hygiene")
                {
                    using (doc.LockDocument())
                    {
                        data = BatchOperations.Hygiene(db, options, dryRun);
                        if (!dryRun)
                        {
                            if (string.IsNullOrWhiteSpace(outputPath))
                                throw new InvalidOperationException("Real hygiene requires output_path.");
                            var fullOutput = Path.GetFullPath(outputPath);
                            if (string.Equals(fullOutput, inputPath, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException("Batch worker refuses in-place overwrite; output_path must differ from input_path.");
                            if (File.Exists(fullOutput))
                                throw new InvalidOperationException("Batch worker refuses to overwrite an existing output DWG.");
                            Directory.CreateDirectory(Path.GetDirectoryName(fullOutput) ?? throw new InvalidOperationException("Invalid output directory."));
                            var saveVersion = db.OriginalFileVersion;
                            if (saveVersion == DwgVersion.Unknown || saveVersion == DwgVersion.Max) saveVersion = DwgVersion.Current;
                            db.SaveAs(fullOutput, saveVersion);
                            savedDwgVersion = saveVersion.ToString();
                        }
                    }
                }
                else
                {
                    throw new ArgumentException("Unsupported mode: " + mode);
                }

                result = new
                {
                    ok = true,
                    mode,
                    dry_run = dryRun,
                    input_path = inputPath,
                    output_path = dryRun ? null : outputPath,
                    elapsed_ms = (long)(DateTime.UtcNow - started).TotalMilliseconds,
                    source_dwg_version = sourceDwgVersion,
                    saved_dwg_version = savedDwgVersion,
                    data
                };
            }
            catch (System.Exception ex)
            {
                result = new { ok = false, error = ex.GetType().Name + ": " + ex.Message };
            }

            try
            {
                if (string.IsNullOrWhiteSpace(resultPath) && !string.IsNullOrWhiteSpace(jobPath) && File.Exists(jobPath))
                {
                    var job = BatchJson.ParseObject(File.ReadAllText(jobPath));
                    resultPath = BatchJson.String(job, "result_path");
                }
                if (!string.IsNullOrWhiteSpace(resultPath))
                {
                    var full = Path.GetFullPath(resultPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(full) ?? throw new InvalidOperationException("Invalid result directory."));
                    File.WriteAllText(full, BatchJson.Serialize(result));
                }
            }
            catch
            {
                // The process stdout remains diagnostic-only. Python treats missing result JSON as a worker failure.
            }
        }
    }
}
