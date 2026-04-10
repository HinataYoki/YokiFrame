#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// AudioKit 音频 ID 生成器窗口的扫描与生成逻辑。
    /// </summary>
    public partial class AudioIdGeneratorWindow
    {
        #region 扫描与生成逻辑

        /// <summary>
        /// 扫描目标目录中的音频文件，并刷新结果列表。
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
                if (!IsAudioExtension(ext))
                {
                    continue;
                }

                var relativePath = file.Replace("\\", "/");
                var assetsIndex = relativePath.IndexOf(ASSETS_PREFIX, StringComparison.Ordinal);
                if (assetsIndex >= 0)
                {
                    relativePath = relativePath[assetsIndex..];
                }

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

            mResultsContainer.style.display = DisplayStyle.Flex;
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
        /// 根据扫描结果生成代码文件并刷新 AssetDatabase。
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

            var code = AudioIdCodeGenerator.Generate(
                mScannedFiles,
                mNamespace,
                mClassName,
                mGeneratePathMap,
                mGroupByFolder);

            File.WriteAllText(mOutputPath, code);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("完成", $"代码已生成到：\n{mOutputPath}", "确定");
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 检查指定扩展名是否属于音频文件。
        /// </summary>
        private static bool IsAudioExtension(string ext)
        {
            foreach (var audioExt in AUDIO_EXTENSIONS)
            {
                if (ext == audioExt)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 根据扫描相对路径推导一级文件夹分类名。
        /// </summary>
        private string GetFolderCategory(string path)
        {
            var relativePath = path;
            if (relativePath.StartsWith(mScanFolder))
            {
                relativePath = relativePath[(mScanFolder.Length + 1)..];
            }

            var dirName = Path.GetDirectoryName(relativePath);
            if (string.IsNullOrEmpty(dirName))
            {
                return string.Empty;
            }

            var parts = dirName.Replace("\\", "/").Split('/');
            return parts.Length > 0 ? parts[0] : string.Empty;
        }

        /// <summary>
        /// 根据文件名和分类生成 `UPPER_SNAKE_CASE` 常量名。
        /// </summary>
        private static string GenerateConstantName(string fileName, string category)
        {
            var name = fileName.Replace(" ", "_").Replace("-", "_");
            var result = new System.Text.StringBuilder(name.Length + 16);

            if (!string.IsNullOrEmpty(category))
            {
                result.Append(category.ToUpperInvariant());
                result.Append('_');
            }

            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsLetterOrDigit(c))
                {
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
