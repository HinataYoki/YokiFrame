#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame
{
    /// <summary>
    /// UIKitToolPage - 创建面板功能
    /// 采用主次分离布局：折叠式配置舱 + 核心创作区 + 底部信息栏
    /// </summary>
    public partial class UIKitToolPage
    {
        #region 常量 - 样式

        private const int INPUT_HEIGHT = 30;
        private const int HERO_INPUT_HEIGHT = 40;
        private const int HERO_INPUT_FONT_SIZE = 16;
        private const int BUTTON_HEIGHT = 38;
        private const int ICON_SIZE = 14;
        private const int SMALL_ICON_SIZE = 12;

        #endregion

        #region 字段 - 创建面板

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

        private string mPanelCreateName = string.Empty;
        private const string ASSETS = "Assets";

        // UI 元素引用
        private Foldout mSettingsFoldout;
        private DropdownField mAssemblyDropdown;
        private List<string> mAssemblyNames;
        private DropdownField mTemplateDropdown;
        private List<string> mTemplateNames;
        private TextField mNamespaceField;
        private TextField mScriptPathField;
        private TextField mPrefabPathField;
        private TextField mPanelNameField;
        private Label mPreviewLabel;
        private Label mValidationLabel;
        private VisualElement mPreviewContainer;
        private Button mCreateButton;

        private string PrefabName => $"{mPanelCreateName}.prefab";
        private string ScriptName => $"{mPanelCreateName}.cs";
        private string DesignerName => $"{mPanelCreateName}.Designer.cs";
        private string PrefabPath => $"{PrefabGeneratePath}/{PrefabName}";
        private string ScriptPath => $"{ScriptGeneratePath}/{mPanelCreateName}/{ScriptName}";
        private string DesignerPath => $"{ScriptGeneratePath}/{mPanelCreateName}/{DesignerName}";

        #endregion

        #region 创建面板 UI - 主入口

        private void BuildCreatePanelUI(VisualElement container)
        {
            var scrollView = new ScrollView();
            scrollView.AddToClassList("yoki-ui-create-panel");
            container.Add(scrollView);

            // 区域 1: 折叠式配置舱
            scrollView.Add(BuildSettingsDeck());

            // 区域 2: 核心创作区
            scrollView.Add(BuildCreationZone());

            // 区域 3: 底部信息栏
            scrollView.Add(BuildInfoFooter());
        }

        #endregion

        #region 区域 2: 核心创作区

        /// <summary>
        /// 构建核心创作区
        /// </summary>
        private VisualElement BuildCreationZone()
        {
            var zone = new VisualElement();
            zone.AddToClassList("yoki-create-zone");

            // 标题
            var titleRow = new VisualElement();
            titleRow.AddToClassList("yoki-create-title-row");

            var titleIcon = new Image { image = KitIcons.GetTexture(KitIcons.UIKIT) };
            titleIcon.AddToClassList("yoki-create-title-icon");
            titleRow.Add(titleIcon);

            var title = new Label("创建 UI 面板");
            title.AddToClassList("yoki-create-title");
            title.style.color = new StyleColor(Colors.TextPrimary);
            titleRow.Add(title);

            zone.Add(titleRow);

            // Panel 名称输入框
            var inputLabel = new Label("Panel 名称");
            inputLabel.AddToClassList("yoki-create-input-label");
            inputLabel.style.color = new StyleColor(Colors.TextSecondary);
            zone.Add(inputLabel);

            mPanelNameField = new TextField();
            mPanelNameField.AddToClassList("yoki-create-hero-input");
            mPanelNameField.style.marginBottom = Spacing.SM;

            // 设置 placeholder
            var textInput = mPanelNameField.Q<TextElement>();
            if (textInput != null)
            {
                textInput.style.unityTextAlign = TextAnchor.MiddleLeft;
            }

            mPanelNameField.RegisterValueChangedCallback(evt =>
            {
                mPanelCreateName = evt.newValue;
                ValidatePanelName();
                UpdateLivePreview();
            });
            zone.Add(mPanelNameField);

            // 验证提示标签
            mValidationLabel = new Label();
            mValidationLabel.AddToClassList("yoki-create-validation-label");
            zone.Add(mValidationLabel);

            // 实时预览标签
            mPreviewLabel = new Label("输入 Panel 名称开始创建...");
            mPreviewLabel.AddToClassList("yoki-create-preview-label");
            mPreviewLabel.style.color = new StyleColor(Colors.TextTertiary);
            zone.Add(mPreviewLabel);

            // 文件预览容器
            mPreviewContainer = new VisualElement();
            mPreviewContainer.AddToClassList("yoki-create-preview-container");
            zone.Add(mPreviewContainer);

            // 创建按钮
            mCreateButton = new Button(OnCreateUIPanelClick);
            mCreateButton.AddToClassList("yoki-create-primary-button");
            mCreateButton.style.height = BUTTON_HEIGHT;
            mCreateButton.style.display = DisplayStyle.None;

            var btnContent = new VisualElement();
            btnContent.AddToClassList("yoki-create-button-content");

            var btnIcon = new Image { image = KitIcons.GetTexture(KitIcons.CODEGEN) };
            btnIcon.AddToClassList("yoki-create-button-icon");
            btnContent.Add(btnIcon);

            var btnLabel = new Label("创建 UI 面板");
            btnLabel.AddToClassList("yoki-create-button-label");
            btnContent.Add(btnLabel);

            mCreateButton.Add(btnContent);
            zone.Add(mCreateButton);

            return zone;
        }

        /// <summary>
        /// 更新实时预览
        /// </summary>
        private void UpdateLivePreview()
        {
            if (string.IsNullOrEmpty(mPanelCreateName))
            {
                mPreviewLabel.text = "输入 Panel 名称开始创建...";
                mPreviewLabel.style.color = new StyleColor(Colors.TextTertiary);
                mPreviewContainer.style.display = DisplayStyle.None;
                mCreateButton.style.display = DisplayStyle.None;
                return;
            }

            // 验证失败时不显示预览
            if (!YokiFrameEditorUtility.IsValidCSharpIdentifier(mPanelCreateName) || 
                YokiFrameEditorUtility.IsCSharpKeyword(mPanelCreateName))
            {
                mPreviewLabel.text = "请修正名称后继续...";
                mPreviewLabel.style.color = new StyleColor(Colors.StatusWarning);
                mPreviewContainer.style.display = DisplayStyle.None;
                mCreateButton.style.display = DisplayStyle.None;
                return;
            }

            // 更新预览文本
            mPreviewLabel.text = $"将生成: {ScriptNamespace}.{mPanelCreateName} : UIPanel";
            mPreviewLabel.style.color = new StyleColor(Colors.StatusInfo);

            // 更新文件预览
            UpdateFilePreview();
        }

        /// <summary>
        /// 更新文件预览
        /// </summary>
        private void UpdateFilePreview()
        {
            mPreviewContainer.Clear();
            mPreviewContainer.style.display = DisplayStyle.Flex;

            var prefabExists = File.Exists(PrefabPath);
            var scriptExists = File.Exists(ScriptPath);
            var designerExists = File.Exists(DesignerPath);

            // 使用公共组件创建文件预览行
            mPreviewContainer.Add(CreateFilePreviewRow(PrefabPath, prefabExists));
            mPreviewContainer.Add(CreateFilePreviewRow(ScriptPath, scriptExists));
            mPreviewContainer.Add(CreateFilePreviewRow(DesignerPath, designerExists));

            // 只有当 Prefab 不存在时才显示创建按钮
            mCreateButton.style.display = prefabExists ? DisplayStyle.None : DisplayStyle.Flex;
        }

        #endregion

        #region 区域 3: 底部信息栏

        /// <summary>
        /// 构建底部信息栏 - 使用公共组件
        /// </summary>
        private VisualElement BuildInfoFooter()
        {
            return CreateInfoFooter(
                "默认加载路径",
                "默认从 Resources/Art/UIPrefab/ 加载  |  " +
                "修改路径: DefaultPanelLoaderPool.PathPrefix  |  " +
                "自定义加载: 继承 IPanelLoaderPool",
                Colors.BrandPrimary
            );
        }

        #endregion

        #region 创建逻辑

        /// <summary>
        /// 兼容旧方法名
        /// </summary>
        private void UpdatePreview() => UpdateLivePreview();

        /// <summary>
        /// 验证面板名称
        /// </summary>
        /// <returns>是否有效</returns>
        private bool ValidatePanelName()
        {
            if (string.IsNullOrEmpty(mPanelCreateName))
            {
                mValidationLabel.style.display = DisplayStyle.None;
                return false;
            }

            if (!YokiFrameEditorUtility.ValidateCSharpIdentifier(mPanelCreateName, out var errorMessage, out var suggestion))
            {
                mValidationLabel.text = errorMessage;
                if (!string.IsNullOrEmpty(suggestion))
                {
                    mValidationLabel.text += $"\n    {suggestion}";
                }
                mValidationLabel.style.color = new StyleColor(Colors.StatusError);
                mValidationLabel.style.display = DisplayStyle.Flex;
                return false;
            }

            mValidationLabel.style.display = DisplayStyle.None;
            return true;
        }

        /// <summary>
        /// 创建 UI 面板
        /// </summary>
        private void OnCreateUIPanelClick()
        {
            var panelName = mPanelCreateName;

            if (string.IsNullOrEmpty(panelName)) return;

            // 验证面板名称
            if (!ValidatePanelName()) return;

            // 确保 Prefab 目录存在
            var prefabDir = Path.GetDirectoryName(PrefabPath);
            if (!string.IsNullOrEmpty(prefabDir) && !Directory.Exists(prefabDir))
            {
                Directory.CreateDirectory(prefabDir);
                AssetDatabase.Refresh();
            }

            var uiKitPrefab = Resources.Load<GameObject>(nameof(UIKit));
            var uikit = Object.Instantiate(uiKitPrefab);

            try
            {
                var uiRoot = uikit.GetComponentInChildren<UIRoot>();

                if (uiRoot == null)
                {
                    KitLogger.Error("UIKit预制体中不包含UIRoot组件!");
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

                // 创建 Panel 子对象
                var panelChild = new GameObject("Panel")
                {
                    transform =
                    {
                        parent = gameObj.transform,
                        localScale = Vector3.one
                    }
                };

                var panelRect = panelChild.AddComponent<RectTransform>();
                panelRect.anchoredPosition3D = Vector3.zero;
                panelRect.localEulerAngles = Vector3.zero;
                panelRect.localScale = Vector3.one;
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.sizeDelta = Vector2.zero;

                // 添加 Image 组件（UGUI Panel）
                var panelImage = panelChild.AddComponent<UnityEngine.UI.Image>();
                panelImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
                panelImage.type = UnityEngine.UI.Image.Type.Sliced;
                panelImage.color = Color.white;

                var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(gameObj, PrefabPath, InteractionMode.AutomatedAction);

                UICodeGenerator.DoCreateCode(prefab, ScriptNamespace);

                mPanelCreateName = string.Empty;
                mPanelNameField.value = string.Empty;
                UpdateLivePreview();

                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("成功", $"UI Panel 已创建:\n{PrefabPath}", "确定");
            }
            finally
            {
                if (uikit != null)
                {
                    Object.DestroyImmediate(uikit);
                }
            }
        }

        #endregion
    }
}
#endif
