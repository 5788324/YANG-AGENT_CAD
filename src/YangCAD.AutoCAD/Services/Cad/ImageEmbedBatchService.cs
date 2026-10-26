using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Services.Cad
{
    public static class ImageEmbedBatchService
    {
        public sealed class Request
        {
            public string InputPath { get; set; } = "";
            public string OutputFolder { get; set; } = "";
            public bool IncludeSubfolders { get; set; }
            public bool ShowWorker { get; set; }
            public bool RequireAllImages { get; set; } = true;
            public bool BindDrawingXrefs { get; set; } = true;
            public int CommandTimeoutSeconds { get; set; } = 90;
        }

        public sealed class Result
        {
            public bool success { get; set; } = true;
            public bool dry_run { get; set; }
            public bool started { get; set; }
            public string input_path { get; set; } = "";
            public string output_folder { get; set; } = "";
            public int file_count { get; set; }
            public bool include_subfolders { get; set; }
            public bool require_all_images { get; set; }
            public bool bind_dwg_xrefs { get; set; }
            public bool overwrite { get; set; }
            public string worker_mode { get; set; } = "independent_autocad_com_process";
            public string job_id { get; set; } = "";
            public int worker_pid { get; set; }
            public string worker { get; set; } = "";
            public string job_path { get; set; } = "";
            public string status_path { get; set; } = "";
            public string[] warnings { get; set; } = Array.Empty<string>();
            public string[] notes { get; set; } = Array.Empty<string>();
            public string[] sample_outputs { get; set; } = Array.Empty<string>();
            public string message { get; set; } = "";
            public string[] changed_object_ids { get; set; } = Array.Empty<string>();
            public string undo_semantics { get; set; } = "input_dwg_read_only_output_copy_no_host_undo";
        }

        private sealed class PlannedItem
        {
            public string input_path { get; set; } = "";
            public string output_path { get; set; } = "";
        }

        public static Result Preview(Request request)
        {
            var plan = BuildPlan(request, createOutputDirectory: false);
            return new Result
            {
                success = true,
                dry_run = true,
                started = false,
                input_path = plan.InputRoot,
                output_folder = plan.OutputRoot,
                file_count = plan.Items.Count,
                include_subfolders = request.IncludeSubfolders,
                require_all_images = request.RequireAllImages,
                bind_dwg_xrefs = request.BindDrawingXrefs,
                overwrite = false,
                warnings = new[]
                {
                    "将 ModelSpace / PaperSpace 的普通 RasterImage 内嵌为 OLE；普通块内图片可能需要额外处理。",
                    "V1 对 clipped / brightness-contrast-fade / raster transparency 图片拒绝转换，避免视觉失真。",
                    "DWG 外部参照可以绑定为内部块；PDF/DGN/DWF Underlay 不能无损绑定，会单独报告。",
                    "OLE 内嵌依赖 Windows Clipboard + AutoCAD PASTECLIP；执行时会暂时占用并清除系统剪贴板。"
                },
                notes = new[]
                {
                    "输入 DWG 不会被修改；输出在 *_Embedded 独立目录，已有输出文件跳过。Worker 异步运行，启动成功不等于内嵌成功。"
                },
                sample_outputs = plan.Items.Take(10).Select(x => x.output_path).ToArray(),
                message = "dry_run 仅生成计划，不创建输出目录、不启动 AutoCAD worker。"
            };
        }

        public static Result Start(Request request)
        {
            var plan = BuildPlan(request, createOutputDirectory: true);
            var worker = ResolveInstalledWorker();
            if (string.IsNullOrWhiteSpace(worker) || !File.Exists(worker))
                worker = RuntimeAssetMaterializer.EnsureImageEmbedWorker();
            if (!File.Exists(worker)) throw new FileNotFoundException("未找到 YangCAD 图片内嵌 worker。", worker);

            var jobId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var jobRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "YangCADAgent", "jobs", "image-embed", jobId);
            Directory.CreateDirectory(jobRoot);
            var jobPath = Path.Combine(jobRoot, "job.json");
            var statusPath = Path.Combine(jobRoot, "status.json");
            var logPath = Path.Combine(jobRoot, "worker.log");

            var job = new
            {
                schema_version = "yangcad-image-embed-job/1.0",
                job_id = jobId,
                created_at = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                autocad_progid = CurrentAutoCadProgId(),
                show_worker = request.ShowWorker,
                require_all_images = request.RequireAllImages,
                bind_dwg_xrefs = request.BindDrawingXrefs,
                command_timeout_seconds = Math.Max(30, Math.Min(600, request.CommandTimeoutSeconds)),
                status_path = statusPath,
                log_path = logPath,
                input_root = plan.InputRoot,
                output_root = plan.OutputRoot,
                items = plan.Items
            };
            File.WriteAllText(jobPath, JsonCodec.Serialize(job, indented: true));

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -Sta -ExecutionPolicy Bypass -File " + Quote(worker) + " -JobPath " + Quote(jobPath),
                UseShellExecute = false,
                CreateNoWindow = !request.ShowWorker,
                WorkingDirectory = Path.GetDirectoryName(worker) ?? ""
            };
            var process = Process.Start(psi) ?? throw new InvalidOperationException("无法启动 YangCAD 图片内嵌 worker。");
            return new Result
            {
                success = true,
                dry_run = false,
                started = true,
                input_path = plan.InputRoot,
                output_folder = plan.OutputRoot,
                file_count = plan.Items.Count,
                include_subfolders = request.IncludeSubfolders,
                require_all_images = request.RequireAllImages,
                bind_dwg_xrefs = request.BindDrawingXrefs,
                overwrite = false,
                job_id = jobId,
                worker_pid = process.Id,
                worker = worker,
                job_path = jobPath,
                status_path = statusPath,
                message = "任务已启动，并非执行完成。请查看 status.json 的最终 state、items 和失败原因；原 DWG 保持不变。"
            };
        }

        private sealed class Plan
        {
            public string InputRoot { get; set; } = "";
            public string OutputRoot { get; set; } = "";
            public List<PlannedItem> Items { get; } = new List<PlannedItem>();
        }

        private static Plan BuildPlan(Request request, bool createOutputDirectory)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var inputRaw = (request.InputPath ?? "").Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(inputRaw)) throw new ArgumentException("input_path 不能为空。");

            var input = Path.GetFullPath(Environment.ExpandEnvironmentVariables(inputRaw));
            var isFile = File.Exists(input);
            var isDirectory = Directory.Exists(input);
            if (!isFile && !isDirectory) throw new FileNotFoundException("输入 DWG/文件夹不存在。", input);
            if (isFile && !string.Equals(Path.GetExtension(input), ".dwg", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("单文件输入必须是 .dwg。");

            string inputRoot;
            string[] files;
            if (isFile)
            {
                inputRoot = Path.GetDirectoryName(input) ?? throw new InvalidOperationException("无法解析输入目录。");
                files = new[] { input };
            }
            else
            {
                inputRoot = input.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                files = Directory.GetFiles(inputRoot, "*.dwg", request.IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            if (files.Length == 0) throw new InvalidOperationException("输入范围内没有 DWG。");
            if (files.Length > 5000) throw new InvalidOperationException("单批最多 5000 个 DWG；请拆分目录后重试。");

            var outputRoot = ResolveOutputRoot(request.OutputFolder, inputRoot, isFile);
            if (IsSameOrChildPath(outputRoot, inputRoot) && !isFile)
                throw new InvalidOperationException("批量输出目录不能位于输入目录内部，避免下次递归扫描再次处理输出文件。");
            if (createOutputDirectory) Directory.CreateDirectory(outputRoot);

            var plan = new Plan { InputRoot = inputRoot, OutputRoot = outputRoot };
            foreach (var file in files)
            {
                var relative = isFile ? Path.GetFileName(file) : MakeRelativePath(inputRoot, file);
                var target = Path.GetFullPath(Path.Combine(outputRoot, relative));
                var source = Path.GetFullPath(file);
                if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("输出路径不能与输入 DWG 相同；本工具永不原位覆盖源文件。");
                plan.Items.Add(new PlannedItem { input_path = source, output_path = target });
            }
            return plan;
        }

        private static string ResolveOutputRoot(string requested, string inputRoot, bool isFile)
        {
            var raw = (requested ?? "").Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(raw))
                return Path.GetFullPath(Environment.ExpandEnvironmentVariables(raw));

            var parent = Directory.GetParent(inputRoot)?.FullName ?? inputRoot;
            var leaf = isFile ? "YangCAD_Embedded" : new DirectoryInfo(inputRoot).Name + "_Embedded";
            return Path.Combine(parent, leaf);
        }

        private static bool IsSameOrChildPath(string candidate, string parent)
        {
            var c = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var p = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return c.StartsWith(p, StringComparison.OrdinalIgnoreCase);
        }

        private static string MakeRelativePath(string basePath, string fullPath)
        {
            var baseUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(basePath)));
            var fileUri = new Uri(Path.GetFullPath(fullPath));
            return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fileUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }

        private static string AppendDirectorySeparator(string path)
            => path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? path : path + Path.DirectorySeparatorChar;

        private static string ResolveInstalledWorker()
        {
            var installed = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Autodesk", "ApplicationPlugins", "YangCADAgent.bundle", "Contents", "ImageEmbed", "Start-YangCAD-ImageEmbed.ps1");
            return installed;
        }

        private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

        private static string CurrentAutoCadProgId()
        {
#if ACAD_2021
            return "AutoCAD.Application.24.0";
#elif ACAD_2022
            return "AutoCAD.Application.24.1";
#elif ACAD_2023
            return "AutoCAD.Application.24.2";
#elif ACAD_2024
            return "AutoCAD.Application.24.3";
#elif ACAD_2025
            return "AutoCAD.Application.25.0";
#elif ACAD_2026
            return "AutoCAD.Application.25.1";
#elif ACAD_2027
            return "AutoCAD.Application.26.0";
#else
            return "AutoCAD.Application";
#endif
        }
    }
}
