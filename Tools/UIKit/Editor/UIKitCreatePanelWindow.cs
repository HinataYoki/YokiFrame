#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// UIKit 面板创建窗口
    /// 使用 UI Toolkit 实现，支持 Unity 2021.3+
    /// </summary>
    public partial class UIKitCreatePanelWindow : EditorWindow
    {
        #region 常量

        private const string WINDOW_NAME = "YokiFrame_UI创建窗口";
        private const string ASSETS_PREFIX = "Assets";
        private const int WINDOW_WIDTH = 500;
        private const int WINDOW_HEIGHT = 600;
        private const int LABEL_WIDTH = 100;

        #endregion

        #region 配置属性

        private string PrefabGeneratePath
        {
            get => UIKitCreateConfig.Instance.PrefabGeneratePath;
            set => UIKitCreateConfig.Instance.PrefabGeneratePath = value;
        }

        private string ScriptGeneratePath
        {
            get => UIKitCreateConfig.Instance.ScriptGeneratePath;
            set => UIKitCreateConfig.Instance.ScriptGeneratePath = value;
        }

        private string ScriptNamespace
        {
            get => UIKitCreateConfig.Instance.ScriptNamespace;
            set => UIKitCreateConfig.Instance.ScriptNamespace = value;
        }

        private string AssemblyName
        {
            get => UIKitCreateConfig.Instance.AssemblyName;
            set => UIKitCreateConfig.Instance.AssemblyName = value;
        }

        #endregion

        #region 面板配置字段

        private string mPanelCreateName = string.Empty;
        private UILevel mSelectedLevel = UILevel.Common;
        private bool mIsModal;
        private bool mGenerateLifecycleHooks = true;
        private bool mGenerateFocusSupport;

        #endregion

        #region 动画配置字段

        private AnimationType mShowAnimationType = AnimationType.None;
        private AnimationType mHideAnimationType = AnimationType.None;
        private float mAnimationDuration = 0.3f;

        /// <summary>
        /// 动画类型枚举
        /// </summary>
        private enum AnimationType
        {
            None,
            Fade,
            Scale,
            SlideFromLeft,
            SlideFromRight,
            SlideFromTop,
            SlideFromBottom
        }

        #endregion

        #region UI 元素引用

        private TextField mAssemblyField;
        private TextField mNamespaceField;
        private TextField mScriptPathField;
        private TextField mPrefabPathField;
        private TextField mPanelNameField;
        private EnumField mLevelField;
        private Toggle mModalToggle;
        private Toggle mLifecycleToggle;
        private Toggle mFocusToggle;
        private Foldout mAnimationFoldout;
        private EnumField mShowAnimField;
        private EnumField mHideAnimField;
        private Slider mDurationSlider;
        private VisualElement mDurationRow;
        private VisualElement mPreviewContainer;
        private Button mCreateButton;

        #endregion

        #region 路径属性

        private string PrefabName => $"{mPanelCreateName}.prefab";
        private string ScriptName => $"{mPanelCreateName}.cs";
        private string DesignerName => $"{mPanelCreateName}.Designer.cs";
        private string PrefabPath => $"{PrefabGeneratePath}/{PrefabName}";
        private string ScriptPath => $"{ScriptGeneratePath}/{mPanelCreateName}/{ScriptName}";
        private string DesignerPath => $"{ScriptGeneratePath}/{mPanelCreateName}/{DesignerName}";

        #endregion

        #region 窗口入口

        /// <summary>
        /// 打开创建面板窗口
        /// </summary>
        public static void Open()
        {
            var window = GetWindow<UIKitCreatePanelWindow>(true, WINDOW_NAME);
            window.minSize = new Vector2(WINDOW_WIDTH, WINDOW_HEIGHT);
            window.maxSize = new Vector2(WINDOW_WIDTH + 100, WINDOW_HEIGHT + 200);
            window.Show();
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

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            root.Add(scrollView);

            // 构建各区块
            BuildAssemblySection(scrollView);
            BuildNamespaceSection(scrollView);
            BuildPathsSection(scrollView);
            BuildPanelConfigSection(scrollView);
            BuildAnimationSection(scrollView);
            BuildPreviewSection(scrollView);
            BuildCreateButton(scrollView);
        }

        #endregion

        #region 创建逻辑

        /// <summary>
        /// 创建 UI 预制体
        /// </summary>
        private void OnCreateUIPanelClick()
        {
            var panelName = mPanelCreateName;
            if (string.IsNullOrEmpty(panelName)) return;

            var uiKitPrefab = Resources.Load<GameObject>(nameof(UIKit));
            var uikit = Instantiate(uiKitPrefab);
            var uiRoot = uikit.GetComponentInChildren<UIRoot>();

            if (uiRoot == null)
            {
                KitLogger.Error("UIKit预制体中不包含UIRoot组件!");
                DestroyImmediate(uikit);
                return;
            }

            var gameObj = new GameObject(Path.GetFileNameWithoutExtension(panelName))
            {
                transform =
                {
                    parent = uiRoot.transform,
                    localScale = Vector3.one
                }
            };

            var rect = gameObj.AddComponent<RectTransform>();
            rect.anchoredPosition3D = Vector3.zero;
            rect.localEulerAngles = Vector3.zero;
            rect.localScale = Vector3.one;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(gameObj, PrefabPath, InteractionMode.AutomatedAction);

            // 使用增强的代码生成
            var options = new PanelCodeGenOptions
            {
                Level = mSelectedLevel,
                IsModal = mIsModal,
                GenerateLifecycleHooks = mGenerateLifecycleHooks,
                GenerateFocusSupport = mGenerateFocusSupport,
                ShowAnimationType = GetAnimationTypeName(mShowAnimationType),
                HideAnimationType = GetAnimationTypeName(mHideAnimationType),
                AnimationDuration = mAnimationDuration
            };

            UICodeGenerator.DoCreateCode(prefab, ScriptNamespace, options);

            DestroyImmediate(gameObj);
            DestroyImmediate(uikit);

            Close();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 获取动画类型名称
        /// </summary>
        private static string GetAnimationTypeName(AnimationType type) => type switch
        {
            AnimationType.Fade => "Fade",
            AnimationType.Scale => "Scale",
            AnimationType.SlideFromLeft => "SlideFromLeft",
            AnimationType.SlideFromRight => "SlideFromRight",
            AnimationType.SlideFromTop => "SlideFromTop",
            AnimationType.SlideFromBottom => "SlideFromBottom",
            _ => null
        };

        #endregion
    }
}
#endif
