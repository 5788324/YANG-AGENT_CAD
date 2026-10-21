using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.Core.Rpc;

namespace YangCAD.AutoCADHost.Mcp
{
    /// <summary>
    /// Minimal loopback HTTP/1.1 gateway implemented on TcpListener.
    /// Intentionally avoids HttpListener/HTTP.sys URL ACL requirements so the personal plugin can run without admin setup.
    /// </summary>
    public sealed class LocalGateway : IDisposable
    {
        private const int MaxHeaderBytes = 64 * 1024;
        private const int MaxRequestBytes = 1024 * 1024;
        private readonly CadRequestBroker _broker;
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _loopTask;
        private string _token = "";
        public int Port { get; private set; }
        public bool IsRunning
        {
            get
            {
                var cts = _cts;
                var loop = _loopTask;
                return Port > 0 && _listener != null && cts != null && !cts.IsCancellationRequested && loop != null && !loop.IsCompleted;
            }
        }

        public LocalGateway(CadRequestBroker broker) => _broker = broker;

        public void Start()
        {
            _token = CreateToken();
            for (var port = 48710; port <= 48729; port++)
            {
                TcpListener? listener = null;
                try
                {
                    listener = new TcpListener(IPAddress.Loopback, port);
                    listener.Start();
                    _listener = listener;
                    Port = port;
                    break;
                }
                catch
                {
                    try { listener?.Stop(); } catch { }
                }
            }

            if (_listener == null) throw new InvalidOperationException("无法在 127.0.0.1:48710–48729 启动 YangCAD Gateway。");
            InstanceRegistry.Write(Port, _token);
            RuntimeLogger.Info("Gateway listening on 127.0.0.1:" + Port + ".");
            _cts = new CancellationTokenSource();
            _loopTask = Task.Run(() => LoopSafe(_cts.Token));
        }

        private async Task LoopSafe(CancellationToken token)
        {
            try
            {
                await Loop(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // normal shutdown
            }
            catch (Exception ex)
            {
                // Never allow a fire-and-forget gateway task to become an unobserved host exception.
                RuntimeLogger.Error("Gateway background loop stopped unexpectedly.", ex);
            }
        }

        private async Task Loop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener != null)
            {
                TcpClient client;
                try { client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false); }
                catch
                {
                    if (token.IsCancellationRequested) return;
                    continue;
                }
                _ = Task.Run(() => HandleClient(client));
            }
        }

        private void HandleClient(TcpClient client)
        {
            using (client)
            {
                client.NoDelay = true;
                client.ReceiveTimeout = 10000;
                client.SendTimeout = 10000;
                using (var stream = client.GetStream())
                {
                    try
                    {
                        var request = ReadRequest(stream);
                        if (request == null)
                        {
                            WriteResponse(stream, 400, new { ok = false, error = "Invalid request" });
                            return;
                        }
                        Handle(request, stream);
                    }
                    catch (InvalidDataException ex)
                    {
                        var status = ex.Message.IndexOf("too large", StringComparison.OrdinalIgnoreCase) >= 0 ? 413 : 400;
                        WriteResponse(stream, status, new { ok = false, error = ex.Message });
                    }
                    catch (Exception ex)
                    {
                        WriteResponse(stream, 500, new { ok = false, error = ex.Message });
                    }
                }
            }
        }

        private void Handle(HttpRequest request, NetworkStream stream)
        {
            var path = request.Path;
            var method = request.Method;

            if (string.Equals(path, "/health", StringComparison.Ordinal) && string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                if (!TokenOk(request.Authorization))
                {
                    WriteResponse(stream, 401, new { ok = false, error = "Unauthorized" });
                    return;
                }
                var snapshot = InstanceRegistry.Snapshot();
                var descriptors = RuntimeHost.Tools.Describe();
                var toolNames = Array.ConvertAll(descriptors, item => item.Name);
                WriteResponse(stream, 200, new
                {
                    ok = true,
                    process_id = snapshot.ProcessId,
                    port = snapshot.Port,
                    product = snapshot.Product,
                    version = snapshot.Version,
                    document = snapshot.Document,
                    document_id = snapshot.DocumentId,
                    started_at = snapshot.StartedAt,
                    updated_at = snapshot.UpdatedAt,
                    tools = toolNames
                });
                return;
            }

            if (!string.Equals(path, "/rpc", StringComparison.Ordinal) || !string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                WriteResponse(stream, 404, new { ok = false, error = "Not Found" });
                return;
            }
            if (!TokenOk(request.Authorization))
            {
                WriteResponse(stream, 401, new { ok = false, error = "Unauthorized" });
                return;
            }

            RpcRequest rpcRequest;
            try
            {
                rpcRequest = JsonCodec.ParseRpcRequest(request.Body);
            }
            catch (Exception)
            {
                WriteResponse(stream, 400, new { ok = false, error = "Invalid JSON" });
                return;
            }

            if (string.IsNullOrWhiteSpace(rpcRequest.Tool))
            {
                WriteResponse(stream, 400, new { ok = false, error = "Invalid request" });
                return;
            }
            if (string.IsNullOrWhiteSpace(rpcRequest.Id)) rpcRequest.Id = Guid.NewGuid().ToString("N");

            RuntimeLogger.Info("RPC accepted tool=" + rpcRequest.Tool + ", id=" + rpcRequest.Id + ".");
            var response = _broker.Invoke(rpcRequest, TimeSpan.FromSeconds(UserSettings.Load().RequestTimeoutSeconds));
            RuntimeLogger.Info("RPC completed tool=" + rpcRequest.Tool + ", id=" + rpcRequest.Id + ", ok=" + response.Ok + ".");
            WriteResponse(stream, 200, response);
        }

        private static HttpRequest? ReadRequest(NetworkStream stream)
        {
            var headerBytes = new List<byte>(1024);
            var matched = 0;
            var terminator = new byte[] { 13, 10, 13, 10 };
            while (headerBytes.Count < MaxHeaderBytes)
            {
                var value = stream.ReadByte();
                if (value < 0) return null;
                var b = (byte)value;
                headerBytes.Add(b);
                if (b == terminator[matched])
                {
                    matched++;
                    if (matched == terminator.Length) break;
                }
                else
                {
                    matched = b == terminator[0] ? 1 : 0;
                }
            }
            if (matched != terminator.Length) throw new InvalidDataException("HTTP header too large.");

            var headerText = Encoding.ASCII.GetString(headerBytes.ToArray());
            var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length == 0) return null;
            var requestLine = lines[0].Split(' ');
            if (requestLine.Length < 2) return null;
            var method = requestLine[0];
            var target = requestLine[1];
            var queryIndex = target.IndexOf('?');
            var path = queryIndex >= 0 ? target.Substring(0, queryIndex) : target;

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrEmpty(line)) break;
                var colon = line.IndexOf(':');
                if (colon <= 0) continue;
                headers[line.Substring(0, colon).Trim()] = line.Substring(colon + 1).Trim();
            }

            var contentLength = 0;
            if (headers.TryGetValue("Content-Length", out var contentLengthText))
            {
                if (!int.TryParse(contentLengthText, out contentLength) || contentLength < 0) throw new InvalidDataException("Invalid Content-Length.");
                if (contentLength > MaxRequestBytes) throw new InvalidDataException("Request too large.");
            }
            if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) && contentLength <= 0)
                throw new InvalidDataException("POST requires Content-Length.");

            var bodyBytes = new byte[contentLength];
            var offset = 0;
            while (offset < contentLength)
            {
                var read = stream.Read(bodyBytes, offset, contentLength - offset);
                if (read <= 0) throw new EndOfStreamException("Unexpected end of request body.");
                offset += read;
            }

            headers.TryGetValue("Authorization", out var authorization);
            return new HttpRequest(method, path, authorization ?? "", Encoding.UTF8.GetString(bodyBytes));
        }

        private bool TokenOk(string header)
        {
            const string prefix = "Bearer ";
            if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            var supplied = Encoding.UTF8.GetBytes(header.Substring(prefix.Length));
            var expected = Encoding.UTF8.GetBytes(_token);
            if (supplied.Length != expected.Length) return false;
            var diff = 0;
            for (var i = 0; i < supplied.Length; i++) diff |= supplied[i] ^ expected[i];
            return diff == 0;
        }

        private static void WriteResponse(NetworkStream stream, int status, object value)
        {
            try
            {
                var body = JsonCodec.SerializeUtf8(value);
                var statusText = StatusText(status);
                var header = Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 {status} {statusText}\r\n" +
                    "Content-Type: application/json; charset=utf-8\r\n" +
                    $"Content-Length: {body.Length}\r\n" +
                    "Connection: close\r\n" +
                    "Cache-Control: no-store\r\n\r\n");
                stream.Write(header, 0, header.Length);
                stream.Write(body, 0, body.Length);
                stream.Flush();
            }
            catch { }
        }

        private static string StatusText(int status)
        {
            switch (status)
            {
                case 200: return "OK";
                case 400: return "Bad Request";
                case 401: return "Unauthorized";
                case 404: return "Not Found";
                case 413: return "Payload Too Large";
                default: return "Internal Server Error";
            }
        }

        private static string CreateToken()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        public void Dispose()
        {
            RuntimeLogger.Info("Gateway stopping on port " + Port + ".");
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }
            _listener = null;
            try { _loopTask?.Wait(500); } catch { }
            _loopTask = null;
            Port = 0;
            try { _cts?.Dispose(); } catch { }
            _cts = null;
            InstanceRegistry.DeleteCurrent();
        }

        private sealed class HttpRequest
        {
            public string Method { get; }
            public string Path { get; }
            public string Authorization { get; }
            public string Body { get; }
            public HttpRequest(string method, string path, string authorization, string body)
            {
                Method = method;
                Path = path;
                Authorization = authorization;
                Body = body;
            }
        }
    }
}
