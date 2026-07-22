#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 从 Unity Editor API 一次性采集 package、asmdef 与预编译程序集证据。
    /// </summary>
    internal sealed class UnityDependencyInventoryProvider
    {
        /// <summary>
        /// 读取三个 Unity 事实源各一次，并构造供纯规划器消费的稳定快照。
        /// </summary>
        /// <returns>本次刷新唯一的依赖 inventory 快照。</returns>
        internal DependencyInventorySnapshot Capture()
        {
            var registeredPackages = PackageInfo.GetAllRegisteredPackages();
            var assemblyDefinitionGuids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");
            var assetPaths = AssetDatabase.GetAllAssetPaths();
            var precompiledAssemblyPaths = CompilationPipeline.GetPrecompiledAssemblyPaths(
                CompilationPipeline.PrecompiledAssemblySources.UserAssembly);

            var assemblyDefinitions = ReadAssemblyDefinitionNames(assemblyDefinitionGuids);
            return new DependencyInventorySnapshot(
                ReadPackageNames(registeredPackages),
                assemblyDefinitions.Names,
                ReadPrecompiledAssemblyNames(precompiledAssemblyPaths, assetPaths),
                assemblyDefinitions.Diagnostics);
        }

        /// <summary>
        /// 从 Unity package 信息中提取非空包名，并生成稳定去重数组。
        /// </summary>
        /// <param name="packages">Unity 已注册 package 快照。</param>
        /// <returns>稳定去重后的 package 名称。</returns>
        private static string[] ReadPackageNames(PackageInfo[] packages)
        {
            List<string> names = new(packages == null ? 0 : packages.Length);
            if (packages == null)
            {
                return Array.Empty<string>();
            }

            for (var index = 0; index < packages.Length; index++)
            {
                if (packages[index] != null && !string.IsNullOrWhiteSpace(packages[index].name))
                {
                    names.Add(packages[index].name);
                }
            }

            return ToStableArray(names);
        }

        /// <summary>
        /// 逐个读取 asmdef JSON 的真实 name，禁止使用文件名猜测程序集身份。
        /// </summary>
        /// <param name="guids">AssetDatabase 返回的 asmdef GUID 快照。</param>
        /// <returns>稳定去重后的 asmdef name。</returns>
        private static AssemblyDefinitionInventory ReadAssemblyDefinitionNames(string[] guids)
        {
            List<string> names = new(guids == null ? 0 : guids.Length);
            List<string> diagnostics = new();
            if (guids == null)
            {
                return new AssemblyDefinitionInventory(Array.Empty<string>(), Array.Empty<string>());
            }

            for (var index = 0; index < guids.Length; index++)
            {
                try
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guids[index]);
                    if (string.IsNullOrEmpty(assetPath))
                    {
                        diagnostics.Add("asmdef GUID 无法解析为资源路径: " + guids[index]);
                        continue;
                    }

                    if (TryReadAssemblyDefinitionName(
                        assetPath,
                        () => File.ReadAllText(assetPath),
                        out var assemblyName,
                        out var diagnostic))
                    {
                        names.Add(assemblyName);
                    }
                    else if (!string.IsNullOrEmpty(diagnostic))
                    {
                        diagnostics.Add(diagnostic);
                    }
                }
                catch (Exception exception)
                {
                    diagnostics.Add("asmdef 证据采集失败: " + guids[index] + " (" + exception.Message + ")");
                }
            }

            return new AssemblyDefinitionInventory(ToStableArray(names), ToStableArray(diagnostics));
        }

        /// <summary>
        /// 合并 CompilationPipeline 与 AssetDatabase 的 DLL 文件名，覆盖旧 PluginImporter 资源。
        /// </summary>
        /// <param name="assemblyPaths">Unity 返回的预编译程序集路径。</param>
        /// <param name="assetPaths">AssetDatabase 返回的完整资源路径快照。</param>
        /// <returns>稳定去重后的 DLL 文件名。</returns>
        private static string[] ReadPrecompiledAssemblyNames(
            string[] assemblyPaths,
            string[] assetPaths)
        {
            int capacity = (assemblyPaths?.Length ?? 0) + 16;
            List<string> names = new(capacity);
            AppendAssemblyPathNames(names, assemblyPaths);
            if (assetPaths != null)
            {
                for (var index = 0; index < assetPaths.Length; index++)
                {
                    if (string.Equals(Path.GetExtension(assetPaths[index]), ".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        AppendAssemblyName(names, assetPaths[index]);
                    }
                }
            }

            return ToStableArray(names);
        }

        /// <summary>把 Unity CompilationPipeline 返回的程序集路径追加为文件名证据。</summary>
        /// <param name="names">接收 DLL 文件名的列表。</param>
        /// <param name="assemblyPaths">Unity 返回的预编译程序集路径。</param>
        private static void AppendAssemblyPathNames(List<string> names, string[] assemblyPaths)
        {
            if (assemblyPaths == null)
            {
                return;
            }

            for (var index = 0; index < assemblyPaths.Length; index++)
            {
                AppendAssemblyName(names, assemblyPaths[index]);
            }
        }

        /// <summary>从单个路径提取非空程序集文件名。</summary>
        /// <param name="names">接收 DLL 文件名的列表。</param>
        /// <param name="path">CompilationPipeline 或 PluginImporter 资源路径。</param>
        private static void AppendAssemblyName(List<string> names, string path)
        {
            string fileName = Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                names.Add(fileName);
            }
        }

        /// <summary>
        /// 使用 Unity JSON 解析器读取 asmdef schema 的 name 字段。
        /// </summary>
        /// <param name="json">完整 asmdef JSON。</param>
        /// <param name="assemblyName">解析成功后的真实程序集名称。</param>
        /// <returns>JSON 合法且包含非空 name 时返回 true。</returns>
        public static bool TryReadAssemblyDefinitionName(string json, out string assemblyName)
        {
            assemblyName = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                var data = JsonUtility.FromJson<AssemblyDefinitionJson>(json);
                if (data == null || string.IsNullOrWhiteSpace(data.Name))
                {
                    return false;
                }

                assemblyName = data.Name.Trim();
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// 从单个 asmdef 文件读取真实程序集名；读取或解析失败时保留路径级诊断而不向上中断整轮 inventory。
        /// </summary>
        /// <param name="assetPath">Unity AssetDatabase 返回的 asmdef 路径。</param>
        /// <param name="readText">延迟执行的文件读取器，便于把单文件 I/O 故障隔离到当前证据。</param>
        /// <param name="assemblyName">读取成功后的真实程序集名称。</param>
        /// <param name="diagnostic">读取或解析失败后的可诊断说明；成功时为空字符串。</param>
        /// <returns>读取并解析出有效 name 时返回 true。</returns>
        public static bool TryReadAssemblyDefinitionName(
            string assetPath,
            Func<string> readText,
            out string assemblyName,
            out string diagnostic)
        {
            if (readText == null)
            {
                throw new ArgumentNullException(nameof(readText));
            }

            assemblyName = string.Empty;
            diagnostic = string.Empty;
            try
            {
                if (TryReadAssemblyDefinitionName(readText(), out assemblyName))
                {
                    return true;
                }

                diagnostic = "asmdef 未包含有效 name: " + assetPath;
                return false;
            }
            catch (Exception exception)
            {
                diagnostic = "asmdef 无法读取: " + assetPath + " (" + exception.Message + ")";
                return false;
            }
        }

        /// <summary>
        /// 将 Unity 事实集合统一去重并按 Ordinal 排序，保证相同环境产生相同快照。
        /// </summary>
        /// <param name="values">待规范化的依赖标识。</param>
        /// <returns>稳定去重后的数组。</returns>
        private static string[] ToStableArray(List<string> values)
        {
            HashSet<string> uniqueValues = new(values, StringComparer.OrdinalIgnoreCase);
            var result = new string[uniqueValues.Count];
            uniqueValues.CopyTo(result);
            Array.Sort(result, StringComparer.OrdinalIgnoreCase);
            return result;
        }

        /// <summary>
        /// 仅用于映射 asmdef JSON schema；字段名必须与 Unity 的小写 name 保持一致。
        /// </summary>
        [Serializable]
        private sealed class AssemblyDefinitionJson
        {
            [SerializeField]
            private string name;

            internal string Name => name;
        }

        /// <summary>
        /// 保存 asmdef 证据和已隔离的单文件诊断，避免异常文件污染其它依赖识别。
        /// </summary>
        private sealed class AssemblyDefinitionInventory
        {
            /// <summary>
            /// 创建 asmdef 采集结果。
            /// </summary>
            /// <param name="names">成功解析的程序集名称。</param>
            /// <param name="diagnostics">单文件读取或解析诊断。</param>
            internal AssemblyDefinitionInventory(string[] names, string[] diagnostics)
            {
                Names = names;
                Diagnostics = diagnostics;
            }

            /// <summary>获取成功解析的程序集名称。</summary>
            internal string[] Names { get; }

            /// <summary>获取单文件读取或解析诊断。</summary>
            internal string[] Diagnostics { get; }
        }
    }
}

#endif
