#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace YokiFrame
{
    /// <summary>
    /// 音频 ID 生成器 - 扫描与生成逻辑
    /// </summary>
    public partial class AudioIdGeneratorWindow
    {
        #region 扫描与生成逻辑

        /// <summary>
        /// 扫描音频文件
        /// </summary>
        private void ScanAudioFiles()
        {
            mScannedFiles.Clear();
            mHasScanned = false;

            if (!Directory.Exists(mScanFolder))
            {
                EditorUtility.DisplayDialog("错误", $"扫描文件夹不存在：{mScanFolder}", "确定");
                return;
            }

            var files = Directory.GetFiles(mScanFolder, "*.*", SearchOption.AllDirectories);
            int currentId = mStartId;

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (!IsAudioExtension(ext)) continue;

                var relativePath = file.Replace("\\", "/");
                var fileName = Path.GetFileNameWithoutExtension(file);
                var category = mGroupByFolder ? GetFolderCategory(relativePath) : string.Empty;
                var constantName = GenerateConstantName(fileName, category);

                mScannedFiles.Add(new AudioFileInfo
                {
                    Id = currentId++,
                    Name = fileName,
                    Path = relativePath,
                    ConstantName = constantName,
                    FolderCategory = category
                });
            }

            mHasScanned = true;

            // 更新 UI
            mResultsContainer.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
            mResultsCountLabel.text = $"共 {mScannedFiles.Count} 个文件";
            mResultsListView.itemsSource = mScannedFiles;
            mResultsListView.RefreshItems();
            mGenerateButton.SetEnabled(mScannedFiles.Count > 0);

            if (mScannedFiles.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "未找到任何音频文件", "确定");
            }
        }

        /// <summary>
        /// 生成代码
        /// </summary>
        private void GenerateCode()
        {
            if (!mHasScanned || mScannedFiles.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先扫描音频文件", "确定");
                return;
            }

            var outputDir = Path.GetDirectoryName(mOutputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            using var writer = new StreamWriter(mOutputPath);

            // 文件头
            writer.WriteLine("// 此文件由 AudioIdGenerator 自动生成，请勿手动修改");
            writer.WriteLine($"// 生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine();

            // 命名空间
            if (!string.IsNullOrEmpty(mNamespace))
            {
                writer.WriteLine($"namespace {mNamespace}");
                writer.WriteLine("{");
            }

            // 类定义
            var indent = string.IsNullOrEmpty(mNamespace) ? "" : "    ";
            writer.WriteLine($"{indent}/// <summary>");
            writer.WriteLine($"{indent}/// 音频 ID 常量定义");
            writer.WriteLine($"{indent}/// </summary>");
            writer.WriteLine($"{indent}public static class {mClassName}");
            writer.WriteLine($"{indent}{{");

            // 输出常量
            WriteConstants(writer, indent);

            // 路径映射字典
            if (mGeneratePathMap)
            {
                WritePathMap(writer, indent);
            }

            writer.WriteLine($"{indent}}}");

            if (!string.IsNullOrEmpty(mNamespace))
            {
                writer.WriteLine("}");
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("完成", $"代码已生成到：\n{mOutputPath}", "确定");
        }

        private void WriteConstants(StreamWriter writer, string indent)
        {
            if (mGroupByFolder)
            {
                var groups = new Dictionary<string, List<AudioFileInfo>>(16);
                foreach (var info in mScannedFiles)
                {
                    var category = string.IsNullOrEmpty(info.FolderCategory) ? "General" : info.FolderCategory;
                    if (!groups.TryGetValue(category, out var list))
                    {
                        list = new List<AudioFileInfo>(16);
                        groups[category] = list;
                    }
                    list.Add(info);
                }

                foreach (var kvp in groups)
                {
                    writer.WriteLine($"{indent}    #region {kvp.Key}");
                    writer.WriteLine();
                    foreach (var info in kvp.Value)
                    {
                        writer.WriteLine($"{indent}    public const int {info.ConstantName} = {info.Id};");
                    }
                    writer.WriteLine();
                    writer.WriteLine($"{indent}    #endregion");
                    writer.WriteLine();
                }
            }
            else
            {
                foreach (var info in mScannedFiles)
                {
                    writer.WriteLine($"{indent}    public const int {info.ConstantName} = {info.Id};");
                }
                writer.WriteLine();
            }
        }

        private void WritePathMap(StreamWriter writer, string indent)
        {
            writer.WriteLine($"{indent}    /// <summary>");
            writer.WriteLine($"{indent}    /// ID 到路径的映射");
            writer.WriteLine($"{indent}    /// </summary>");
            writer.WriteLine($"{indent}    public static readonly System.Collections.Generic.Dictionary<int, string> PathMap = new()");
            writer.WriteLine($"{indent}    {{");
            foreach (var info in mScannedFiles)
            {
                writer.WriteLine($"{indent}        {{ {info.ConstantName}, \"{info.Path}\" }},");
            }
            writer.WriteLine($"{indent}    }};");
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 检查是否为音频扩展名
        /// </summary>
        private static bool IsAudioExtension(string ext)
        {
            foreach (var audioExt in AUDIO_EXTENSIONS)
            {
                if (ext == audioExt) return true;
            }
            return false;
        }

        /// <summary>
        /// 获取文件夹分类名
        /// </summary>
        private string GetFolderCategory(string path)
        {
            var relativePath = path;
            if (relativePath.StartsWith(mScanFolder))
            {
                relativePath = relativePath[(mScanFolder.Length + 1)..];
            }

            var dirName = Path.GetDirectoryName(relativePath);
            if (string.IsNullOrEmpty(dirName)) return string.Empty;

            // 取第一级目录作为分类
            var parts = dirName.Replace("\\", "/").Split('/');
            return parts.Length > 0 ? parts[0] : string.Empty;
        }

        /// <summary>
        /// 生成常量名
        /// </summary>
        private static string GenerateConstantName(string fileName, string category)
        {
            // 移除非法字符，转换为大写下划线格式
            var name = fileName.Replace(" ", "_").Replace("-", "_");
            var result = new System.Text.StringBuilder(name.Length + 16);

            // 添加分类前缀
            if (!string.IsNullOrEmpty(category))
            {
                result.Append(category.ToUpperInvariant());
                result.Append('_');
            }

            // 转换为 UPPER_SNAKE_CASE
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsLetterOrDigit(c))
                {
                    // 在大写字母前添加下划线（驼峰转换）
                    if (i > 0 && char.IsUpper(c) && char.IsLower(name[i - 1]))
                    {
                        result.Append('_');
                    }
                    result.Append(char.ToUpperInvariant(c));
                }
                else if (c == '_')
                {
                    result.Append('_');
                }
            }

            // 确保不以数字开头
            if (result.Length > 0 && char.IsDigit(result[0]))
            {
                result.Insert(0, "AUDIO_");
            }

            return result.ToString();
        }

        #endregion
    }
}
#endif
