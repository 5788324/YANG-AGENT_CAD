using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
#if YANGCAD_NETFRAMEWORK
using System.Web.Script.Serialization;
#else
using System.Text.Json;
#endif

namespace YangCAD.BatchWorker
{
    internal static class BatchJson
    {
#if YANGCAD_NETFRAMEWORK
        private static JavaScriptSerializer CreateSerializer() => new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 };
#endif

        public static string Serialize(object value)
        {
#if YANGCAD_NETFRAMEWORK
            return CreateSerializer().Serialize(value);
#else
            return JsonSerializer.Serialize(value);
#endif
        }

        public static Dictionary<string, object?> ParseObject(string json)
        {
#if YANGCAD_NETFRAMEWORK
            var raw = CreateSerializer().DeserializeObject(json);
            return ToDictionary(raw) ?? throw new FormatException("JSON root must be an object.");
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

        private static Dictionary<string, object?>? ToDictionary(object? value)
        {
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
            var map = ToDictionary(value);
            if (map != null) return map;
            if (value is IList list)
            {
                var result = new List<object?>();
                foreach (var item in list) result.Add(Normalize(item));
                return result;
            }
            return value;
        }

        public static string RequiredString(IReadOnlyDictionary<string, object?> map, string key)
        {
            if (!map.TryGetValue(key, out var raw) || !(raw is string text) || string.IsNullOrWhiteSpace(text))
                throw new ArgumentException($"Missing non-empty string: {key}");
            return text.Trim();
        }

        public static string? String(IReadOnlyDictionary<string, object?> map, string key)
            => map.TryGetValue(key, out var raw) && raw is string text ? text : null;

        public static bool Bool(IReadOnlyDictionary<string, object?> map, string key, bool fallback)
        {
            if (!map.TryGetValue(key, out var raw) || raw == null) return fallback;
            if (raw is bool b) return b;
            if (bool.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), out var parsed)) return parsed;
            throw new ArgumentException($"Invalid bool: {key}");
        }

        public static int Int(IReadOnlyDictionary<string, object?> map, string key, int fallback)
        {
            if (!map.TryGetValue(key, out var raw) || raw == null) return fallback;
            return Convert.ToInt32(raw, CultureInfo.InvariantCulture);
        }

        public static double Double(IReadOnlyDictionary<string, object?> map, string key, double fallback)
        {
            if (!map.TryGetValue(key, out var raw) || raw == null) return fallback;
            return Convert.ToDouble(raw, CultureInfo.InvariantCulture);
        }
    }
}
