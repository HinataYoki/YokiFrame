using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// UIKit 工具页面 - UI Toolkit 版本
    /// </summary>
    public class UIKitToolPage : YokiFrameToolPageBase
    {
        public override string PageName => "UIKit";
        public override string PageIcon => KitIcons.UIKIT;
        public override int Priority => 30;

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
        private TextField mAssemblyField;
        private TextField mNamespaceField;
        private TextField mScriptPathField;
        private TextField mPrefabPathField;
        private TextField mPanelNameField;
        private VisualElement mPreviewContainer;
        private UnityEngine.UIElements.Button mCreateButton;

        private string PrefabName => $"{mPanelCreateName}.prefab";
        private string ScriptName => $"{mPanelCreateName}.cs";
        private string DesignerName => $"{mPanelCreateName}.Designer.cs";
        private string PrefabPath => $"{PrefabGeneratePath}/{PrefabName}";
        private string ScriptPath => $"{ScriptGeneratePath}/{mPanelCreateName}/{ScriptName}";
        private string DesignerPath => $"{ScriptGeneratePath}/{mPanelCreateName}/{DesignerName}";

        protected override void BuildUI(VisualElement root)
        {
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.style.paddingLeft = 16;
            scrollView.style.paddingRight = 16;
            scrollView.style.paddingTop = 16;
            root.Add(scrollView);
            
            var title = new Label("UI Panel 创建工具");
            title.style.fontSize = 16;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 16;
            scrollView.Add(title);
            
            // 程序集
            scrollView.Add(CreateFormRow("UI脚本所在的程序集:", out mAssemblyField, AssemblyName));
            mAssemblyField.RegisterValueChangedCallback(evt => AssemblyName = evt.newValue);
            
            // 命名空间
            scrollView.Add(CreateFormRow("Scripts命名空间:", out mNamespaceField, ScriptNamespace));
            mNamespaceField.RegisterValueChangedCallback(evt => ScriptNamespace = evt.newValue);
            
            // Scripts 目录
            scrollView.Add(CreatePathRow("Scripts目录:", out mScriptPathField, ScriptGeneratePath, path =>
            {
                ScriptGeneratePath = path;
                mScriptPathField.value = path;
            }));
            
            // Prefab 目录
            scrollView.Add(CreatePathRow("Prefab目录:", out mPrefabPathField, PrefabGeneratePath, path =>
            {
                PrefabGeneratePath = path;
                mPrefabPathField.value = path;
            }));
            
            // Panel 名字
            var panelNameRow = new VisualElement();
            panelNameRow.AddToClassList("form-row");
            panelNameRow.style.marginTop = 16;
            
            var panelLabel = new Label("Panel名字:");
            panelLabel.AddToClassList("form-label");
            panelNameRow.Add(panelLabel);
            
            mPanelNameField = new TextField();
            mPanelNameField.AddToClassList("form-field");
            mPanelNameField.RegisterValueChangedCallback(evt =>
            {
                mPanelCreateName = evt.newValue;
                UpdatePreview();
            });
            panelNameRow.Add(mPanelNameField);
            
            scrollView.Add(panelNameRow);
            
            // 预览区域
            mPreviewContainer = new VisualElement();
            mPreviewContainer.style.marginTop = 16;
            mPreviewContainer.style.display = DisplayStyle.None;
            scrollView.Add(mPreviewContainer);
            
            // 创建按钮
            mCreateButton = new UnityEngine.UIElements.Button(OnCreateUIPanelClick) { text = "创建 UI Panel" };
            mCreateButton.AddToClassList("action-button");
            mCreateButton.AddToClassList("primary");
            mCreateButton.style.marginTop = 16;
            mCreateButton.style.height = 32;
            mCreateButton.style.display = DisplayStyle.None;
            scrollView.Add(mCreateButton);
        }

        private VisualElement CreateFormRow(string labelText, out TextField textField, string initialValue)
        {
            var row = new VisualElement();
            row.AddToClassList("form-row");
            
            var label = new Label(labelText);
            label.AddToClassList("form-label");
            row.Add(label);
            
            textField = new TextField();
            textField.AddToClassList("form-field");
            textField.value = initialValue;
            row.Add(textField);
            
            return row;
        }

        private VisualElement CreatePathRow(string labelText, out TextField textField, string initialValue, Action<string> onPathChanged)
        {
            var row = new VisualElement();
            row.AddToClassList("form-row");
            
            var label = new Label(labelText);
            label.AddToClassList("form-label");
            row.Add(label);
            
            var pathContainer = new VisualElement();
            pathContainer.style.flexDirection = FlexDirection.Row;
            pathContainer.style.flexGrow = 1;
            
            textField = new TextField();
            textField.style.flexGrow = 1;
            textField.value = initialValue;
            textField.SetEnabled(false);
            pathContainer.Add(textField);
            
            var browseBtn = new UnityEngine.UIElements.Button(() =>
            {
                var folderPath = EditorUtility.OpenFolderPanel(labelText, initialValue, string.Empty);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    var idx = folderPath.IndexOf(ASSETS, StringComparison.Ordinal);
                    var newPath = idx >= 0 ? folderPath[idx..] : folderPath;
                    onPathChanged?.Invoke(newPath);
                }
            }) { text = "..." };
            browseBtn.style.width = 30;
            browseBtn.style.marginLeft = 4;
            pathContainer.Add(browseBtn);
            
            row.Add(pathContainer);
            
            return row;
        }

        private void UpdatePreview()
        {
            mPreviewContainer.Clear();
            
            if (string.IsNullOrEmpty(mPanelCreateName))
            {
                mPreviewContainer.style.display = DisplayStyle.None;
                mCreateButton.style.display = DisplayStyle.None;
                return;
            }
            
            mPreviewContainer.style.display = DisplayStyle.Flex;
            
            var previewTitle = new Label("生成文件预览");
            previewTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            previewTitle.style.marginBottom = 8;
            mPreviewContainer.Add(previewTitle);
            
            var previewBox = new VisualElement();
            previewBox.AddToClassList("info-box");
            mPreviewContainer.Add(previewBox);
            
            AddPreviewItem(previewBox, PrefabPath, File.Exists(PrefabPath));
            AddPreviewItem(previewBox, ScriptPath, File.Exists(ScriptPath));
            AddPreviewItem(previewBox, DesignerPath, File.Exists(DesignerPath));
            
            // 只有当 Prefab 不存在时才显示创建按钮
            var canCreate = !File.Exists(PrefabPath);
            mCreateButton.style.display = canCreate ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void AddPreviewItem(VisualElement parent, string path, bool exists)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 4;
            
            var pathLabel = new Label(path);
            pathLabel.style.flexGrow = 1;
            pathLabel.style.fontSize = 11;
            pathLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
            row.Add(pathLabel);
            
            if (exists)
            {
                var existsLabel = new Label("[已存在]");
                existsLabel.style.color = new StyleColor(new Color(1f, 0.4f, 0.4f));
                existsLabel.style.fontSize = 11;
                row.Add(existsLabel);
            }
            
            parent.Add(row);
        }

        private void OnCreateUIPanelClick()
        {
            var panelName = mPanelCreateName;

            if (string.IsNullOrEmpty(panelName)) return;

            // 确保 Prefab 目录存在
            var prefabDir = Path.GetDirectoryName(PrefabPath);
            if (!string.IsNullOrEmpty(prefabDir) && !Directory.Exists(prefabDir))
            {
                Directory.CreateDirectory(prefabDir);
                AssetDatabase.Refresh();
            }

            var uiKitPrefab = Resources.Load<GameObject>(nameof(UIKit));
            var uikit = UnityEngine.Object.Instantiate(uiKitPrefab);
            
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

                var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(gameObj, PrefabPath, InteractionMode.AutomatedAction);

                UICodeGenerator.DoCreateCode(prefab, ScriptPath, DesignerPath, ScriptNamespace);

                mPanelCreateName = string.Empty;
                mPanelNameField.value = string.Empty;
                UpdatePreview();
                
                AssetDatabase.Refresh();
                
                EditorUtility.DisplayDialog("成功", $"UI Panel 已创建:\n{PrefabPath}", "确定");
            }
            finally
            {
                // 确保 UIKit 克隆物体被销毁（gameObj 是其子物体，会一起被销毁）
                if (uikit != null)
                {
                    UnityEngine.Object.DestroyImmediate(uikit);
                }
            }
        }
    }
}
