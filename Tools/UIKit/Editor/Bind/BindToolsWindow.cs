#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// 批量绑定工具窗口 - 提供批量添加 Bind 组件的功能
    /// </summary>
    public partial class BindToolsWindow : EditorWindow
    {
        #region 常量

        private const string WINDOW_TITLE = "UIKit 批量绑定工具";
        private const int MIN_WIDTH = 400;
        private const int MIN_HEIGHT = 500;

        #endregion

        #region 字段

        // 配置选项
        private bool mRecursive = true;
        private BindType mDefaultType = BindType.Member;
        private bool mAutoSuggestName = true;

        // UI 元素
        private VisualElement mRoot;
        private Label mSelectionCountLabel;
        private VisualElement mPreviewContainer;
        private Button mExecuteBtn;
        private Label mResultLabel;

        // 预览数据
        private readonly List<BindPreviewItem> mPreviewItems = new(32);

        #endregion

        #region 窗口生命周期

        /// <summary>
        /// 打开批量绑定工具窗口
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

            // 加载样式
            var styleSheet = YokiFrameEditorUtility.LoadStyleSheetByName("BindToolsWindowStyles");
            if (styleSheet != null)
            {
                mRoot.styleSheets.Add(styleSheet);
            }

            // 标题
            var title = new Label(WINDOW_TITLE);
            title.AddToClassList("window-title");
            mRoot.Add(title);

            // 选中对象信息
            CreateSelectionSection();

            // 配置选项
            CreateOptionsSection();

            // 预览区域
            CreatePreviewSection();

            // 操作按钮
            CreateActionButtons();

            // 结果显示
            mResultLabel = new Label();
            mResultLabel.AddToClassList("result-label");
            mRoot.Add(mResultLabel);

            // 初始化
            RefreshPreview();
        }

        #endregion

        #region 数据结构

        /// <summary>
        /// 绑定预览项
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
