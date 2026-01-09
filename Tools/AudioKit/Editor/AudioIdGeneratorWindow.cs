#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// 音频 ID 生成器编辑器窗口
    /// 使用 UI Toolkit 实现，支持 Unity 2021.3+
    /// </summary>
    public partial class AudioIdGeneratorWindow : EditorWindow
    {
        #region 常量

        private const string WINDOW_NAME = "AudioKit - 音频ID生成器";
        private const string ASSETS_PREFIX = "Assets";
        private const int WINDOW_MIN_WIDTH = 600;
        private const int WINDOW_MIN_HEIGHT = 500;
        private const int LABEL_WIDTH = 90;

        private static readonly string[] AUDIO_EXTENSIONS = { ".wav", ".mp3", ".ogg", ".aiff", ".aif", ".flac" };

        // EditorPrefs keys
        private const string PREF_SCAN_FOLDER = "AudioIdGenerator_ScanFolder";
        private const string PREF_OUTPUT_PATH = "AudioIdGenerator_OutputPath";
        private const string PREF_NAMESPACE = "AudioIdGenerator_Namespace";
        private const string PREF_CLASS_NAME = "AudioIdGenerator_ClassName";
        private const string PREF_START_ID = "AudioIdGenerator_StartId";
        private const string PREF_GENERATE_PATH_MAP = "AudioIdGenerator_GeneratePathMap";
        private const string PREF_GROUP_BY_FOLDER = "AudioIdGenerator_GroupByFolder";

        #endregion

        #region 配置字段

        private string mScanFolder = "Assets/Audio";
        private string mOutputPath = "Assets/Scripts/Generated/AudioIds.cs";
        private string mNamespace = "Game";
        private string mClassName = "AudioIds";
        private int mStartId = 1001;
        private bool mGeneratePathMap = true;
        private bool mGroupByFolder = true;

        #endregion

        #region 扫描结果

        private readonly List<AudioFileInfo> mScannedFiles = new(64);
        private bool mHasScanned;

        #endregion

        #region UI 元素引用

        private TextField mScanFolderField;
        private TextField mOutputPathField;
        private TextField mNamespaceField;
        private TextField mClassNameField;
        private TextField mStartIdField;
        private Toggle mGeneratePathMapToggle;
        private Toggle mGroupByFolderToggle;
        private Button mScanButton;
        private Button mGenerateButton;
        private VisualElement mResultsContainer;
        private ListView mResultsListView;
        private Label mResultsCountLabel;

        #endregion

        #region 窗口入口

        public static void Open()
        {
            var window = GetWindow<AudioIdGeneratorWindow>(true, WINDOW_NAME);
            window.minSize = new Vector2(WINDOW_MIN_WIDTH, WINDOW_MIN_HEIGHT);
            window.Show();
        }

        #endregion

        #region 生命周期

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

        #endregion

        #region UI Toolkit 入口

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingTop = 12;
            root.style.paddingBottom = 12;
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;
            root.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f));

            // 标题
            var title = new Label("音频 ID 代码生成器");
            title.style.fontSize = 16;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 12;
            title.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
            root.Add(title);

            // 构建各区块
            BuildScanConfigSection(root);
            BuildCodeConfigSection(root);
            BuildOptionsSection(root);
            BuildButtonSection(root);
            BuildResultsSection(root);
        }

        #endregion
    }
}
#endif
