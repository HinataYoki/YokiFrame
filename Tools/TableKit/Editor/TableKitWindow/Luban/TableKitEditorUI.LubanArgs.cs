#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKitEditorUI - Luban 参数构建与数据复制
    /// </summary>
    public partial class TableKitEditorUI
    {
        #region 输出目标分组

        /// <summary>
        /// 按 target 分组所有输出目标
        /// </summary>
        private Dictionary<string, List<OutputConfig>> GroupOutputsByTarget()
        {
            var groups = new Dictionary<string, List<OutputConfig>>();
            var projectRoot = Path.GetDirectoryName(Application.dataPath);

            // 主配置
            var mainDataDir = mOutputDataDir.StartsWith("Assets/")
                ? Path.Combine(projectRoot, mOutputDataDir.TrimEnd('/'))
                : mOutputDataDir;
            var mainCodeDir = mOutputCodeDir.StartsWith("Assets/")
                ? Path.Combine(projectRoot, mOutputCodeDir.TrimEnd('/'))
                : mOutputCodeDir;

            if (!groups.ContainsKey(mTarget))
                groups[mTarget] = new List<OutputConfig>();

            groups[mTarget].Add(new OutputConfig
            {
                IsMain = true,
                DataTarget = mDataTarget,
                DataDir = mainDataDir,
                CodeTarget = mCodeTarget,
                CodeDir = Path.Combine(mainCodeDir, "Luban")
            });

            // 额外输出目标
            foreach (var extra in mExtraOutputTargets)
            {
                if (!extra.enabled) continue;

                var targetKey = extra.target;
                if (!groups.ContainsKey(targetKey))
                    groups[targetKey] = new List<OutputConfig>();

                var extraDataDir = string.IsNullOrEmpty(extra.dataDir) ? "" :
                    (Path.IsPathRooted(extra.dataDir) ? extra.dataDir : Path.Combine(projectRoot, extra.dataDir));
                var extraCodeDir = string.IsNullOrEmpty(extra.codeDir) ? "" :
                    (Path.IsPathRooted(extra.codeDir) ? extra.codeDir : Path.Combine(projectRoot, extra.codeDir));

                groups[targetKey].Add(new OutputConfig
                {
                    IsMain = false,
                    DataTarget = extra.dataTarget,
                    DataDir = extraDataDir,
                    CodeTarget = extra.codeTarget,
                    CodeDir = extraCodeDir
                });
            }

            return groups;
        }

        /// <summary>
        /// 输出配置（用于分组）
        /// </summary>
        private class OutputConfig
        {
            public bool IsMain;
            public string DataTarget;
            public string DataDir;
            public string CodeTarget;
            public string CodeDir;
        }

        #endregion

        #region Luban 参数构建

        /// <summary>
        /// 构建验证模式的 Luban 参数
        /// </summary>
        private string BuildLubanArgsForValidate(string projectRoot)
        {
            var sb = new StringBuilder();
            sb.Append($"-t {mTarget} ");
            sb.Append("--conf luban.conf ");
            sb.Append("-d json ");
            sb.Append($"-x outputDataDir=\"{Path.Combine(projectRoot, "Temp/LubanValidate")}\" ");
            return sb.ToString();
        }

        /// <summary>
        /// 为指定 target 组构建 Luban 参数
        /// </summary>
        private string BuildLubanArgsForGroup(string target, List<OutputConfig> outputs, string projectRoot)
        {
            var sb = new StringBuilder();
            sb.Append($"-t {target} ");
            sb.Append("--conf luban.conf ");

            // 使用 HashSet 跟踪已添加的数据格式和代码目标
            var addedDataTargets = new HashSet<string>();
            var addedCodeTargets = new HashSet<string>();

            foreach (var output in outputs)
            {
                // 添加数据输出
                if (!string.IsNullOrEmpty(output.DataDir) && !addedDataTargets.Contains(output.DataTarget))
                {
                    sb.Append($"-d {output.DataTarget} ");
                    addedDataTargets.Add(output.DataTarget);
                    sb.Append($"-x {output.DataTarget}.outputDataDir=\"{output.DataDir}\" ");
                }

                // 添加代码输出
                if (!string.IsNullOrEmpty(output.CodeDir))
                {
                    if (!addedCodeTargets.Contains(output.CodeTarget))
                    {
                        sb.Append($"-c {output.CodeTarget} ");
                        addedCodeTargets.Add(output.CodeTarget);
                    }
                    sb.Append($"-x {output.CodeTarget}.outputCodeDir=\"{output.CodeDir}\" ");
                }
            }

            return sb.ToString();
        }

        #endregion

        #region 数据复制

        /// <summary>
        /// 为指定 target 组复制数据到额外目录
        /// </summary>
        private void CopyDataToExtraDirectoriesForGroup(string target, List<OutputConfig> outputs, StringBuilder logBuilder)
        {
            // 按数据格式分组，找出需要复制的目录
            var dataTargetDirs = new Dictionary<string, List<string>>();
            var firstDataDirs = new Dictionary<string, string>();

            foreach (var output in outputs)
            {
                if (string.IsNullOrEmpty(output.DataDir)) continue;

                if (firstDataDirs.TryGetValue(output.DataTarget, out var sourceDir))
                {
                    // 已有此格式的源目录，需要复制
                    if (!dataTargetDirs.ContainsKey(output.DataTarget))
                        dataTargetDirs[output.DataTarget] = new List<string>();
                    dataTargetDirs[output.DataTarget].Add(output.DataDir);
                }
                else
                {
                    // 第一次出现此格式，记录为源
                    firstDataDirs[output.DataTarget] = output.DataDir;
                }
            }

            if (dataTargetDirs.Count == 0) return;

            logBuilder.AppendLine("正在复制数据到额外目录...");

            foreach (var kvp in dataTargetDirs)
            {
                var dataFormat = kvp.Key;
                var targetDirs = kvp.Value;
                var sourceDir = firstDataDirs[dataFormat];

                if (!Directory.Exists(sourceDir))
                {
                    logBuilder.AppendLine($"[警告] 源目录不存在: {sourceDir}");
                    continue;
                }

                var extension = GetDataFileExtension(dataFormat);
                var sourceFiles = Directory.GetFiles(sourceDir, $"*{extension}");

                if (sourceFiles.Length == 0)
                {
                    logBuilder.AppendLine($"[警告] 源目录无 {extension} 文件: {sourceDir}");
                    continue;
                }

                foreach (var targetDir in targetDirs)
                {
                    if (!Directory.Exists(targetDir))
                        Directory.CreateDirectory(targetDir);

                    var copiedCount = 0;
                    foreach (var sourceFile in sourceFiles)
                    {
                        var fileName = Path.GetFileName(sourceFile);
                        var targetFile = Path.Combine(targetDir, fileName);
                        File.Copy(sourceFile, targetFile, true);
                        copiedCount++;
                    }

                    logBuilder.AppendLine($"[复制] {dataFormat}: {copiedCount} 个文件 -> {targetDir}");
                }
            }
        }

        /// <summary>
        /// 获取数据格式对应的文件扩展名
        /// </summary>
        private static string GetDataFileExtension(string dataFormat) => dataFormat switch
        {
            "bin" => ".bytes",
            "json" => ".json",
            "lua" => ".lua",
            _ => ".*"
        };

        #endregion
    }
}
#endif
