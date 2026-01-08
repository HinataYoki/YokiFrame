#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKitEditorUI - Luban 生成逻辑
    /// </summary>
    public partial class TableKitEditorUI
    {
        #region Luban 生成入口

        private void GenerateLuban() => ExecuteLuban(false);
        private void ValidateLuban() => ExecuteLuban(true);

        private void ExecuteLuban(bool validateOnly)
        {
            if (!ValidateLubanConfig()) return;

            mGenerateBtn.SetEnabled(false);
            UpdateStatusBanner(BuildStatus.Building);

            var logBuilder = new StringBuilder();
            logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss}] 开始{(validateOnly ? "验证" : "生成")}...");

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var workDir = Path.IsPathRooted(mLubanWorkDir) ? mLubanWorkDir : Path.Combine(projectRoot, mLubanWorkDir);
            var dllPath = Path.IsPathRooted(mLubanDllPath) ? mLubanDllPath : Path.Combine(projectRoot, mLubanDllPath);

            try
            {
                var allSuccess = true;

                if (validateOnly)
                {
                    // 验证模式：只运行一次
                    allSuccess = RunLubanOnce(dllPath, workDir, BuildLubanArgsForValidate(projectRoot), logBuilder);
                    if (allSuccess)
                    {
                        LoadDataPreview(Path.Combine(projectRoot, "Temp/LubanValidate"), logBuilder);
                    }
                }
                else
                {
                    // 生成模式：按 target 分组运行
                    var targetGroups = GroupOutputsByTarget();
                    var runIndex = 0;

                    foreach (var group in targetGroups)
                    {
                        runIndex++;
                        if (targetGroups.Count > 1)
                        {
                            logBuilder.AppendLine($"\n═══════════════════════════════");
                            logBuilder.AppendLine($"[批次 {runIndex}/{targetGroups.Count}] 导出目标: {group.Key}");
                            logBuilder.AppendLine($"═══════════════════════════════");
                        }

                        var args = BuildLubanArgsForGroup(group.Key, group.Value, projectRoot);
                        var success = RunLubanOnce(dllPath, workDir, args, logBuilder);

                        if (!success)
                        {
                            allSuccess = false;
                            logBuilder.AppendLine($"[批次 {runIndex}] 生成失败，停止后续批次");
                            break;
                        }

                        // 处理同数据格式多目录的复制
                        CopyDataToExtraDirectoriesForGroup(group.Key, group.Value, logBuilder);
                    }

                    if (allSuccess)
                    {
                        EnsureRequiredFiles(logBuilder);
                        AssetDatabase.Refresh();
                        logBuilder.AppendLine("\n✓ 已刷新 Unity 资源数据库");
                    }
                }

                UpdateStatusBanner(allSuccess ? BuildStatus.Success : BuildStatus.Failed);
            }
            catch (Exception ex)
            {
                logBuilder.AppendLine($"[异常] {ex.Message}");
                logBuilder.AppendLine(ex.StackTrace);
                UpdateStatusBanner(BuildStatus.Failed);
                Debug.LogException(ex);
            }
            finally
            {
                mGenerateBtn.SetEnabled(true);
                mLogContent.value = logBuilder.ToString();
            }
        }

        #endregion

        #region Luban 进程执行

        /// <summary>
        /// 执行单次 Luban 命令
        /// </summary>
        private bool RunLubanOnce(string dllPath, string workDir, string args, StringBuilder logBuilder)
        {
            logBuilder.AppendLine($"命令: dotnet {dllPath}");
            logBuilder.AppendLine($"参数: {args}");
            logBuilder.AppendLine("───────────────────────────────");

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{dllPath}\" {args}",
                WorkingDirectory = workDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = startInfo };
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) outputBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) errorBuilder.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            var exitCode = process.ExitCode;
            logBuilder.AppendLine(outputBuilder.ToString());

            if (!string.IsNullOrEmpty(errorBuilder.ToString()))
            {
                logBuilder.AppendLine("[错误输出]");
                logBuilder.AppendLine(errorBuilder.ToString());
            }

            logBuilder.AppendLine("───────────────────────────────");
            logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss}] 退出码: {exitCode}");

            return exitCode == 0;
        }

        #endregion

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

        #region 单目标生成

        /// <summary>
        /// 生成单个额外输出目标
        /// </summary>
        private void GenerateSingleTarget(int targetIndex)
        {
            if (targetIndex < 0 || targetIndex >= mExtraOutputTargets.Count) return;
            if (!ValidateLubanConfig()) return;

            var extraTarget = mExtraOutputTargets[targetIndex];
            if (!extraTarget.enabled)
            {
                EditorUtility.DisplayDialog("提示", "此目标已禁用", "确定");
                return;
            }

            mGenerateBtn.SetEnabled(false);
            UpdateStatusBanner(BuildStatus.Building);

            var logBuilder = new StringBuilder();
            logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss}] 单独生成目标: {extraTarget.name}");

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var workDir = Path.IsPathRooted(mLubanWorkDir) ? mLubanWorkDir : Path.Combine(projectRoot, mLubanWorkDir);
            var dllPath = Path.IsPathRooted(mLubanDllPath) ? mLubanDllPath : Path.Combine(projectRoot, mLubanDllPath);

            try
            {
                var extraDataDir = string.IsNullOrEmpty(extraTarget.dataDir) ? "" :
                    (Path.IsPathRooted(extraTarget.dataDir) ? extraTarget.dataDir : Path.Combine(projectRoot, extraTarget.dataDir));
                var extraCodeDir = string.IsNullOrEmpty(extraTarget.codeDir) ? "" :
                    (Path.IsPathRooted(extraTarget.codeDir) ? extraTarget.codeDir : Path.Combine(projectRoot, extraTarget.codeDir));

                var output = new OutputConfig
                {
                    IsMain = false,
                    DataTarget = extraTarget.dataTarget,
                    DataDir = extraDataDir,
                    CodeTarget = extraTarget.codeTarget,
                    CodeDir = extraCodeDir
                };

                var outputs = new List<OutputConfig> { output };
                var args = BuildLubanArgsForGroup(extraTarget.target, outputs, projectRoot);
                var success = RunLubanOnce(dllPath, workDir, args, logBuilder);

                if (success)
                {
                    AssetDatabase.Refresh();
                    logBuilder.AppendLine("\n✓ 已刷新 Unity 资源数据库");
                }

                UpdateStatusBanner(success ? BuildStatus.Success : BuildStatus.Failed);
            }
            catch (Exception ex)
            {
                logBuilder.AppendLine($"[异常] {ex.Message}");
                logBuilder.AppendLine(ex.StackTrace);
                UpdateStatusBanner(BuildStatus.Failed);
                Debug.LogException(ex);
            }
            finally
            {
                mGenerateBtn.SetEnabled(true);
                mLogContent.value = logBuilder.ToString();
            }
        }

        #endregion

        #region 配置验证

        private bool ValidateLubanConfig()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var workDir = Path.IsPathRooted(mLubanWorkDir) ? mLubanWorkDir : Path.Combine(projectRoot, mLubanWorkDir);

            if (string.IsNullOrEmpty(mLubanWorkDir) || !Directory.Exists(workDir))
            {
                EditorUtility.DisplayDialog("配置错误", $"Luban 工作目录不存在\n路径: {workDir}", "确定");
                return false;
            }

            if (!File.Exists(Path.Combine(workDir, "luban.conf")))
            {
                EditorUtility.DisplayDialog("配置错误", $"找不到 luban.conf 文件\n路径: {Path.Combine(workDir, "luban.conf")}", "确定");
                return false;
            }

            var dllPath = Path.IsPathRooted(mLubanDllPath) ? mLubanDllPath : Path.Combine(projectRoot, mLubanDllPath);
            if (string.IsNullOrEmpty(mLubanDllPath) || !File.Exists(dllPath))
            {
                EditorUtility.DisplayDialog("配置错误", $"Luban.dll 路径无效\n路径: {dllPath}", "确定");
                return false;
            }

            return true;
        }

        private void OpenLubanFolder()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var workDir = Path.IsPathRooted(mLubanWorkDir) ? mLubanWorkDir : Path.Combine(projectRoot, mLubanWorkDir);

            if (!string.IsNullOrEmpty(workDir) && Directory.Exists(workDir))
                EditorUtility.RevealInFinder(workDir);
            else
                EditorUtility.DisplayDialog("提示", $"Luban 工作目录未配置或不存在\n路径: {workDir}", "确定");
        }

        private void EnsureRequiredFiles(StringBuilder logBuilder)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var codeDir = mOutputCodeDir.StartsWith("Assets/")
                ? Path.Combine(projectRoot, mOutputCodeDir.TrimEnd('/'))
                : mOutputCodeDir;

            var lubanCodeDir = Path.Combine(codeDir, "Luban");
            if (!Directory.Exists(lubanCodeDir)) Directory.CreateDirectory(lubanCodeDir);

            logBuilder.AppendLine("正在生成 TableKit 运行时代码...");
            TableKitCodeGenerator.Generate(codeDir, mUseAssemblyDefinition, mGenerateExternalTypeUtil, mAssemblyName, "cfg", mRuntimePathPattern, mEditorDataPath, mCodeTarget);
            logBuilder.AppendLine("✓ TableKit 运行时代码生成完成");

            if (mGenerateExternalTypeUtil) logBuilder.AppendLine("✓ 已生成 ExternalTypeUtil.cs");
        }

        #endregion
    }
}
#endif
