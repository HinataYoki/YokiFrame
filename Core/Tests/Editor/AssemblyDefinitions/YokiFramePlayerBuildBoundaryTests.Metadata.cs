#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
#if UNITY_6000_5_OR_NEWER
using UnityEngine.Assemblies;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 承载 Player 边界测试的 ECMA-335 TypeDef 读取与无分配二进制文本扫描。
    /// </summary>
    public sealed partial class YokiFramePlayerBuildBoundaryTests
    {
        /// <summary>确认指定业务类型名真实存在于程序集 TypeDef 表。</summary>
        private static void AssertMetadataTypeExists(string assemblyPath, string typeName)
        {
            HashSet<string> typeNames = ReadMetadataTypeNames(assemblyPath);
            Assert.IsTrue(
                typeNames.Contains(typeName),
                Path.GetFileName(assemblyPath) + " 缺少业务 Runtime 正向类型: " + typeName);
        }

        /// <summary>扫描全部一方 Managed DLL，返回仍残留的工具类型及其程序集。</summary>
        private static List<string> FindForbiddenTypeHits(string[] assemblyPaths, string[] forbiddenTypeNames)
        {
            List<string> hits = new();
            for (var assemblyIndex = 0; assemblyIndex < assemblyPaths.Length; assemblyIndex++)
            {
                HashSet<string> assemblyTypeNames = ReadMetadataTypeNames(assemblyPaths[assemblyIndex]);
                for (var typeIndex = 0; typeIndex < forbiddenTypeNames.Length; typeIndex++)
                {
                    if (!assemblyTypeNames.Contains(forbiddenTypeNames[typeIndex])) continue;
                    hits.Add(Path.GetFileName(assemblyPaths[assemblyIndex]) + ":" + forbiddenTypeNames[typeIndex]);
                }
            }

            return hits;
        }

        /// <summary>扫描全部一方 Managed DLL，返回仍残留在业务类型上的工具方法、属性或字段。</summary>
        private static List<string> FindForbiddenMemberHits(
            string[] assemblyPaths,
            string[] forbiddenMemberNames)
        {
            List<string> hits = new();
            for (var assemblyIndex = 0; assemblyIndex < assemblyPaths.Length; assemblyIndex++)
            {
                HashSet<string> assemblyMemberNames = ReadMetadataMemberNames(assemblyPaths[assemblyIndex]);
                for (var memberIndex = 0; memberIndex < forbiddenMemberNames.Length; memberIndex++)
                {
                    string memberName = forbiddenMemberNames[memberIndex];
                    if (!assemblyMemberNames.Contains(memberName)) continue;
                    hits.Add(Path.GetFileName(assemblyPaths[assemblyIndex]) + ":" + memberName);
                }
            }

            return hits;
        }

        /// <summary>扫描全部一方 Managed DLL，返回仍写入元数据的工具专属参数名。</summary>
        private static List<string> FindForbiddenParameterHits(
            string[] assemblyPaths,
            string[] forbiddenParameterNames)
        {
            List<string> hits = new();
            for (var assemblyIndex = 0; assemblyIndex < assemblyPaths.Length; assemblyIndex++)
            {
                HashSet<string> parameterNames = ReadMetadataParameterNames(assemblyPaths[assemblyIndex]);
                for (var nameIndex = 0; nameIndex < forbiddenParameterNames.Length; nameIndex++)
                {
                    string parameterName = forbiddenParameterNames[nameIndex];
                    if (!parameterNames.Contains(parameterName)) continue;
                    hits.Add(Path.GetFileName(assemblyPaths[assemblyIndex]) + ":" + parameterName);
                }
            }

            return hits;
        }

        /// <summary>扫描全部一方 Managed DLL，返回 partial 业务类型上残留的工具成员。</summary>
        private static List<string> FindForbiddenQualifiedMemberHits(
            string[] assemblyPaths,
            string[] forbiddenQualifiedMemberNames)
        {
            List<string> hits = new();
            for (var assemblyIndex = 0; assemblyIndex < assemblyPaths.Length; assemblyIndex++)
            {
                HashSet<string> memberNames = ReadMetadataQualifiedMemberNames(assemblyPaths[assemblyIndex]);
                for (var nameIndex = 0; nameIndex < forbiddenQualifiedMemberNames.Length; nameIndex++)
                {
                    string memberName = forbiddenQualifiedMemberNames[nameIndex];
                    if (!memberNames.Contains(memberName)) continue;
                    hits.Add(Path.GetFileName(assemblyPaths[assemblyIndex]) + ":" + memberName);
                }
            }

            return hits;
        }

        /// <summary>扫描全部一方 Managed DLL，返回对 Editor、测试或 Workbench 工具程序集的引用。</summary>
        private static List<string> FindForbiddenAssemblyReferenceHits(
            string[] assemblyPaths,
            string[] forbiddenReferenceFragments)
        {
            List<string> hits = new();
            for (var assemblyIndex = 0; assemblyIndex < assemblyPaths.Length; assemblyIndex++)
            {
                HashSet<string> referenceNames = ReadMetadataAssemblyReferenceNames(assemblyPaths[assemblyIndex]);
                foreach (string referenceName in referenceNames)
                {
                    for (var fragmentIndex = 0; fragmentIndex < forbiddenReferenceFragments.Length; fragmentIndex++)
                    {
                        if (referenceName.IndexOf(
                                forbiddenReferenceFragments[fragmentIndex],
                                StringComparison.OrdinalIgnoreCase) < 0) continue;
                        hits.Add(Path.GetFileName(assemblyPaths[assemblyIndex]) + "->" + referenceName);
                        break;
                    }
                }
            }

            return hits;
        }

        /// <summary>通过 Unity 自带 Mono.Cecil 读取程序集 TypeDef，避免字符串堆后缀复用造成误判。</summary>
        private static HashSet<string> ReadMetadataTypeNames(string assemblyPath)
        {
            Assembly cecilAssembly = ResolveCecilAssembly();
            Type assemblyDefinitionType = cecilAssembly.GetType("Mono.Cecil.AssemblyDefinition", true);
            MethodInfo readAssembly = assemblyDefinitionType.GetMethod(
                "ReadAssembly",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            Assert.IsNotNull(readAssembly, "Mono.Cecil 缺少 ReadAssembly(string) API。");
            object assemblyDefinition = readAssembly.Invoke(null, new object[] { assemblyPath });
            try
            {
                object mainModule = assemblyDefinitionType.GetProperty("MainModule").GetValue(assemblyDefinition);
                IEnumerable typeDefinitions = (IEnumerable)mainModule.GetType().GetProperty("Types").GetValue(mainModule);
                HashSet<string> typeNames = new(StringComparer.Ordinal);
                AddTypeDefinitionNames(typeDefinitions, typeNames);
                return typeNames;
            }
            finally
            {
                (assemblyDefinition as IDisposable)?.Dispose();
            }
        }

        /// <summary>通过 Unity 自带 Mono.Cecil 读取程序集中的方法、字段和属性名称。</summary>
        private static HashSet<string> ReadMetadataMemberNames(string assemblyPath)
        {
            Assembly cecilAssembly = ResolveCecilAssembly();
            Type assemblyDefinitionType = cecilAssembly.GetType("Mono.Cecil.AssemblyDefinition", true);
            MethodInfo readAssembly = assemblyDefinitionType.GetMethod(
                "ReadAssembly",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            Assert.IsNotNull(readAssembly, "Mono.Cecil 缺少 ReadAssembly(string) API。");
            object assemblyDefinition = readAssembly.Invoke(null, new object[] { assemblyPath });
            try
            {
                object mainModule = assemblyDefinitionType.GetProperty("MainModule").GetValue(assemblyDefinition);
                IEnumerable typeDefinitions = (IEnumerable)mainModule.GetType().GetProperty("Types").GetValue(mainModule);
                HashSet<string> memberNames = new(StringComparer.Ordinal);
                AddTypeDefinitionMemberNames(typeDefinitions, memberNames);
                return memberNames;
            }
            finally
            {
                (assemblyDefinition as IDisposable)?.Dispose();
            }
        }

        /// <summary>通过 Unity 自带 Mono.Cecil 读取程序集中的方法参数名称。</summary>
        private static HashSet<string> ReadMetadataParameterNames(string assemblyPath)
        {
            Assembly cecilAssembly = ResolveCecilAssembly();
            Type assemblyDefinitionType = cecilAssembly.GetType("Mono.Cecil.AssemblyDefinition", true);
            MethodInfo readAssembly = assemblyDefinitionType.GetMethod(
                "ReadAssembly",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            Assert.IsNotNull(readAssembly, "Mono.Cecil 缺少 ReadAssembly(string) API。");
            object assemblyDefinition = readAssembly.Invoke(null, new object[] { assemblyPath });
            try
            {
                object mainModule = assemblyDefinitionType.GetProperty("MainModule").GetValue(assemblyDefinition);
                IEnumerable typeDefinitions = (IEnumerable)mainModule.GetType().GetProperty("Types").GetValue(mainModule);
                HashSet<string> parameterNames = new(StringComparer.Ordinal);
                AddTypeDefinitionParameterNames(typeDefinitions, parameterNames);
                return parameterNames;
            }
            finally
            {
                (assemblyDefinition as IDisposable)?.Dispose();
            }
        }

        /// <summary>通过 Unity 自带 Mono.Cecil 读取程序集中的 Type.Member 限定成员名。</summary>
        private static HashSet<string> ReadMetadataQualifiedMemberNames(string assemblyPath)
        {
            Assembly cecilAssembly = ResolveCecilAssembly();
            Type assemblyDefinitionType = cecilAssembly.GetType("Mono.Cecil.AssemblyDefinition", true);
            MethodInfo readAssembly = assemblyDefinitionType.GetMethod(
                "ReadAssembly",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            Assert.IsNotNull(readAssembly, "Mono.Cecil 缺少 ReadAssembly(string) API。");
            object assemblyDefinition = readAssembly.Invoke(null, new object[] { assemblyPath });
            try
            {
                object mainModule = assemblyDefinitionType.GetProperty("MainModule").GetValue(assemblyDefinition);
                IEnumerable typeDefinitions = (IEnumerable)mainModule.GetType().GetProperty("Types").GetValue(mainModule);
                HashSet<string> memberNames = new(StringComparer.Ordinal);
                AddTypeDefinitionQualifiedMemberNames(typeDefinitions, memberNames);
                return memberNames;
            }
            finally
            {
                (assemblyDefinition as IDisposable)?.Dispose();
            }
        }

        /// <summary>通过 Unity 自带 Mono.Cecil 读取程序集 AssemblyRef 名称。</summary>
        private static HashSet<string> ReadMetadataAssemblyReferenceNames(string assemblyPath)
        {
            Assembly cecilAssembly = ResolveCecilAssembly();
            Type assemblyDefinitionType = cecilAssembly.GetType("Mono.Cecil.AssemblyDefinition", true);
            MethodInfo readAssembly = assemblyDefinitionType.GetMethod(
                "ReadAssembly",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            Assert.IsNotNull(readAssembly, "Mono.Cecil 缺少 ReadAssembly(string) API。");
            object assemblyDefinition = readAssembly.Invoke(null, new object[] { assemblyPath });
            try
            {
                object mainModule = assemblyDefinitionType.GetProperty("MainModule").GetValue(assemblyDefinition);
                IEnumerable references = (IEnumerable)mainModule.GetType()
                    .GetProperty("AssemblyReferences").GetValue(mainModule);
                HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
                foreach (object reference in references)
                {
                    names.Add((string)reference.GetType().GetProperty("Name").GetValue(reference));
                }

                return names;
            }
            finally
            {
                (assemblyDefinition as IDisposable)?.Dispose();
            }
        }

        /// <summary>递归收集顶层与嵌套 TypeDef 名称，并移除泛型 arity 后缀。</summary>
        private static void AddTypeDefinitionNames(IEnumerable definitions, HashSet<string> typeNames)
        {
            foreach (object definition in definitions)
            {
                Type definitionType = definition.GetType();
                string name = (string)definitionType.GetProperty("Name").GetValue(definition);
                int arityIndex = name.IndexOf('`');
                typeNames.Add(arityIndex < 0 ? name : name.Substring(0, arityIndex));
                IEnumerable nestedTypes = (IEnumerable)definitionType.GetProperty("NestedTypes").GetValue(definition);
                AddTypeDefinitionNames(nestedTypes, typeNames);
            }
        }

        /// <summary>递归收集 TypeDef 的方法、字段和属性名称。</summary>
        private static void AddTypeDefinitionMemberNames(
            IEnumerable definitions,
            HashSet<string> memberNames)
        {
            foreach (object definition in definitions)
            {
                Type definitionType = definition.GetType();
                AddMemberNames(definitionType, definition, "Methods", memberNames);
                AddMemberNames(definitionType, definition, "Fields", memberNames);
                AddMemberNames(definitionType, definition, "Properties", memberNames);
                IEnumerable nestedTypes = (IEnumerable)definitionType.GetProperty("NestedTypes").GetValue(definition);
                AddTypeDefinitionMemberNames(nestedTypes, memberNames);
            }
        }

        /// <summary>递归收集 TypeDef 全部方法参数名，包括嵌套类型。</summary>
        private static void AddTypeDefinitionParameterNames(
            IEnumerable definitions,
            HashSet<string> parameterNames)
        {
            foreach (object definition in definitions)
            {
                Type definitionType = definition.GetType();
                IEnumerable methods = (IEnumerable)definitionType.GetProperty("Methods").GetValue(definition);
                AddMethodParameterNames(methods, parameterNames);
                IEnumerable nestedTypes = (IEnumerable)definitionType.GetProperty("NestedTypes").GetValue(definition);
                AddTypeDefinitionParameterNames(nestedTypes, parameterNames);
            }
        }

        /// <summary>收集 Cecil MethodDefinition 集合中的显式参数名。</summary>
        private static void AddMethodParameterNames(IEnumerable methods, HashSet<string> parameterNames)
        {
            foreach (object method in methods)
            {
                IEnumerable parameters = (IEnumerable)method.GetType().GetProperty("Parameters").GetValue(method);
                foreach (object parameter in parameters)
                {
                    string name = (string)parameter.GetType().GetProperty("Name").GetValue(parameter);
                    if (!string.IsNullOrEmpty(name)) parameterNames.Add(name);
                }
            }
        }

        /// <summary>递归收集 Type.Member 限定名称，覆盖方法、字段和属性。</summary>
        private static void AddTypeDefinitionQualifiedMemberNames(
            IEnumerable definitions,
            HashSet<string> memberNames)
        {
            foreach (object definition in definitions)
            {
                Type definitionType = definition.GetType();
                string typeName = NormalizeMetadataName(
                    (string)definitionType.GetProperty("Name").GetValue(definition));
                AddQualifiedMemberNames(definitionType, definition, "Methods", typeName, memberNames);
                AddQualifiedMemberNames(definitionType, definition, "Fields", typeName, memberNames);
                AddQualifiedMemberNames(definitionType, definition, "Properties", typeName, memberNames);
                IEnumerable nestedTypes = (IEnumerable)definitionType.GetProperty("NestedTypes").GetValue(definition);
                AddTypeDefinitionQualifiedMemberNames(nestedTypes, memberNames);
            }
        }

        /// <summary>把 Cecil 成员集合追加为 Type.Member 限定名称。</summary>
        private static void AddQualifiedMemberNames(
            Type definitionType,
            object definition,
            string collectionName,
            string typeName,
            HashSet<string> memberNames)
        {
            IEnumerable members = (IEnumerable)definitionType.GetProperty(collectionName).GetValue(definition);
            foreach (object member in members)
            {
                string memberName = (string)member.GetType().GetProperty("Name").GetValue(member);
                memberNames.Add(typeName + "." + memberName);
            }
        }

        /// <summary>移除 Cecil 泛型 arity 后缀，统一源码类型名与元数据类型名。</summary>
        private static string NormalizeMetadataName(string name)
        {
            int arityIndex = name.IndexOf('`');
            return arityIndex < 0 ? name : name.Substring(0, arityIndex);
        }

        /// <summary>从 Cecil TypeDefinition 的指定成员集合提取名称。</summary>
        private static void AddMemberNames(
            Type definitionType,
            object definition,
            string collectionName,
            HashSet<string> memberNames)
        {
            IEnumerable members = (IEnumerable)definitionType.GetProperty(collectionName).GetValue(definition);
            foreach (object member in members)
            {
                string name = (string)member.GetType().GetProperty("Name").GetValue(member);
                memberNames.Add(name);
            }
        }

        /// <summary>
        /// 复用 Unity 当前加载上下文中的 Cecil；Unity 6.5 及以上始终使用 Unity 管理的程序集 API，
        /// Unity 2022.3 基线则保留其可用的 AppDomain 与路径加载实现。
        /// </summary>
        private static Assembly ResolveCecilAssembly()
        {
#if UNITY_6000_5_OR_NEWER
            IEnumerable<Assembly> loadedAssemblies = CurrentAssemblies.GetLoadedAssemblies();
#else
            Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
#endif
            foreach (Assembly loadedAssembly in loadedAssemblies)
            {
                if (string.Equals(loadedAssembly.GetName().Name, "Mono.Cecil", StringComparison.Ordinal))
                {
                    return loadedAssembly;
                }
            }

            string contentsPath = EditorApplication.applicationContentsPath;
            string[] candidatePaths = CreateCecilCandidatePaths(contentsPath);
            for (var index = 0; index < candidatePaths.Length; index++)
            {
                if (!File.Exists(candidatePaths[index])) continue;
#if UNITY_6000_5_OR_NEWER
                return CurrentAssemblies.LoadFromPath(candidatePaths[index]);
#else
                return Assembly.LoadFrom(candidatePaths[index]);
#endif
            }

            throw new FileNotFoundException("Unity 安装目录中未找到 Mono.Cecil.dll，无法审计 Player TypeDef。");
        }

        /// <summary>返回覆盖 Unity 2022.3 与 Unity 6 布局的 Mono.Cecil 候选路径。</summary>
        private static string[] CreateCecilCandidatePaths(string contentsPath)
        {
            return new[]
            {
                Path.Combine(contentsPath, "Tools", "BuildPipeline", "Compilation", "Unity.CompilationPipeline.Common", "Mono.Cecil.dll"),
                Path.Combine(contentsPath, "Tools", "BuildPipeline", "Compilation", "ApiUpdater", "Mono.Cecil.dll"),
                Path.Combine(contentsPath, "MonoBleedingEdge", "lib", "mono", "unity", "Mono.Cecil.dll"),
                Path.Combine(contentsPath, "Managed", "Mono.Cecil.dll")
            };
        }

        /// <summary>在任意二进制数据中查找完整字节序列。</summary>
        private static bool ContainsSequence(byte[] bytes, byte[] expected)
        {
            for (var offset = 0; offset + expected.Length <= bytes.Length; offset++)
            {
                if (bytes[offset] == expected[0] && SequenceMatchesAt(bytes, expected, offset)) return true;
            }

            return false;
        }

        /// <summary>从指定偏移逐字节比较候选序列，避免为每次扫描分配切片。</summary>
        private static bool SequenceMatchesAt(byte[] bytes, byte[] expected, int offset)
        {
            for (var index = 0; index < expected.Length; index++)
            {
                if (bytes[offset + index] != expected[index]) return false;
            }

            return true;
        }
    }
}
#endif
