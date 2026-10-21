using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
#if YANGCAD_NETFRAMEWORK
using System.Web.Script.Serialization;
#else
using System.Text.Json;
#endif
using YangCAD.Core.Rpc;

namespace YangCAD.AutoCADHost.Infrastructure
{
    /// <summary>
    /// Keeps AutoCAD 2021-2024 (net48) free of System.Text.Json runtime dependencies.
    /// AutoCAD 2025+ uses the JSON implementation included with the .NET runtime.
    /// </summary>
    internal static class JsonCodec
    {
#if YANGCAD_NETFRAMEWORK
        private static JavaScriptSerializer CreateSerializer() => new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };
#endif

        public static string Serialize(object value, bool indented = false)
        {
#if YANGCAD_NETFRAMEWORK
            // JavaScriptSerializer is framework-provided and does not require plugin-local DLLs.
            // It does not support pretty-printing; compact JSON is intentional for reliability.
            return CreateSerializer().Serialize(value);
#else
            return JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = indented });
#endif
        }

        public static byte[] SerializeUtf8(object value)
            => System.Text.Encoding.UTF8.GetBytes(Serialize(value));

        public static T? Deserialize<T>(string json)
        {
#if YANGCAD_NETFRAMEWORK
            return CreateSerializer().Deserialize<T>(json);
#else
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
#endif
        }

        public static RpcRequest ParseRpcRequest(string json)
        {
            var root = ParseObject(json);
            var request = new RpcRequest
            {
                Id = GetString(root, "id") ?? "",
                Tool = GetString(root, "tool") ?? "",
                ExpectedDocument = GetString(root, "expectedDocument") ?? "",
                ExpectedDocumentId = GetString(root, "expectedDocumentId") ?? "",
                Args = GetObject(root, "args") ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            };
            return request;
        }

        private static Dictionary<string, object?> ParseObject(string json)
        {
#if YANGCAD_NETFRAMEWORK
            var raw = CreateSerializer().DeserializeObject(json);
            return ToObjectDictionary(raw) ?? throw new FormatException("JSON root must be an object.");
#else
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new FormatException("JSON root must be an object.");
            return (Dictionary<string, object?>)ConvertElement(doc.RootElement)!;
#endif
        }

#if !YANGCAD_NETFRAMEWORK
        private static object? ConvertElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var property in element.EnumerateObject()) map[property.Name] = ConvertElement(property.Value);
                    return map;
                case JsonValueKind.Array:
                    var list = new List<object?>();
                    foreach (var item in element.EnumerateArray()) list.Add(ConvertElement(item));
                    return list;
                case JsonValueKind.String: return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt64(out var l)) return l;
                    return element.GetDouble();
                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                case JsonValueKind.Null:
                case JsonValueKind.Undefined: return null;
                default: return null;
            }
        }
#endif

        private static Dictionary<string, object?>? ToObjectDictionary(object? value)
        {
            if (value is Dictionary<string, object> direct)
            {
                var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in direct) result[pair.Key] = Normalize(pair.Value);
                return result;
            }
            if (value is IDictionary dictionary)
            {
                var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is string key) result[key] = Normalize(entry.Value);
                }
                return result;
            }
            return null;
        }

        private static object? Normalize(object? value)
        {
            var map = ToObjectDictionary(value);
            if (map != null) return map;
            if (value is Array array)
            {
                var list = new List<object?>();
                foreach (var item in array) list.Add(Normalize(item));
                return list;
            }
            if (value is IList source)
            {
                var list = new List<object?>();
                foreach (var item in source) list.Add(Normalize(item));
                return list;
            }
            return value;
        }

        private static string? GetString(IReadOnlyDictionary<string, object?> map, string key)
            => map.TryGetValue(key, out var value) && value is string text ? text : null;

        private static Dictionary<string, object?>? GetObject(IReadOnlyDictionary<string, object?> map, string key)
            => map.TryGetValue(key, out var value) ? ToObjectDictionary(value) ?? value as Dictionary<string, object?> : null;
    }
}
