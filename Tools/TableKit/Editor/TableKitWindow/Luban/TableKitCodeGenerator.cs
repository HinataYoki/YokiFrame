#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKit 代码生成器
    /// 在 Luban 生成代码后，生成配套的 TableKit 运行时代码
    /// 生成的代码完全独立，不依赖任何外部配置文件
    /// </summary>
    public static partial class TableKitCodeGenerator
    {
        /// <summary>
        /// 生成所有 TableKit 运行时代码
        /// </summary>
        /// <param name="outputDir">输出目录</param>
        /// <param name="useAssemblyDefinition">是否生成 asmdef</param>
        /// <param name="generateExternalTypeUtil">是否生成外部类型工具</param>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="tablesNamespace">Tables 命名空间</param>
        /// <param name="runtimePathPattern">运行时路径模式，将嵌入生成代码</param>
        /// <param name="editorDataPath">编辑器数据路径，将嵌入生成代码</param>
        /// <param name="codeTarget">代码生成器类型，用于确定程序集引用</param>
        /// <param name="useAsyncLoading">是否生成异步加载代码</param>
        /// <param name="dataDir">数据文件目录，用于扫描表文件名（异步模式需要）</param>
        /// <param name="dataTarget">数据格式（bin/json），用于确定文件扩展名</param>
        public static void Generate(
            string outputDir,
            bool useAssemblyDefinition,
            bool generateExternalTypeUtil,
            string assemblyName = "YokiFrame.TableKit",
            string tablesNamespace = "cfg",
            string runtimePathPattern = "{0}",
            string editorDataPath = "Assets/Art/Table/",
            string codeTarget = "cs-bin",
            bool useAsyncLoading = false,
            string dataDir = "",
            string dataTarget = "bin")
        {
            if (string.IsNullOrEmpty(outputDir))
            {
                Debug.LogError("[TableKit] 输出目录不能为空");
                return;
            }

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // 使用默认值
            if (string.IsNullOrEmpty(runtimePathPattern)) runtimePathPattern = "{0}";
            if (string.IsNullOrEmpty(editorDataPath)) editorDataPath = "Assets/Art/Table/";

            var hasYokiFrame = DetectYokiFrame();

            // 异步模式：扫描数据目录获取表文件名
            string[] tableFileNames = null;
            if (useAsyncLoading && !string.IsNullOrEmpty(dataDir))
            {
                tableFileNames = ScanTableFileNames(dataDir, dataTarget);
                if (tableFileNames.Length == 0)
                {
                    Debug.LogWarning("[TableKit] 异步模式已启用但未找到数据文件，将生成空的文件名列表");
                }
            }

            GenerateTableKit(outputDir, tablesNamespace, hasYokiFrame, runtimePathPattern, editorDataPath,
                useAsyncLoading, tableFileNames);
            
            if (generateExternalTypeUtil)
            {
                var utilPath = Path.Combine(outputDir, "ExternalTypeUtil.cs");
                if (!File.Exists(utilPath))
                {
                    GenerateExternalTypeUtil(outputDir);
                    Debug.Log("[TableKit] 已生成 ExternalTypeUtil.cs");
                }
            }

            if (useAssemblyDefinition)
            {
                CleanupOldAsmdef(outputDir, assemblyName);
                GenerateAssemblyDefinition(outputDir, assemblyName, hasYokiFrame, codeTarget, useAsyncLoading);
            }
            else
            {
                CleanupOldAsmdef(outputDir, null);
            }
            
            CleanupOldFiles(outputDir);
            Debug.Log($"[TableKit] 代码生成完成: {outputDir}");
        }

        #region 检测与清理

        /// <summary>
        /// 检测 YokiFrame 是否存在（作为 Package 或 Assets 文件夹）
        /// </summary>
        private static bool DetectYokiFrame()
        {
            var packagePath = "Packages/com.hinatayoki.yokiframe";
            if (Directory.Exists(packagePath)) return true;
            
            var guids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset YokiFrame");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == "YokiFrame")
                {
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// 清理旧版本遗留文件
        /// </summary>
        private static void CleanupOldFiles(string outputDir)
        {
            var oldFiles = new[] { "ITableLoader.cs", "TableLoadMode.cs", "TableExtensions.cs" };
            foreach (var file in oldFiles)
            {
                var path = Path.Combine(outputDir, file);
                if (File.Exists(path))
                {
                    File.Delete(path);
                    var metaPath = path + ".meta";
                    if (File.Exists(metaPath)) File.Delete(metaPath);
                }
            }

            var loadersDir = Path.Combine(outputDir, "Loaders");
            if (Directory.Exists(loadersDir))
            {
                Directory.Delete(loadersDir, true);
                var metaPath = loadersDir + ".meta";
                if (File.Exists(metaPath)) File.Delete(metaPath);
            }
        }

        /// <summary>
        /// 清理旧的 asmdef 文件
        /// </summary>
        private static void CleanupOldAsmdef(string outputDir, string keepAssemblyName)
        {
            var asmdefFiles = Directory.GetFiles(outputDir, "*.asmdef", SearchOption.TopDirectoryOnly);
            foreach (var asmdefPath in asmdefFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(asmdefPath);
                if (!string.IsNullOrEmpty(keepAssemblyName) && fileName == keepAssemblyName) continue;
                
                File.Delete(asmdefPath);
                var metaPath = asmdefPath + ".meta";
                if (File.Exists(metaPath)) File.Delete(metaPath);
            }
        }

        /// <summary>
        /// 扫描数据目录，获取所有表文件名（不含扩展名）
        /// </summary>
        private static string[] ScanTableFileNames(string dataDir, string dataTarget)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var fullDataDir = dataDir.StartsWith("Assets/")
                ? Path.Combine(projectRoot, dataDir.TrimEnd('/'))
                : dataDir;

            if (!Directory.Exists(fullDataDir))
            {
                Debug.LogWarning($"[TableKit] 数据目录不存在: {fullDataDir}");
                return System.Array.Empty<string>();
            }

            var extension = dataTarget switch
            {
                "bin" => "*.bytes",
                "json" => "*.json",
                "lua" => "*.lua",
                _ => "*.*"
            };

            var files = Directory.GetFiles(fullDataDir, extension, SearchOption.TopDirectoryOnly);
            var fileNames = new string[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                fileNames[i] = Path.GetFileNameWithoutExtension(files[i]);
            }

            return fileNames;
        }

        #endregion
    }
}
#endif
