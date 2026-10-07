#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace YokiFrame
{
    /// <summary>Explicit public instance calls without numeric conversion or guessed overloads.</summary>
    public sealed class LiveMethodInvoker
    {
        private sealed class Candidate
        {
            internal MethodInfo Method;
            internal ParameterInfo[] Parameters;
        }

        private readonly object mInstance;
        private readonly Dictionary<string, List<Candidate>> mMethods =
            new Dictionary<string, List<Candidate>>(StringComparer.Ordinal);

        public LiveMethodInvoker(object instance)
        {
            mInstance = instance ?? throw new ArgumentNullException(nameof(instance));
            foreach (var method in instance.GetType().GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName || method.ContainsGenericParameters
                    || method.ReturnType.IsByRef || method.ReturnType.IsPointer) continue;
                var parameters = method.GetParameters();
                bool supported = true;
                foreach (var parameter in parameters)
                    if (parameter.ParameterType.IsByRef || parameter.ParameterType.IsPointer) supported = false;
                if (!supported) continue;
                if (!mMethods.TryGetValue(method.Name, out var candidates))
                    mMethods.Add(method.Name, candidates = new List<Candidate>());
                candidates.Add(new Candidate { Method = method, Parameters = parameters });
            }
        }

        public object Invoke(string name, object[] arguments)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A public method name is required.", nameof(name));
            arguments = arguments ?? Array.Empty<object>();
            if (arguments.Length > 32) throw new ArgumentException("Live calls support at most 32 arguments.");
            Candidate selected = null;
            if (mMethods.TryGetValue(name, out var candidates))
            {
                foreach (var candidate in candidates)
                {
                    if (!Matches(candidate.Parameters, arguments)) continue;
                    if (selected != null)
                        throw new AmbiguousMatchException("Multiple overloads match " + name
                            + "; use a uniquely named public method or a compiled shared interface.");
                    selected = candidate;
                }
            }
            if (selected == null)
                throw new MissingMethodException("No supported public instance method matches " + name
                    + ". Arguments must match declared types; optional arguments must be supplied.");
            try { return selected.Method.Invoke(mInstance, arguments); }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        private static bool Matches(ParameterInfo[] parameters, object[] arguments)
        {
            if (parameters.Length != arguments.Length) return false;
            for (int i = 0; i < parameters.Length; i++)
            {
                Type type = parameters[i].ParameterType;
                if (arguments[i] == null)
                {
                    if (type.IsValueType && Nullable.GetUnderlyingType(type) == null) return false;
                }
                else if (!type.IsInstanceOfType(arguments[i])) return false;
            }
            return true;
        }
    }
}
#endif
