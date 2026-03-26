using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SelfDiagnostic.Models;

namespace SelfDiagnostic.Services
{
    /// <summary>
    /// Universal method invoker that can call ANY .NET method via reflection.
    /// Supports arbitrary method signatures by auto-mapping parameters from step.Params
    /// and well-known context objects (DiagnosticItem, RunbookStepDefinition, etc.).
    /// </summary>
    public sealed class GenericMethodInvoker
    {
        private readonly PluginAssemblyLoader _assemblyLoader;

        private static readonly HashSet<string> SkipMethodNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "ToString", "Equals", "GetHashCode", "GetType", "Finalize", "MemberwiseClone",
            "ReferenceEquals"
        };

        private static readonly HashSet<string> FrameworkAssemblyPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System", "mscorlib", "Microsoft", "DevExpress", "Newtonsoft", "netstandard"
        };

        public GenericMethodInvoker(PluginAssemblyLoader assemblyLoader)
        {
            _assemblyLoader = assemblyLoader ?? throw new ArgumentNullException(nameof(assemblyLoader));
        }

        /// <summary>
        /// Invoke an arbitrary method identified by BindDll + BindMethod.
        /// Parameters are auto-mapped from context objects and step.Params.
        /// Return values are automatically adapted to CheckExecutionOutcome.
        /// </summary>
        public async Task<CheckExecutionOutcome> InvokeAsync(
            string bindDll,
            string bindMethod,
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            var assembly = _assemblyLoader.Load(bindDll);
            if (assembly == null)
            {
                item.Status = CheckStatus.Fail;
                item.Detail = string.Format("Cannot load assembly: {0}", bindDll);
                return new CheckExecutionOutcome { Success = false };
            }

            if (!TryParseBindMethod(bindMethod, out var typeName, out var methodName))
            {
                item.Status = CheckStatus.Fail;
                item.Detail = string.Format("Invalid BindMethod format: {0} (expected Namespace.Type.Method)", bindMethod);
                return new CheckExecutionOutcome { Success = false };
            }

            var type = ResolveType(assembly, typeName);
            if (type == null)
            {
                item.Status = CheckStatus.Fail;
                item.Detail = string.Format("Type not found: {0} in {1}", typeName, assembly.GetName().Name);
                return new CheckExecutionOutcome { Success = false };
            }

            var method = FindMethod(type, methodName);
            if (method == null)
            {
                item.Status = CheckStatus.Fail;
                item.Detail = string.Format("Method not found: {0}.{1}", type.FullName, methodName);
                return new CheckExecutionOutcome { Success = false };
            }

            object instance = null;
            if (!method.IsStatic)
            {
                instance = TryCreateInstance(type);
                if (instance == null)
                {
                    item.Status = CheckStatus.Fail;
                    item.Detail = string.Format("Cannot instantiate: {0} (no parameterless constructor)", type.FullName);
                    return new CheckExecutionOutcome { Success = false };
                }
            }

            object[] args;
            try
            {
                args = MapParameters(method, item, step, runContext, ct);
            }
            catch (Exception ex)
            {
                item.Status = CheckStatus.Fail;
                item.Detail = string.Format("Parameter mapping failed for {0}.{1}: {2}",
                    type.FullName, methodName, ex.Message);
                return new CheckExecutionOutcome { Success = false };
            }

            try
            {
                var rawResult = method.Invoke(instance, args);
                return await AdaptReturnValue(rawResult, item);
            }
            catch (TargetInvocationException tex)
            {
                var inner = tex.InnerException ?? tex;
                if (inner is OperationCanceledException && ct.IsCancellationRequested)
                    throw inner;

                item.Status = CheckStatus.Fail;
                item.Detail = string.Format("{0}.{1} threw: {2}", type.FullName, methodName, inner.Message);
                return new CheckExecutionOutcome { Success = false };
            }
        }

        // ──────────────────────────────────────────────────────────────
        //  Assembly / Method browsing (for RunbookEditor picker UI)
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Scan an assembly and return CheckExecutorInfo for every callable method.
        /// Skips framework base methods, property accessors, and event accessors.
        /// </summary>
        public static IReadOnlyList<CheckExecutorInfo> BrowseAssemblyMethods(Assembly assembly)
        {
            var results = new List<CheckExecutorInfo>();
            var asmName = assembly.GetName().Name ?? string.Empty;

            foreach (var type in SafeGetTypes(assembly))
            {
                var flags = BindingFlags.Public | BindingFlags.NonPublic
                          | BindingFlags.Static | BindingFlags.Instance
                          | BindingFlags.DeclaredOnly;

                foreach (var method in type.GetMethods(flags))
                {
                    if (method.IsSpecialName) continue;
                    if (SkipMethodNames.Contains(method.Name)) continue;
                    if (method.IsAbstract) continue;

                    results.Add(new CheckExecutorInfo
                    {
                        CheckId = string.Empty,
                        DisplayName = method.Name,
                        Description = FormatSignature(method),
                        DefaultCategory = string.Empty,
                        MethodName = method.Name,
                        ClassName = type.FullName ?? type.Name,
                        PluginAssembly = asmName
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Browse all non-framework assemblies currently loaded in the PluginAssemblyLoader.
        /// </summary>
        public IReadOnlyList<CheckExecutorInfo> BrowseAllLoadedMethods()
        {
            var results = new List<CheckExecutorInfo>();

            foreach (var asm in _assemblyLoader.GetLoadedAssemblies())
            {
                var name = asm.GetName().Name ?? string.Empty;
                if (IsFrameworkAssembly(name)) continue;

                results.AddRange(BrowseAssemblyMethods(asm));
            }

            return results.OrderBy(i => i.PluginAssembly, StringComparer.OrdinalIgnoreCase)
                          .ThenBy(i => i.ClassName, StringComparer.OrdinalIgnoreCase)
                          .ThenBy(i => i.MethodName, StringComparer.OrdinalIgnoreCase)
                          .ToList();
        }

        // ──────────────────────────────────────────────────────────────
        //  BindMethod parsing
        // ──────────────────────────────────────────────────────────────

        private static bool TryParseBindMethod(string bindMethod, out string typeName, out string methodName)
        {
            typeName = null;
            methodName = null;
            if (string.IsNullOrWhiteSpace(bindMethod)) return false;

            var lastDot = bindMethod.LastIndexOf('.');
            if (lastDot <= 0 || lastDot >= bindMethod.Length - 1) return false;

            typeName = bindMethod.Substring(0, lastDot);
            methodName = bindMethod.Substring(lastDot + 1);
            return true;
        }

        // ──────────────────────────────────────────────────────────────
        //  Type / Method resolution
        // ──────────────────────────────────────────────────────────────

        private static Type ResolveType(Assembly assembly, string typeName)
        {
            var type = assembly.GetType(typeName, throwOnError: false, ignoreCase: true);
            if (type != null) return type;

            return SafeGetTypes(assembly).FirstOrDefault(t =>
                string.Equals(t.FullName, typeName, StringComparison.OrdinalIgnoreCase));
        }

        private static MethodInfo FindMethod(Type type, string methodName)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
                                     | BindingFlags.Static | BindingFlags.Instance;
            try
            {
                return type.GetMethod(methodName, flags);
            }
            catch (AmbiguousMatchException)
            {
                return type.GetMethods(flags)
                    .Where(m => string.Equals(m.Name, methodName, StringComparison.Ordinal))
                    .OrderByDescending(m => m.GetParameters().Length)
                    .FirstOrDefault();
            }
        }

        // ──────────────────────────────────────────────────────────────
        //  Parameter mapping
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Map method parameters from well-known context objects and step.Params.
        /// Priority: well-known injectable types → step.Params by name → default value.
        /// </summary>
        private static object[] MapParameters(
            MethodInfo method,
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            var parameters = method.GetParameters();
            var args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                var pType = p.ParameterType;

                if (pType == typeof(DiagnosticItem))
                    args[i] = item;
                else if (pType == typeof(RunbookStepDefinition))
                    args[i] = step;
                else if (pType == typeof(DiagnosticRunContext))
                    args[i] = runContext;
                else if (pType == typeof(CancellationToken))
                    args[i] = ct;
                else if (pType == typeof(Dictionary<string, string>))
                    args[i] = step.Params ?? new Dictionary<string, string>();
                else
                    args[i] = ResolveParamValue(p, step.Params);
            }

            return args;
        }

        private static object ResolveParamValue(ParameterInfo p, Dictionary<string, string> stepParams)
        {
            string rawValue;
            if (stepParams != null && stepParams.TryGetValue(p.Name, out rawValue))
                return ConvertValue(rawValue, p.ParameterType, p.Name);

            if (p.HasDefaultValue)
                return p.DefaultValue;

            if (!p.ParameterType.IsValueType || IsNullableType(p.ParameterType))
                return null;

            throw new InvalidOperationException(
                string.Format("Required parameter '{0}' (type: {1}) not found in step.Params and has no default value",
                    p.Name, p.ParameterType.Name));
        }

        // ──────────────────────────────────────────────────────────────
        //  Value conversion (string → target type)
        // ──────────────────────────────────────────────────────────────

        private static object ConvertValue(string raw, Type targetType, string paramName)
        {
            if (targetType == typeof(string))
                return raw;

            if (string.IsNullOrEmpty(raw))
            {
                if (!targetType.IsValueType || IsNullableType(targetType))
                    return null;
                return Activator.CreateInstance(targetType);
            }

            try
            {
                var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

                if (underlying == typeof(int)) return int.Parse(raw, CultureInfo.InvariantCulture);
                if (underlying == typeof(long)) return long.Parse(raw, CultureInfo.InvariantCulture);
                if (underlying == typeof(double)) return double.Parse(raw, CultureInfo.InvariantCulture);
                if (underlying == typeof(float)) return float.Parse(raw, CultureInfo.InvariantCulture);
                if (underlying == typeof(decimal)) return decimal.Parse(raw, CultureInfo.InvariantCulture);
                if (underlying == typeof(bool)) return ParseBool(raw);
                if (underlying == typeof(TimeSpan)) return TimeSpan.Parse(raw, CultureInfo.InvariantCulture);
                if (underlying == typeof(DateTime)) return DateTime.Parse(raw, CultureInfo.InvariantCulture);
                if (underlying == typeof(Guid)) return Guid.Parse(raw);
                if (underlying.IsEnum) return Enum.Parse(underlying, raw, ignoreCase: true);

                var converter = TypeDescriptor.GetConverter(underlying);
                if (converter.CanConvertFrom(typeof(string)))
                    return converter.ConvertFromInvariantString(raw);

                throw new InvalidOperationException(
                    string.Format("No conversion available from string to {0}", targetType.Name));
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    string.Format("Failed to convert parameter '{0}' value '{1}' to {2}: {3}",
                        paramName, raw, targetType.Name, ex.Message));
            }
        }

        private static bool ParseBool(string raw)
        {
            if (bool.TryParse(raw, out var b)) return b;
            if (raw == "1" || string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase)) return true;
            if (raw == "0" || string.Equals(raw, "no", StringComparison.OrdinalIgnoreCase)) return false;
            throw new FormatException("Cannot parse as bool: " + raw);
        }

        private static bool IsNullableType(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        // ──────────────────────────────────────────────────────────────
        //  Return value adaptation
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Adapt any return value to CheckExecutionOutcome.
        /// Supports: void, bool, Task, Task&lt;bool&gt;, Task&lt;CheckExecutionOutcome&gt;,
        /// CheckExecutionOutcome, and any other type (stored as Detail).
        /// </summary>
        private static async Task<CheckExecutionOutcome> AdaptReturnValue(object result, DiagnosticItem item)
        {
            if (result == null)
                return InferOutcomeFromItem(item);

            if (result is CheckExecutionOutcome outcome)
                return outcome;

            if (result is bool boolResult)
                return AdaptBool(boolResult, item);

            if (result is Task task)
            {
                await task;

                if (task.IsFaulted)
                {
                    var ex = task.Exception?.InnerException ?? task.Exception;
                    item.Status = CheckStatus.Fail;
                    item.Detail = "Async method failed: " + (ex?.Message ?? "unknown error");
                    return new CheckExecutionOutcome { Success = false };
                }

                var taskType = task.GetType();
                if (taskType.IsGenericType)
                {
                    var resultProp = taskType.GetProperty("Result");
                    if (resultProp != null)
                    {
                        var innerResult = resultProp.GetValue(task);
                        if (innerResult is CheckExecutionOutcome co) return co;
                        if (innerResult is bool b) return AdaptBool(b, item);

                        if (innerResult != null && item.Status == CheckStatus.Scanning)
                        {
                            item.Status = CheckStatus.Pass;
                            if (string.IsNullOrEmpty(item.Detail))
                                item.Detail = innerResult.ToString();
                        }

                        return InferOutcomeFromItem(item);
                    }
                }

                return InferOutcomeFromItem(item);
            }

            if (item.Status == CheckStatus.Scanning)
            {
                item.Status = CheckStatus.Pass;
                if (string.IsNullOrEmpty(item.Detail))
                    item.Detail = result.ToString();
            }
            return InferOutcomeFromItem(item);
        }

        private static CheckExecutionOutcome AdaptBool(bool value, DiagnosticItem item)
        {
            if (item.Status == CheckStatus.Scanning)
            {
                item.Status = value ? CheckStatus.Pass : CheckStatus.Fail;
                if (!value && string.IsNullOrEmpty(item.Detail))
                    item.Detail = "Method returned false";
            }

            return new CheckExecutionOutcome { Success = value };
        }

        private static CheckExecutionOutcome InferOutcomeFromItem(DiagnosticItem item)
        {
            if (item.Status == CheckStatus.Scanning)
            {
                item.Status = CheckStatus.Pass;
                item.Score = 100;
            }

            return new CheckExecutionOutcome
            {
                Success = item.Status == CheckStatus.Pass || item.Status == CheckStatus.Fixed
            };
        }

        // ──────────────────────────────────────────────────────────────
        //  Helpers
        // ──────────────────────────────────────────────────────────────

        private static object TryCreateInstance(Type type)
        {
            try { return Activator.CreateInstance(type, nonPublic: true); }
            catch { return null; }
        }

        internal static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
        }

        private static bool IsFrameworkAssembly(string assemblyName)
        {
            return FrameworkAssemblyPrefixes.Any(prefix =>
                assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Format a human-readable method signature for the UI picker.
        /// Example: "static Task&lt;bool&gt; (string port, int baudRate)"
        /// </summary>
        internal static string FormatSignature(MethodInfo method)
        {
            var prefix = method.IsStatic ? "static " : "";
            var returnName = FormatTypeName(method.ReturnType);
            var paramList = string.Join(", ",
                method.GetParameters().Select(p => FormatTypeName(p.ParameterType) + " " + p.Name));
            return string.Format("{0}{1} ({2})", prefix, returnName, paramList);
        }

        private static string FormatTypeName(Type type)
        {
            if (type == typeof(void)) return "void";
            if (type == typeof(string)) return "string";
            if (type == typeof(int)) return "int";
            if (type == typeof(long)) return "long";
            if (type == typeof(double)) return "double";
            if (type == typeof(float)) return "float";
            if (type == typeof(decimal)) return "decimal";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(object)) return "object";

            if (type.IsGenericType)
            {
                var baseName = type.Name;
                var tickIndex = baseName.IndexOf('`');
                if (tickIndex > 0) baseName = baseName.Substring(0, tickIndex);
                var genericArgs = string.Join(", ", type.GetGenericArguments().Select(FormatTypeName));
                return string.Format("{0}<{1}>", baseName, genericArgs);
            }

            return type.Name;
        }
    }
}
