#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.Compilation;

namespace YokiFrame
{
    /// <summary>根据 Unity Player 编译图定位一方 Managed 程序集，不依赖程序集命名前缀。</summary>
    public sealed partial class YokiFramePlayerBuildBoundaryTests
    {
        private const string PACKAGE_SOURCE_PREFIX = "Assets/YokiFrame/";
        private const string PROJECT_SOURCE_PREFIX = "Assets/Scripts/";

        /// <summary>定位真实 Player 数据目录中由当前 YokiFrame 包或项目 smoke 源码生成的程序集。</summary>
        private static string[] FindFirstPartyManagedAssemblies(string outputRoot)
        {
            HashSet<string> assemblyNames = CollectFirstPartyPlayerAssemblyNames();
            string[] managedPaths = Directory.GetFiles(outputRoot, "*.dll", SearchOption.AllDirectories);
            List<string> paths = new();
            for (var index = 0; index < managedPaths.Length; index++)
            {
                string assemblyName = Path.GetFileNameWithoutExtension(managedPaths[index]);
                if (assemblyNames.Contains(assemblyName)) paths.Add(managedPaths[index]);
            }

            string[] assemblyPaths = paths.ToArray();
            Array.Sort(assemblyPaths, StringComparer.OrdinalIgnoreCase);
            Assert.Greater(assemblyPaths.Length, 0, "Player 构建未包含任何当前项目一方 Managed 程序集。");
            return assemblyPaths;
        }

        /// <summary>从 Unity PlayerWithoutTestAssemblies 编译图收集包与项目 smoke 所属程序集名。</summary>
        private static HashSet<string> CollectFirstPartyPlayerAssemblyNames()
        {
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
            UnityEditor.Compilation.Assembly[] assemblies =
                CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);
            for (var assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                string[] sourceFiles = assemblies[assemblyIndex].sourceFiles;
                for (var sourceIndex = 0; sourceIndex < sourceFiles.Length; sourceIndex++)
                {
                    if (!IsFirstPartySourcePath(sourceFiles[sourceIndex])) continue;
                    names.Add(assemblies[assemblyIndex].name);
                    break;
                }
            }

            return names;
        }

        /// <summary>判断 Unity 编译图中的源码路径是否属于当前包或项目 smoke 目录。</summary>
        private static bool IsFirstPartySourcePath(string sourcePath)
        {
            string normalized = sourcePath.Replace('\\', '/');
            return normalized.StartsWith(PACKAGE_SOURCE_PREFIX, StringComparison.OrdinalIgnoreCase)
                   || normalized.StartsWith(PROJECT_SOURCE_PREFIX, StringComparison.OrdinalIgnoreCase)
                   || normalized.IndexOf("/" + PACKAGE_SOURCE_PREFIX, StringComparison.OrdinalIgnoreCase) >= 0
                   || normalized.IndexOf("/" + PROJECT_SOURCE_PREFIX, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
#endif
