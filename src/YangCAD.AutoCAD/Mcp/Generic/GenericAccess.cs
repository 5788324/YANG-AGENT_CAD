using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Mcp.Generic
{
    /// <summary>
    /// Reflection bridge used by the generic MCP layer. It intentionally supports only
    /// public Autodesk object members and JSON-friendly value types. Unsupported members
    /// remain discoverable but are marked non-writable/non-invokable instead of being forced.
    /// </summary>
    internal static class GenericAccess
    {
        private static readonly HashSet<string> DeniedPropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ObjectId", "OwnerId", "Database", "Document", "TransactionManager", "ExtensionDictionary",
            "XData", "AcadObject", "UnmanagedObject", "Handle", "IsDisposed", "IsWriteEnabled", "IsReadEnabled"
        };

        private static readonly HashSet<string> DeniedMethodNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Dispose", "Erase", "UpgradeOpen", "DowngradeOpen", "Close", "Cancel", "DeleteUnmanagedObject",
            "SwapIdWith", "HandOverTo", "SetObjectIdsInFlux", "AssertWriteEnabled", "AssertReadEnabled",
            "SetDatabaseDefaults", "GetHashCode", "GetType", "MemberwiseClone", "Finalize"
        };

        public static DBObject OpenByHandle(Transaction transaction, Database database, string handle, OpenMode mode)
        {
            var id = EntityHelpers.FromHandle(database, handle);
            return transaction.GetObject(id, mode) ?? throw new InvalidOperationException("Handle 对应对象不存在。");
        }

        public static Dictionary<string, object?> DescribeObject(DBObject obj, bool includeValues, bool includeMethods, int maxMembers)
        {
            maxMembers = Math.Max(10, Math.Min(maxMembers, 240));
            var type = obj.GetType();
            var result = new Dictionary<string, object?>
            {
                ["handle"] = obj.ObjectId.IsNull ? "" : obj.Handle.ToString(),
                ["runtime_type"] = type.FullName ?? type.Name,
                ["assembly"] = type.Assembly.GetName().Name ?? "",
                ["is_entity"] = obj is Entity,
                ["is_autodesk_type"] = IsAutodeskType(type),
            };

            if (obj is Entity entity)
            {
                result["dxf_type"] = entity.GetRXClass().DxfName;
                result["layer"] = entity.Layer;
            }

            result["properties"] = DescribeProperties(obj, includeValues, maxMembers);
            if (includeMethods) result["methods"] = DescribeMethods(type, null, maxMembers);
            return result;
        }

        public static List<object> DescribeProperties(object target, bool includeValues, int maxMembers)
        {
            var rows = new List<object>();
            foreach (var property in target.GetType()
                         .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                         .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                         .Take(maxMembers))
            {
                var indexed = property.GetIndexParameters().Length != 0;
                var safeRead = !indexed && property.GetMethod != null && property.GetMethod.IsPublic && CanSerializeType(property.PropertyType);
                var safeWrite = IsSafeWritableProperty(property);
                object? value = null;
                string? valueError = null;
                if (includeValues && safeRead)
                {
                    try { value = ToSerializable(property.GetValue(target, null)); }
                    catch (Exception ex) { valueError = Unwrap(ex).Message; }
                }

                rows.Add(new Dictionary<string, object?>
                {
                    ["name"] = property.Name,
                    ["type"] = TypeName(property.PropertyType),
                    ["declared_by"] = property.DeclaringType?.FullName ?? "",
                    ["readable"] = property.CanRead,
                    ["writable"] = property.CanWrite,
                    ["safe_read"] = safeRead,
                    ["safe_write"] = safeWrite,
                    ["indexed"] = indexed,
                    ["value"] = includeValues && safeRead ? value : null,
                    ["value_error"] = valueError,
                });
            }
            return rows;
        }

        public static List<object> DescribeMethods(Type type, string? nameFilter, int maxMembers)
        {
            var rows = new List<object>();
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => !m.IsSpecialName)
                .Where(m => string.IsNullOrWhiteSpace(nameFilter) || string.Equals(m.Name, nameFilter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(m => m.GetParameters().Length)
                .Take(Math.Max(10, Math.Min(maxMembers, 240)));

            foreach (var method in methods)
            {
                var policy = MethodPolicy(method);
                rows.Add(new Dictionary<string, object?>
                {
                    ["name"] = method.Name,
                    ["signature"] = Signature(method),
                    ["declared_by"] = method.DeclaringType?.FullName ?? "",
                    ["return_type"] = TypeName(method.ReturnType),
                    ["parameters"] = method.GetParameters().Select(p => new Dictionary<string, object?>
                    {
                        ["name"] = p.Name ?? "",
                        ["type"] = TypeName(p.ParameterType),
                        ["optional"] = p.IsOptional,
                        ["by_ref"] = p.ParameterType.IsByRef,
                    }).ToArray(),
                    ["invokable"] = policy.Allowed,
                    ["reason"] = policy.Reason,
                });
            }
            return rows;
        }

        public static PropertyInfo GetPropertyForRead(Type type, string name)
        {
            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
                ?? throw new ArgumentException("未找到公共属性：" + name);
            if (property.GetIndexParameters().Length != 0) throw new InvalidOperationException("暂不支持索引属性：" + property.Name);
            if (property.GetMethod == null || !property.GetMethod.IsPublic) throw new InvalidOperationException("属性不可读：" + property.Name);
            if (!CanSerializeType(property.PropertyType)) throw new InvalidOperationException("属性类型暂不能安全序列化：" + TypeName(property.PropertyType));
            return property;
        }

        public static PropertyInfo GetPropertyForWrite(Type type, string name)
        {
            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
                ?? throw new ArgumentException("未找到公共属性：" + name);
            if (!IsSafeWritableProperty(property))
                throw new InvalidOperationException("属性存在但不在通用安全写入范围：" + property.Name + " (" + TypeName(property.PropertyType) + ")。请改用专用 Tool 或扩展。 ");
            return property;
        }

        public static object? ReadProperty(object target, PropertyInfo property)
            => ToSerializable(property.GetValue(target, null));

        public static object? ConvertFor(Type targetType, object? raw, Database database)
        {
            var nullable = Nullable.GetUnderlyingType(targetType);
            if (nullable != null)
            {
                if (raw == null) return null;
                targetType = nullable;
            }

            if (raw == null)
            {
                if (!targetType.IsValueType) return null;
                throw new ArgumentException("不能把 null 赋给 " + TypeName(targetType));
            }

            if (targetType.IsInstanceOfType(raw)) return raw;
            if (targetType == typeof(string)) return Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "";
            if (targetType == typeof(bool))
            {
                if (raw is bool b) return b;
                if (raw is string bs && bool.TryParse(bs, out var parsed)) return parsed;
                throw new ArgumentException("布尔值必须为 true/false。");
            }
            if (targetType.IsEnum)
            {
                if (raw is string text) return Enum.Parse(targetType, text, true);
                var numeric = Convert.ToInt64(raw, CultureInfo.InvariantCulture);
                return Enum.ToObject(targetType, numeric);
            }

            if (IsNumericType(targetType)) return Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
            if (targetType == typeof(Point2d)) { var a = NumberArray(raw, 2, 2); return new Point2d(a[0], a[1]); }
            if (targetType == typeof(Point3d)) { var a = NumberArray(raw, 2, 3); return new Point3d(a[0], a[1], a.Length > 2 ? a[2] : 0); }
            if (targetType == typeof(Vector2d)) { var a = NumberArray(raw, 2, 2); return new Vector2d(a[0], a[1]); }
            if (targetType == typeof(Vector3d)) { var a = NumberArray(raw, 2, 3); return new Vector3d(a[0], a[1], a.Length > 2 ? a[2] : 0); }
            if (targetType == typeof(Handle))
            {
                var text = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "";
                return new Handle(Convert.ToInt64(text, 16));
            }
            if (targetType == typeof(ObjectId))
            {
                var text = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "";
                return EntityHelpers.FromHandle(database, text);
            }

            throw new ArgumentException("暂不支持把 JSON 值转换为 " + TypeName(targetType));
        }

        public static MethodInfo ResolveMethod(Type type, string methodName, string? signature, IList<object?> rawArgs, Database database, out object?[] converted)
        {
            var candidates = type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => !m.IsSpecialName && string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase))
                .Where(m => MethodPolicy(m).Allowed)
                .Where(m => string.IsNullOrWhiteSpace(signature) || string.Equals(Signature(m), signature, StringComparison.Ordinal))
                .Where(m => m.GetParameters().Length == rawArgs.Count)
                .ToList();

            var matches = new List<Tuple<MethodInfo, object?[]>>();
            foreach (var candidate in candidates)
            {
                try
                {
                    var parameters = candidate.GetParameters();
                    var values = new object?[parameters.Length];
                    for (var i = 0; i < parameters.Length; i++) values[i] = ConvertFor(parameters[i].ParameterType, rawArgs[i], database);
                    matches.Add(Tuple.Create(candidate, values));
                }
                catch { }
            }

            if (matches.Count == 0)
                throw new InvalidOperationException("没有找到参数可匹配且允许调用的方法。请先使用 cad.api.describe 查看可调用 signature。");
            if (matches.Count > 1)
                throw new InvalidOperationException("方法重载存在歧义，请传入 cad.api.describe 返回的 signature 精确指定。");
            converted = matches[0].Item2;
            return matches[0].Item1;
        }

        public static object? ToSerializable(object? value)
        {
            if (value == null) return null;
            var type = value.GetType();
            if (type.IsEnum) return value.ToString();
            if (value is string || value is bool || IsNumericType(type)) return value;
            if (value is Point2d p2) return new[] { p2.X, p2.Y };
            if (value is Point3d p3) return new[] { p3.X, p3.Y, p3.Z };
            if (value is Vector2d v2) return new[] { v2.X, v2.Y };
            if (value is Vector3d v3) return new[] { v3.X, v3.Y, v3.Z };
            if (value is Extents3d ex) return new Dictionary<string, object?> { ["min"] = new[] { ex.MinPoint.X, ex.MinPoint.Y, ex.MinPoint.Z }, ["max"] = new[] { ex.MaxPoint.X, ex.MaxPoint.Y, ex.MaxPoint.Z } };
            if (value is Handle handle) return handle.ToString();
            if (value is ObjectId id)
            {
                if (id.IsNull) return null;
                try { return new Dictionary<string, object?> { ["handle"] = id.Handle.ToString(), ["is_erased"] = id.IsErased }; }
                catch { return new Dictionary<string, object?> { ["object_id"] = id.ToString() }; }
            }
            if (value is IEnumerable enumerable && !(value is string))
            {
                var list = new List<object?>();
                foreach (var item in enumerable)
                {
                    if (list.Count >= 64) break;
                    if (item == null || CanSerializeType(item.GetType())) list.Add(ToSerializable(item));
                    else list.Add("<" + TypeName(item.GetType()) + ">");
                }
                return list;
            }
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "<" + TypeName(type) + ">";
        }

        private static bool IsSafeWritableProperty(PropertyInfo property)
        {
            if (property.GetIndexParameters().Length != 0) return false;
            if (DeniedPropertyNames.Contains(property.Name)) return false;
            if (property.SetMethod == null || !property.SetMethod.IsPublic) return false;
            if (property.DeclaringType == null || !IsAutodeskType(property.DeclaringType)) return false;
            return CanWriteType(property.PropertyType);
        }

        private static MethodPolicyResult MethodPolicy(MethodInfo method)
        {
            if (method.IsStatic) return new MethodPolicyResult(false, "static method");
            if (method.IsGenericMethodDefinition || method.ContainsGenericParameters) return new MethodPolicyResult(false, "generic method");
            if (DeniedMethodNames.Contains(method.Name)) return new MethodPolicyResult(false, "blocked infrastructure/dangerous method");
            if (method.DeclaringType == null || !IsAutodeskType(method.DeclaringType)) return new MethodPolicyResult(false, "not an Autodesk API member");
            if (method.ReturnType != typeof(void) && !CanSerializeType(method.ReturnType)) return new MethodPolicyResult(false, "return type is not JSON-safe");
            foreach (var p in method.GetParameters())
            {
                if (p.IsOut || p.ParameterType.IsByRef || p.ParameterType.IsPointer) return new MethodPolicyResult(false, "ref/out/pointer parameter");
                if (!CanInvokeParameterType(p.ParameterType)) return new MethodPolicyResult(false, "unsupported parameter type: " + TypeName(p.ParameterType));
            }
            return new MethodPolicyResult(true, "");
        }

        private static bool CanSerializeType(Type type)
        {
            var nullable = Nullable.GetUnderlyingType(type);
            if (nullable != null) type = nullable;
            return type == typeof(void) || type == typeof(string) || type == typeof(bool) || type.IsEnum || IsNumericType(type)
                || type == typeof(Point2d) || type == typeof(Point3d) || type == typeof(Vector2d) || type == typeof(Vector3d)
                || type == typeof(Extents3d) || type == typeof(ObjectId) || type == typeof(Handle);
        }

        private static bool CanWriteType(Type type)
        {
            var nullable = Nullable.GetUnderlyingType(type);
            if (nullable != null) type = nullable;
            return type == typeof(string) || type == typeof(bool) || type.IsEnum || IsNumericType(type)
                || type == typeof(Point2d) || type == typeof(Point3d) || type == typeof(Vector2d) || type == typeof(Vector3d);
        }

        private static bool CanInvokeParameterType(Type type)
        {
            var nullable = Nullable.GetUnderlyingType(type);
            if (nullable != null) type = nullable;
            return CanWriteType(type) || type == typeof(ObjectId) || type == typeof(Handle);
        }

        private static bool IsNumericType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte: case TypeCode.SByte: case TypeCode.Int16: case TypeCode.UInt16:
                case TypeCode.Int32: case TypeCode.UInt32: case TypeCode.Int64: case TypeCode.UInt64:
                case TypeCode.Single: case TypeCode.Double: case TypeCode.Decimal: return true;
                default: return false;
            }
        }

        private static bool IsAutodeskType(Type type)
        {
            var ns = type.Namespace ?? "";
            return ns.StartsWith("Autodesk.AutoCAD.", StringComparison.Ordinal) || ns.StartsWith("Autodesk.Civil.", StringComparison.Ordinal);
        }

        private static double[] NumberArray(object raw, int min, int max)
        {
            if (raw is string || !(raw is IEnumerable source)) throw new ArgumentException("坐标/向量必须是数值数组。");
            var values = new List<double>();
            foreach (var item in source)
            {
                if (item == null) throw new ArgumentException("坐标/向量不能包含 null。");
                values.Add(Convert.ToDouble(item, CultureInfo.InvariantCulture));
            }
            if (values.Count < min || values.Count > max) throw new ArgumentException("坐标/向量数组长度无效。");
            return values.ToArray();
        }

        private static string Signature(MethodInfo method)
            => method.Name + "(" + string.Join(",", method.GetParameters().Select(p => TypeName(p.ParameterType))) + ")";

        private static string TypeName(Type type)
        {
            if (type.IsByRef) return TypeName(type.GetElementType() ?? type) + "&";
            var nullable = Nullable.GetUnderlyingType(type);
            if (nullable != null) return TypeName(nullable) + "?";
            return type.FullName ?? type.Name;
        }

        private static Exception Unwrap(Exception exception)
            => exception is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : exception;

        private sealed class MethodPolicyResult
        {
            public bool Allowed { get; }
            public string Reason { get; }
            public MethodPolicyResult(bool allowed, string reason) { Allowed = allowed; Reason = reason; }
        }
    }
}
