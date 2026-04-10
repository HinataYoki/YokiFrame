#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// 批量绑定工具窗口。
    /// 用于对当前选择的 GameObject 集合批量添加 Bind 组件，并在执行前给出预览结果。
    /// </summary>
    public partial class BindToolsWindow : EditorWindow
    {
        #region 常量

        private const string WINDOW_TITLE = "UIKit 批量绑定工具";
        private const int MIN_WIDTH = 400;
        private const int MIN_HEIGHT = 500;

        #endregion

        #region 字段

        private bool mRecursive = true;
        private BindType mDefaultType = BindType.Member;
        private bool mAutoSuggestName = true;

        private VisualElement mRoot;
        private Label mSelectionCountLabel;
        private VisualElement mPreviewContainer;
        private Button mExecuteBtn;
        private Label mResultLabel;

        private readonly List<BindPreviewItem> mPreviewItems = new(32);

        #endregion

        #region 窗口生命周期

        /// <summary>
        /// 打开批量绑定工具窗口。
        /// </summary>
        public static void ShowWindow()
        {
            var window = GetWindow<BindToolsWindow>();
            window.titleContent = new GUIContent(WINDOW_TITLE);
            window.minSize = new Vector2(MIN_WIDTH, MIN_HEIGHT);
            window.Show();
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void CreateGUI()
        {
            mRoot = rootVisualElement;
            mRoot.AddToClassList("bind-tools-window");

            var styleSheet = YokiFrameEditorUtility.LoadStyleSheetByName("BindToolsWindowStyles");
            if (styleSheet != null)
            {
                mRoot.styleSheets.Add(styleSheet);
            }

            var title = new Label(WINDOW_TITLE);
            title.AddToClassList("window-title");
            mRoot.Add(title);

            CreateSelectionSection();
            CreateOptionsSection();
            CreatePreviewSection();
            CreateActionButtons();

            mResultLabel = new Label();
            mResultLabel.AddToClassList("result-label");
            mRoot.Add(mResultLabel);

            RefreshPreview();
        }

        #endregion

        #region 数据结构

        /// <summary>
        /// 单个绑定候选项的预览数据。
        /// </summary>
        private struct BindPreviewItem
        {
            public GameObject GameObject;
            public string OriginalName;
            public string SuggestedName;
            public string ComponentType;
            public bool AlreadyHasBind;
        }

        #endregion
    }
}
#endif
