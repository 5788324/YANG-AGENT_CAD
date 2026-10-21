using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.Core.Logging;
using YangCAD.Core.Rpc;

namespace YangCAD.AutoCADHost.Mcp
{
    public sealed class CadRequestBroker : IDisposable
    {
        private sealed class Pending
        {
            // 0 = queued, 1 = running, 2 = completed, 3 = cancelled before execution.
            private int _state;
            public RpcRequest Request { get; }
            public ManualResetEventSlim Done { get; } = new ManualResetEventSlim(false);
            public RpcResponse? Response { get; set; }
            public Pending(RpcRequest request) => Request = request;
            public bool TryStart() => Interlocked.CompareExchange(ref _state, 1, 0) == 0;
            public bool TryCancelQueued() => Interlocked.CompareExchange(ref _state, 3, 0) == 0;
            public void Complete() { Interlocked.Exchange(ref _state, 2); Done.Set(); }
        }

        private readonly ToolRegistry _tools;
        private readonly ConcurrentQueue<Pending> _queue = new ConcurrentQueue<Pending>();
        private System.Windows.Forms.Timer? _timer;
        private int _pumping;
        private bool _started;
        public CadRequestBroker(ToolRegistry tools) => _tools = tools;

        public void Start()
        {
            if (_started) return;

            // RuntimeHost.Initialize() is called by AutoCAD on its UI thread. WinForms.Timer therefore
            // ticks on the CAD UI thread and gives queued RPC work a deterministic pump even when Idle
            // is not raised frequently enough. Idle remains as a low-cost fallback.
            if (Application.DocumentManager.MdiActiveDocument == null)
                throw new InvalidOperationException("Broker 不能在 Start 页/无活动 DWG 状态启动。");

            // TASK-001 proved Timer + Idle on AutoCAD/Civil 3D 2024.  Keep that known-good
            // dispatcher, but treat native Timer creation as optional so a Win32 resource error
            // can never escape into acad.exe startup.
            try
            {
                _timer = new System.Windows.Forms.Timer { Interval = 50 };
                _timer.Tick += OnTimer;
                _timer.Start();
                RuntimeLogger.Info("Broker WinForms Timer started after active DWG.");
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("Broker Timer unavailable; falling back to Idle-only for this session.", ex);
                try
                {
                    if (_timer != null)
                    {
                        _timer.Tick -= OnTimer;
                        _timer.Dispose();
                        _timer = null;
                    }
                }
                catch { }
            }
            Application.Idle += OnIdle;
            _started = true;
            RuntimeLogger.Info("Broker started after active DWG; Idle fallback registered.");
        }

        public RpcResponse Invoke(RpcRequest request, TimeSpan timeout)
        {
            var p = new Pending(request);
            _queue.Enqueue(p);
            if (p.Done.Wait(timeout))
                return p.Response ?? RpcResponse.Failure(request.Id, request.Tool, "主线程未返回结果。");

            if (p.TryCancelQueued())
                return RpcResponse.Failure(request.Id, request.Tool, $"等待进入 AutoCAD 主线程执行超时（{timeout.TotalSeconds:0}s），请求已取消且不会延迟执行。");

            // The UI thread already claimed the request. Do not report an ambiguous client-side timeout
            // while Autodesk API work may still commit. Preserve exactly-once semantics and wait.
            p.Done.Wait();
            return p.Response ?? RpcResponse.Failure(request.Id, request.Tool, "主线程未返回结果。");
        }

        private void OnTimer(object? sender, EventArgs e) => PumpOne();
        private void OnIdle(object? sender, EventArgs e) => PumpOne();

        private void PumpOne()
        {
            if (Interlocked.Exchange(ref _pumping, 1) != 0) return;
            try
            {
                if (!HostReadyForRequest()) return;
                if (!_queue.TryDequeue(out var p)) return;
                if (!p.TryStart()) return;
                Execute(p);
            }
            finally { Volatile.Write(ref _pumping, 0); }
        }

        private void Execute(Pending p)
        {
            var sw = Stopwatch.StartNew();
            var r = p.Request;
            var dry = false;
            var write = false;
            var docName = "";
            try
            {
                if (!_tools.TryGet(r.Tool, out var tool)) { p.Response = RpcResponse.Failure(r.Id, r.Tool, "未知 Tool。"); return; }
                write = tool.Descriptor.IsWrite;
                if (r.Args.TryGetValue("dry_run", out var dryRaw))
                {
                    if (!(dryRaw is bool dryValue)) { p.Response = RpcResponse.Failure(r.Id, r.Tool, "参数 dry_run 必须是布尔值。"); return; }
                    dry = dryValue;
                }

                var settings = UserSettings.Load();
                if (write && !settings.AllowWrites) { p.Response = RpcResponse.Failure(r.Id, r.Tool, "当前设置禁止 MCP 写入。"); return; }
                if ((string.Equals(r.Tool, "cad.entity.erase", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(r.Tool, "cad.layer.cleanup", StringComparison.OrdinalIgnoreCase)) &&
                    !settings.AllowErase)
                {
                    p.Response = RpcResponse.Failure(r.Id, r.Tool, "当前设置禁止 MCP 删除/清理对象；请先在控制中心显式开启删除权限。");
                    return;
                }
                if (string.Equals(r.Tool, "cad.command.run", StringComparison.OrdinalIgnoreCase) && !settings.AllowCommandExecution) { p.Response = RpcResponse.Failure(r.Id, r.Tool, "实验性 cad.command.run 当前未授权；请在控制中心显式开启。"); return; }
                if (string.Equals(r.Tool, "cad.extension.invoke", StringComparison.OrdinalIgnoreCase) && !settings.AllowExtensionInvoke) { p.Response = RpcResponse.Failure(r.Id, r.Tool, "动态扩展执行当前未授权；请在控制中心显式开启。"); return; }

                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) { p.Response = RpcResponse.Failure(r.Id, r.Tool, "当前没有活动 DWG。"); return; }
                docName = doc.Name;
                var documentId = InstanceRegistry.GetDocumentId(doc);
                var documentChanged =
                    (!string.IsNullOrWhiteSpace(r.ExpectedDocumentId) && !string.Equals(r.ExpectedDocumentId, documentId, StringComparison.Ordinal)) ||
                    (!string.IsNullOrWhiteSpace(r.ExpectedDocument) && !string.Equals(r.ExpectedDocument, docName, StringComparison.OrdinalIgnoreCase));
                if (documentChanged)
                {
                    InstanceRegistry.RefreshCurrent();
                    p.Response = RpcResponse.Failure(r.Id, r.Tool, $"活动 DWG 已变化：请求绑定 '{r.ExpectedDocument}'，当前为 '{docName}'。Document session 不匹配；为避免写错图，本次请求未执行，请先重新读取当前状态/实体。");
                    return;
                }

                var data = tool.Execute(new CadToolContext(doc, dry, r.Args));
                p.Response = RpcResponse.Success(r.Id, r.Tool, data);
            }
            catch (System.Exception ex) { p.Response = RpcResponse.Failure(r.Id, r.Tool, ex.Message); }
            finally
            {
                sw.Stop();
                try
                {
                    OperationLogger.Write(new OperationLogEntry
                    {
                        Time = DateTimeOffset.Now.ToString("O"), RequestId = r.Id, Tool = r.Tool,
                        Ok = p.Response?.Ok == true, DryRun = dry, IsWrite = write,
                        Product = CadHostInfo.Product, Document = docName,
                        DurationMs = sw.ElapsedMilliseconds, Error = p.Response?.Error
                    });
                }
                catch { }
                finally { p.Complete(); }
            }
        }

        private static bool HostReadyForRequest()
        {
            try
            {
                // Keep /health + instance JSON truthful across SaveAs and last-DWG-close transitions.
                // The refresh is a no-op unless identity actually changed.
                InstanceRegistry.RefreshCurrentIfChanged();
                if (Application.DocumentManager.MdiActiveDocument == null) return false;
                var commandNames = Convert.ToString(Application.GetSystemVariable("CMDNAMES"));
                return string.IsNullOrWhiteSpace(commandNames);
            }
            catch { return false; }
        }

        public void Dispose()
        {
            if (_started)
            {
                try { Application.Idle -= OnIdle; } catch { }
                try
                {
                    if (_timer != null)
                    {
                        _timer.Stop();
                        _timer.Tick -= OnTimer;
                        _timer.Dispose();
                        _timer = null;
                    }
                }
                catch { }
                _started = false;
            }
            while (_queue.TryDequeue(out var p))
            {
                if (!p.TryCancelQueued()) continue;
                p.Response = RpcResponse.Failure(p.Request.Id, p.Request.Tool, "YangCAD 正在关闭。");
                p.Done.Set();
            }
        }
    }
}
