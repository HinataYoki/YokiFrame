using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 音频 ID 生成器编辑器窗口
    /// </summary>
    public class AudioIdGeneratorWindow : EditorWindow
    {
        private const string WINDOW_NAME = "AudioKit - 音频ID生成器";
        private const string ASSETS_PREFIX = "Assets";

        private static readonly string[] AUDIO_EXTENSIONS = { ".wav", ".mp3", ".ogg", ".aiff", ".aif", ".flac" };

        [MenuItem("YokiFrame/AudioKit/AudioId Generator")]
        private static void Open()
        {
            var window = GetWindow<AudioIdGeneratorWindow>(true, WINDOW_NAME);
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        #region 配置参数

        private string mScanFolder = "Assets/Audio";
        private string mOutputPath = "Assets/Scripts/Generated/AudioIds.cs";
        private string mNamespace = "Game";
        private string mClassName = "AudioIds";
        private int mStartId = 1001;
        private bool mGeneratePathMap = true;
        private bool mGroupByFolder = true;

        // EditorPrefs keys
        private const string PREF_SCAN_FOLDER = "AudioIdGenerator_ScanFolder";
        private const string PREF_OUTPUT_PATH = "AudioIdGenerator_OutputPath";
        private const string PREF_NAMESPACE = "AudioIdGenerator_Namespace";
        private const string PREF_CLASS_NAME = "AudioIdGenerator_ClassName";
        private const string PREF_START_ID = "AudioIdGenerator_StartId";
        private const string PREF_GENERATE_PATH_MAP = "AudioIdGenerator_GeneratePathMap";
        private const string PREF_GROUP_BY_FOLDER = "AudioIdGenerator_GroupByFolder";

        // 扫描结果
        private readonly List<AudioFileInfo> mScannedFiles = new();
        private Vector2 mScrollPosition;
        private bool mHasScanned;

        #endregion

        private void OnEnable()
        {
            LoadPrefs();
        }

        private void OnDisable()
        {
            SavePrefs();
        }

        private void LoadPrefs()
        {
            mScanFolder = EditorPrefs.GetString(PREF_SCAN_FOLDER, "Assets/Audio");
            mOutputPath = EditorPrefs.GetString(PREF_OUTPUT_PATH, "Assets/Scripts/Generated/AudioIds.cs");
            mNamespace = EditorPrefs.GetString(PREF_NAMESPACE, "Game");
            mClassName = EditorPrefs.GetString(PREF_CLASS_NAME, "AudioIds");
            mStartId = EditorPrefs.GetInt(PREF_START_ID, 1001);
            mGeneratePathMap = EditorPrefs.GetBool(PREF_GENERATE_PATH_MAP, true);
            mGroupByFolder = EditorPrefs.GetBool(PREF_GROUP_BY_FOLDER, true);
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(PREF_SCAN_FOLDER, mScanFolder);
            EditorPrefs.SetString(PREF_OUTPUT_PATH, mOutputPath);
            EditorPrefs.SetString(PREF_NAMESPACE, mNamespace);
            EditorPrefs.SetString(PREF_CLASS_NAME, mClassName);
            EditorPrefs.SetInt(PREF_START_ID, mStartId);
            EditorPrefs.SetBool(PREF_GENERATE_PATH_MAP, mGeneratePathMap);
            EditorPrefs.SetBool(PREF_GROUP_BY_FOLDER, mGroupByFolder);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("音频 ID 代码生成器", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawConfigSection();
            EditorGUILayout.Space(10);

            DrawButtonSection();
            EditorGUILayout.Space(10);

            if (mHasScanned)
            {
                DrawPreviewSection();
            }
        }

        private void DrawConfigSection()
        {
            EditorGUILayout.LabelField("扫描配置", EditorStyles.boldLabel);

            // 扫描文件夹
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("扫描文件夹:", GUILayout.Width(80));
            mScanFolder = EditorGUILayout.TextField(mScanFolder);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var folder = EditorUtility.OpenFolderPanel("选择音频文件夹", mScanFolder, "");
                if (!string.IsNullOrEmpty(folder))
                {
                    var assetsIndex = folder.IndexOf(ASSETS_PREFIX, StringComparison.Ordinal);
                    mScanFolder = assetsIndex >= 0 ? folder[assetsIndex..] : folder;
                }
            }
            EditorGUILayout.EndHorizontal();

            // 输出路径
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("输出路径:", GUILayout.Width(80));
            mOutputPath = EditorGUILayout.TextField(mOutputPath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var path = EditorUtility.SaveFilePanel("保存代码文件", Path.GetDirectoryName(mOutputPath), mClassName, "cs");
                if (!string.IsNullOrEmpty(path))
                {
                    var assetsIndex = path.IndexOf(ASSETS_PREFIX, StringComparison.Ordinal);
                    mOutputPath = assetsIndex >= 0 ? path[assetsIndex..] : path;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("代码配置", EditorStyles.boldLabel);

            // 命名空间
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("命名空间:", GUILayout.Width(80));
            mNamespace = EditorGUILayout.TextField(mNamespace);
            EditorGUILayout.EndHorizontal();

            // 类名
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("类名:", GUILayout.Width(80));
            mClassName = EditorGUILayout.TextField(mClassName);
            EditorGUILayout.EndHorizontal();

            // 起始 ID
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("起始 ID:", GUILayout.Width(80));
            mStartId = EditorGUILayout.IntField(mStartId);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("生成选项", EditorStyles.boldLabel);

            mGeneratePathMap = EditorGUILayout.Toggle("生成路径映射字典", mGeneratePathMap);
            mGroupByFolder = EditorGUILayout.Toggle("按文件夹分组", mGroupByFolder);
        }

        private void DrawButtonSection()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("扫描音频文件", GUILayout.Height(30)))
            {
                ScanAudioFiles();
            }

            GUI.enabled = mHasScanned && mScannedFiles.Count > 0;
            if (GUILayout.Button("生成代码", GUILayout.Height(30)))
            {
                GenerateCode();
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField($"扫描结果 ({mScannedFiles.Count} 个文件)", EditorStyles.boldLabel);

            mScrollPosition = EditorGUILayout.BeginScrollView(mScrollPosition, GUILayout.ExpandHeight(true));

            foreach (var file in mScannedFiles)
            {
                EditorGUILayout.BeginHorizontal("box");
                EditorGUILayout.LabelField(file.ConstantName, GUILayout.Width(200));
                EditorGUILayout.LabelField($"= {file.Id}", GUILayout.Width(80));
                EditorGUILayout.LabelField(file.Path, EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void ScanAudioFiles()
        {
            mScannedFiles.Clear();
            mHasScanned = true;

            if (!Directory.Exists(mScanFolder))
            {
                EditorUtility.DisplayDialog("错误", $"文件夹不存在: {mScanFolder}", "确定");
                return;
            }

            var currentId = mStartId;
            var files = Directory.GetFiles(mScanFolder, "*.*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (!IsAudioExtension(ext)) continue;

                var relativePath = file.Replace("\\", "/");
                var assetsIndex = relativePath.IndexOf(ASSETS_PREFIX, StringComparison.Ordinal);
                if (assetsIndex >= 0)
                {
                    relativePath = relativePath[assetsIndex..];
                }

                // 移除扩展名用于 ResKit 加载
                var pathWithoutExt = relativePath[..^ext.Length];

                var fileName = Path.GetFileNameWithoutExtension(file);
                var folderName = GetFolderCategory(relativePath);
                var constantName = GenerateConstantName(fileName, folderName);

                mScannedFiles.Add(new AudioFileInfo
                {
                    Name = fileName,
                    Path = pathWithoutExt,
                    Id = currentId++,
                    ConstantName = constantName,
                    FolderCategory = folderName
                });
            }

            if (mScannedFiles.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "未找到音频文件", "确定");
            }
        }

        private static bool IsAudioExtension(string ext)
        {
            foreach (var audioExt in AUDIO_EXTENSIONS)
            {
                if (ext.Equals(audioExt, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private string GetFolderCategory(string path)
        {
            if (!mGroupByFolder) return string.Empty;

            // 获取相对于扫描文件夹的子文件夹名
            var relativePath = path.Replace(mScanFolder, "").TrimStart('/');
            var parts = relativePath.Split('/');
            return parts.Length > 1 ? parts[0] : string.Empty;
        }

        private static string GenerateConstantName(string fileName, string folderCategory)
        {
            var name = fileName.ToUpperInvariant();

            // 替换非法字符
            name = name.Replace(" ", "_")
                       .Replace("-", "_")
                       .Replace(".", "_");

            // 移除连续下划线
            while (name.Contains("__"))
            {
                name = name.Replace("__", "_");
            }

            // 确保不以数字开头
            if (char.IsDigit(name[0]))
            {
                name = "_" + name;
            }

            // 添加文件夹前缀
            if (!string.IsNullOrEmpty(folderCategory))
            {
                var prefix = folderCategory.ToUpperInvariant().Replace(" ", "_").Replace("-", "_");
                name = $"{prefix}_{name}";
            }

            return name;
        }

        private void GenerateCode()
        {
            if (mScannedFiles.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "没有可生成的音频文件", "确定");
                return;
            }

            var directory = Path.GetDirectoryName(mOutputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var code = AudioIdCodeGenerator.Generate(
                mScannedFiles,
                mNamespace,
                mClassName,
                mGeneratePathMap,
                mGroupByFolder
            );

            File.WriteAllText(mOutputPath, code);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("成功", $"代码已生成到:\n{mOutputPath}", "确定");
        }
    }

    /// <summary>
    /// 音频文件信息
    /// </summary>
    internal struct AudioFileInfo
    {
        public string Name;
        public string Path;
        public int Id;
        public string ConstantName;
        public string FolderCategory;
    }
}
