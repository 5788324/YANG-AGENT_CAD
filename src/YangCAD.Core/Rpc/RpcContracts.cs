using System;
using System.Collections.Generic;

namespace YangCAD.Core.Rpc
{
    public sealed class RpcRequest
    {
        public string Id { get; set; } = "";
        public string Tool { get; set; } = "";
        public string ExpectedDocument { get; set; } = "";
        public string ExpectedDocumentId { get; set; } = "";
        public Dictionary<string, object?> Args { get; set; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class RpcResponse
    {
        public string Id { get; set; } = "";
        public bool Ok { get; set; }
        public string Tool { get; set; } = "";
        public object? Data { get; set; }
        public string? Error { get; set; }
        public string[] Warnings { get; set; } = Array.Empty<string>();

        public static RpcResponse Success(string id, string tool, object? data, params string[] warnings)
            => new RpcResponse { Id = id, Ok = true, Tool = tool, Data = data, Warnings = warnings };
        public static RpcResponse Failure(string id, string tool, string error, params string[] warnings)
            => new RpcResponse { Id = id, Ok = false, Tool = tool, Error = error, Warnings = warnings };
    }

    public sealed class InstanceDescriptor
    {
        public int ProcessId { get; set; }
        public int Port { get; set; }
        public string Token { get; set; } = "";
        public string Product { get; set; } = "";
        public string Version { get; set; } = "";
        public string Document { get; set; } = "";
        public string DocumentId { get; set; } = "";
        public string StartedAt { get; set; } = "";
        public string UpdatedAt { get; set; } = "";
    }
}
