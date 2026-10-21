using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace YangCAD.AutoCADHost.Mcp.Tools
{
    public static class ToolJson
    {
        public static bool Has(IReadOnlyDictionary<string, object?> args, string name) => args.ContainsKey(name);

        public static string? String(IReadOnlyDictionary<string, object?> args, string name)
        {
            if (!args.TryGetValue(name, out var value) || value == null) return null;
            if (value is string text) return text;
            throw new ArgumentException($"参数 {name} 必须是字符串。");
        }

        public static string RequiredString(IReadOnlyDictionary<string, object?> args, string name)
        {
            var value = String(args, name);
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"缺少参数：{name}");
            return value!;
        }


        public static bool? Bool(IReadOnlyDictionary<string, object?> args, string name)
        {
            if (!args.TryGetValue(name, out var value) || value == null) return null;
            if (value is bool result) return result;
            throw new ArgumentException($"参数 {name} 必须是布尔值。");
        }

        public static List<object?>? List(IReadOnlyDictionary<string, object?> args, string name)
        {
            if (!args.TryGetValue(name, out var raw) || raw == null) return null;
            if (raw is string || !(raw is IEnumerable source)) throw new ArgumentException($"参数 {name} 必须是数组。");
            var result = new List<object?>();
            foreach (var item in source) result.Add(item);
            return result;
        }

        public static Dictionary<string, object?>? Object(IReadOnlyDictionary<string, object?> args, string name)
        {
            if (!args.TryGetValue(name, out var raw) || raw == null) return null;
            if (raw is Dictionary<string, object?> typed) return new Dictionary<string, object?>(typed, StringComparer.OrdinalIgnoreCase);
            if (raw is IDictionary dictionary)
            {
                var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is string key) result[key] = entry.Value;
                }
                return result;
            }
            throw new ArgumentException($"参数 {name} 必须是对象。");
        }

        public static int? Int(IReadOnlyDictionary<string, object?> args, string name)
        {
            if (!args.TryGetValue(name, out var value) || value == null) return null;
            if (TryInt(value, out var result)) return result;
            throw new ArgumentException($"参数 {name} 必须是整数。");
        }

        public static string? OptionalNonBlankString(IReadOnlyDictionary<string, object?> args, string name)
        {
            if (!args.TryGetValue(name, out var value) || value == null) return null;
            if (!(value is string text)) throw new ArgumentException($"参数 {name} 必须是字符串。");
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException($"参数 {name} 不能为空白字符串。");
            return text;
        }

        public static double? Double(IReadOnlyDictionary<string, object?> args, string name)
        {
            if (!args.TryGetValue(name, out var value) || value == null) return null;
            if (TryDouble(value, out var result)) return result;
            throw new ArgumentException($"参数 {name} 必须是数值。");
        }

        public static double[]? DoubleArray(IReadOnlyDictionary<string, object?> args, string name)
        {
            if (!args.TryGetValue(name, out var raw) || raw == null) return null;
            if (raw is string || !(raw is IEnumerable source)) throw new ArgumentException($"参数 {name} 必须是数值数组。");
            var values = new List<double>();
            foreach (var item in source)
            {
                if (item == null || !TryDouble(item, out var value)) throw new ArgumentException($"参数 {name} 必须全部为数值。");
                values.Add(value);
            }
            return values.ToArray();
        }

        public static short? AciColor(IReadOnlyDictionary<string, object?> args, string name)
        {
            if (!args.TryGetValue(name, out var raw) || raw == null) return null;
            if (!TryInt(raw, out var value)) throw new ArgumentException($"参数 {name} 必须是整数。");
            if (value < 0 || value > 256) throw new ArgumentOutOfRangeException(name, "ACI color_index 必须在 0–256 之间。");
            return (short)value;
        }

        private static bool TryInt(object value, out int result)
        {
            switch (value)
            {
                case int i: result = i; return true;
                case long l when l >= int.MinValue && l <= int.MaxValue: result = (int)l; return true;
                case short s: result = s; return true;
                case byte b: result = b; return true;
                case decimal m when decimal.Truncate(m) == m && m >= int.MinValue && m <= int.MaxValue: result = (int)m; return true;
                case double d when !double.IsNaN(d) && !double.IsInfinity(d) && Math.Truncate(d) == d && d >= int.MinValue && d <= int.MaxValue: result = (int)d; return true;
                case float f when !float.IsNaN(f) && !float.IsInfinity(f) && Math.Truncate(f) == f && f >= int.MinValue && f <= int.MaxValue: result = (int)f; return true;
                default: result = 0; return false;
            }
        }

        private static bool TryDouble(object value, out double result)
        {
            switch (value)
            {
                case double d: result = d; return !double.IsNaN(d) && !double.IsInfinity(d);
                case float f: result = f; return !float.IsNaN(f) && !float.IsInfinity(f);
                case decimal m: result = (double)m; return true;
                case long l: result = l; return true;
                case int i: result = i; return true;
                case short s: result = s; return true;
                case byte b: result = b; return true;
                default:
                    try
                    {
                        result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                        return !double.IsNaN(result) && !double.IsInfinity(result);
                    }
                    catch { result = 0; return false; }
            }
        }
    }
}
