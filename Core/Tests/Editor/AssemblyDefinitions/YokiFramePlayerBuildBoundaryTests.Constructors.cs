#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace YokiFrame
{
    /// <summary>扫描 Player 元数据中的构造参数，防止工具展示字段回流到业务构造契约。</summary>
    public sealed partial class YokiFramePlayerBuildBoundaryTests
    {
        /// <summary>确认真实 Player Core 程序集保留跨构建一致的 FSM 名称构造参数。</summary>
        /// <param name="assemblyPaths">真实 Player 一方程序集路径。</param>
        private static void AssertPlayerFsmNameConstructor(string[] assemblyPaths)
        {
            for (var index = 0; index < assemblyPaths.Length; index++)
            {
                if (!string.Equals(Path.GetFileName(assemblyPaths[index]), "YokiFrame.dll", StringComparison.Ordinal))
                {
                    continue;
                }

                HashSet<string> parameters = ReadMetadataConstructorParameters(assemblyPaths[index]);
                if (!parameters.Contains("FSM:name"))
                {
                    throw new InvalidOperationException("Player 的 FSM 构造器缺少跨构建名称参数。");
                }

                return;
            }

            throw new FileNotFoundException("Player Managed 输出缺少 YokiFrame.dll。");
        }

        /// <summary>扫描全部一方 Managed DLL，返回命中的 Type:param 构造参数。</summary>
        private static List<string> FindForbiddenConstructorParameterHits(
            string[] assemblyPaths,
            string[] forbiddenQualifiedParameters)
        {
            List<string> hits = new();
            for (var assemblyIndex = 0; assemblyIndex < assemblyPaths.Length; assemblyIndex++)
            {
                HashSet<string> parameters = ReadMetadataConstructorParameters(assemblyPaths[assemblyIndex]);
                for (var parameterIndex = 0; parameterIndex < forbiddenQualifiedParameters.Length; parameterIndex++)
                {
                    string parameter = forbiddenQualifiedParameters[parameterIndex];
                    if (!parameters.Contains(parameter)) continue;
                    hits.Add(Path.GetFileName(assemblyPaths[assemblyIndex]) + ":" + parameter);
                }
            }

            return hits;
        }

        /// <summary>通过 Unity 自带 Mono.Cecil 读取 Type:param 构造参数名称。</summary>
        private static HashSet<string> ReadMetadataConstructorParameters(string assemblyPath)
        {
            Assembly cecilAssembly = ResolveCecilAssembly();
            Type assemblyDefinitionType = cecilAssembly.GetType("Mono.Cecil.AssemblyDefinition", true);
            MethodInfo readAssembly = assemblyDefinitionType.GetMethod(
                "ReadAssembly",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            if (readAssembly == null) throw new MissingMethodException("Mono.Cecil 缺少 ReadAssembly(string) API。");
            object assemblyDefinition = readAssembly.Invoke(null, new object[] { assemblyPath });
            try
            {
                object mainModule = assemblyDefinitionType.GetProperty("MainModule").GetValue(assemblyDefinition);
                IEnumerable definitions = (IEnumerable)mainModule.GetType().GetProperty("Types").GetValue(mainModule);
                HashSet<string> parameters = new(StringComparer.Ordinal);
                AddConstructorParameters(definitions, parameters);
                return parameters;
            }
            finally
            {
                (assemblyDefinition as IDisposable)?.Dispose();
            }
        }

        /// <summary>递归收集顶层与嵌套类型的实例构造参数。</summary>
        private static void AddConstructorParameters(IEnumerable definitions, HashSet<string> parameters)
        {
            foreach (object definition in definitions)
            {
                Type definitionType = definition.GetType();
                string typeName = NormalizeMetadataName(
                    (string)definitionType.GetProperty("Name").GetValue(definition));
                IEnumerable methods = (IEnumerable)definitionType.GetProperty("Methods").GetValue(definition);
                AddTypeConstructorParameters(typeName, methods, parameters);
                IEnumerable nestedTypes = (IEnumerable)definitionType.GetProperty("NestedTypes").GetValue(definition);
                AddConstructorParameters(nestedTypes, parameters);
            }
        }

        /// <summary>收集单个类型所有实例构造器的命名参数。</summary>
        private static void AddTypeConstructorParameters(
            string typeName,
            IEnumerable methods,
            HashSet<string> parameters)
        {
            foreach (object method in methods)
            {
                Type methodType = method.GetType();
                string methodName = (string)methodType.GetProperty("Name").GetValue(method);
                if (!string.Equals(methodName, ".ctor", StringComparison.Ordinal)) continue;
                IEnumerable methodParameters = (IEnumerable)methodType.GetProperty("Parameters").GetValue(method);
                foreach (object parameter in methodParameters)
                {
                    string parameterName = (string)parameter.GetType().GetProperty("Name").GetValue(parameter);
                    if (!string.IsNullOrEmpty(parameterName)) parameters.Add(typeName + ":" + parameterName);
                }
            }
        }
    }
}
#endif
