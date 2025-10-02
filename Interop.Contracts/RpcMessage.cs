#nullable enable
using System.Text.Json;

namespace Interop.Contracts
{
    public class RpcRequest
    {
        public string Action { get; set; } = string.Empty;
        public JsonElement? Payload { get; set; }
    }

    public class RpcResponse
    {
        public bool Success { get; set; }
        public JsonElement? Result { get; set; }
        public string? Error { get; set; }
    }
}

