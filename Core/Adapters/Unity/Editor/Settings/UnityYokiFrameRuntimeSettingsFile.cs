#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using YokiFrame.Unity;

namespace YokiFrame
{
    /// <summary>
    /// 读写当前 Unity 项目的 Runtime Settings JSON；文件始终受当前 Application.dataPath 隔离。
    /// </summary>
    public static class UnityYokiFrameRuntimeSettingsFile
    {
        /// <summary>Unity 项目内运行时配置的稳定 Asset 路径。</summary>
        public const string ASSET_PATH = "Assets/Settings/Resources/YokiFrame/runtime-settings.json";

        /// <summary>
        /// 尝试读取当前项目 Runtime Settings 原文；文件不存在时返回 false，不自动创建默认文件。
        /// </summary>
        /// <param name="json">存在配置时返回 JSON 原文。</param>
        /// <returns>当前项目存在配置文件时返回 true。</returns>
        public static bool TryRead(out string json)
        {
            string absolutePath = GetAbsolutePath();
            if (!File.Exists(absolutePath))
            {
                json = string.Empty;
                return false;
            }

            json = File.ReadAllText(absolutePath, Encoding.UTF8);
            return true;
        }

        /// <summary>
        /// 校验并原子保存当前项目 Runtime Settings，避免损坏文件或写入其它 Unity 项目。
        /// </summary>
        /// <param name="json">符合统一格式的完整 JSON。</param>
        public static void Save(string json)
        {
            if (!UnityYokiFrameRuntimeSettingsLoader.TryParse(json, out var store, out var errorMessage))
            {
                throw new ArgumentException(errorMessage, nameof(json));
            }

            if (ContainsEditorOnlyLogKitSetting(store))
            {
                throw new ArgumentException(
                    "Unity Editor 专属 LogKit 设置必须保存到 Editor 项目配置，不能写入 Player Resources。",
                    nameof(json));
            }

            string absolutePath = GetAbsolutePath();
            string directoryPath = Path.GetDirectoryName(absolutePath);
            Directory.CreateDirectory(directoryPath);
            WriteAtomically(absolutePath, json);
            AssetDatabase.ImportAsset(ASSET_PATH, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>
        /// 检查候选 Runtime Settings 是否混入只允许 Editor/Tools 使用的 LogKit 文件设置。
        /// </summary>
        /// <param name="store">已经完整解析的候选运行时设置。</param>
        /// <returns>存在任一 Editor 专属字段时返回 true。</returns>
        private static bool ContainsEditorOnlyLogKitSetting(YokiFrameRuntimeSettingsStore store)
        {
            return store.TryGetValue(LogKitSettings.KIT_NAME, LogKitSettings.SAVE_LOG_IN_EDITOR_KEY, out _)
                   || store.TryGetValue(LogKitSettings.KIT_NAME, LogKitSettings.EDITOR_FILE_NAME_KEY, out _);
        }

        /// <summary>
        /// 解析固定 Asset 路径并验证结果仍位于当前项目 Assets 下，阻止跨项目写入。
        /// </summary>
        /// <returns>当前项目 Runtime Settings 绝对路径。</returns>
        private static string GetAbsolutePath()
        {
            string assetsRoot = Path.GetFullPath(Application.dataPath);
            string relativePath = ASSET_PATH.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar);
            string absolutePath = Path.GetFullPath(Path.Combine(assetsRoot, relativePath));
            string containmentRoot = assetsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                     + Path.DirectorySeparatorChar;
            if (!absolutePath.StartsWith(containmentRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("YokiFrame Runtime Settings 路径越出当前 Unity 项目 Assets。");
            }

            return absolutePath;
        }

        /// <summary>
        /// 通过同目录临时文件、落盘 flush 和原子替换提交 JSON；失败时保留原正式文件。
        /// </summary>
        /// <param name="targetPath">正式配置绝对路径。</param>
        /// <param name="json">待写入 JSON。</param>
        private static void WriteAtomically(string targetPath, string json)
        {
            string temporaryPath = targetPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                WriteTemporaryFile(temporaryPath, json);
                if (File.Exists(targetPath))
                {
                    File.Replace(temporaryPath, targetPath, null);
                }
                else
                {
                    File.Move(temporaryPath, targetPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        /// <summary>
        /// 使用无 BOM UTF-8 写入临时文件，并强制刷新到磁盘后再参与原子替换。
        /// </summary>
        /// <param name="path">临时文件路径。</param>
        /// <param name="json">待写入 JSON。</param>
        private static void WriteTemporaryFile(string path, string json)
        {
            using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using StreamWriter writer = new(stream, new UTF8Encoding(false));
            writer.Write(json);
            writer.Flush();
            stream.Flush(true);
        }
    }
}
#endif
