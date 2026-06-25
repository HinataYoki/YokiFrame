#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.Unity;

namespace YokiFrame.Unity
{
    /// <summary>
    /// YooInitBehaviour 的 Inspector 操作入口。
    /// </summary>
    [CustomEditor(typeof(YooInitBehaviour))]
    public sealed class YooInitBehaviourEditor : UnityEditor.Editor
    {
        private static readonly Color sPanelBackground = new(0.13f, 0.15f, 0.18f, 1f);
        private static readonly Color sBorderColor = new(0.24f, 0.26f, 0.30f, 1f);
        private static readonly Color sTextSecondary = new(0.64f, 0.66f, 0.72f, 1f);
        private static readonly Color sBrandBlue = new(0.19f, 0.60f, 1f, 1f);
        private static readonly Color sBrandGreen = new(0.25f, 0.78f, 0.45f, 1f);

        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new();
            root.name = "yoo-init-behaviour-inspector";
            root.style.paddingTop = 4f;

            var initOnStart = serializedObject.FindProperty("mInitOnStart");
            var config = serializedObject.FindProperty("mConfig");

            root.Add(CreatePropertyField(initOnStart, "Start 时初始化"));
            root.Add(new YooInitConfigDrawer().CreatePropertyGUI(config));
            root.Add(CreateHelpPanel());
            root.Add(CreateButtons());

            return root;
        }

        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox("运行时调用 InitAsync 后，ResKit 会自动使用 YooAssetResourceProvider。也可以只安装 Provider，让项目自己的 YooAsset 初始化流程继续负责包初始化。", MessageType.Info);

            if (GUILayout.Button("仅安装 ResKit YooAsset Provider"))
                YooInit.InstallProvider();

            if (GUILayout.Button("打开 YooAsset 资源收集器"))
                YooAssetEditorMenuBridge.OpenCollector();
        }

        private static PropertyField CreatePropertyField(SerializedProperty property, string label)
        {
            var field = new PropertyField(property, label);
            field.style.marginBottom = 6f;
            field.BindProperty(property);
            return field;
        }

        private static VisualElement CreateHelpPanel()
        {
            VisualElement panel = new();
            panel.style.backgroundColor = sPanelBackground;
            panel.style.borderTopLeftRadius = 6f;
            panel.style.borderTopRightRadius = 6f;
            panel.style.borderBottomLeftRadius = 6f;
            panel.style.borderBottomRightRadius = 6f;
            panel.style.borderLeftWidth = 2f;
            panel.style.borderLeftColor = sBrandBlue;
            panel.style.borderRightWidth = 1f;
            panel.style.borderTopWidth = 1f;
            panel.style.borderBottomWidth = 1f;
            panel.style.borderRightColor = sBorderColor;
            panel.style.borderTopColor = sBorderColor;
            panel.style.borderBottomColor = sBorderColor;
            panel.style.paddingLeft = 10f;
            panel.style.paddingRight = 10f;
            panel.style.paddingTop = 8f;
            panel.style.paddingBottom = 8f;
            panel.style.marginTop = 4f;
            panel.style.marginBottom = 8f;

            Label label = new("运行时调用 InitAsync 后，ResKit 会自动使用 YooAssetResourceProvider。也可以只安装 Provider，让项目自己的 YooAsset 初始化流程继续负责包初始化。");
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.color = sTextSecondary;
            label.style.fontSize = 11f;
            panel.Add(label);
            return panel;
        }

        private static VisualElement CreateButtons()
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;

            var installButton = CreateButton("仅安装 Provider", sBrandBlue, YooInit.InstallProvider);
            installButton.style.flexGrow = 1f;
            installButton.style.marginRight = 6f;
            row.Add(installButton);

            var collectorButton = CreateButton("打开收集器", sBrandGreen, () =>
            {
                if (!YooAssetEditorMenuBridge.OpenCollector())
                    EditorUtility.DisplayDialog("YooAsset", "未找到 YooAsset 资源收集器菜单，请确认 YooAsset 包已正确导入。", "OK");
            });
            collectorButton.style.flexGrow = 1f;
            row.Add(collectorButton);

            return row;
        }

        private static Button CreateButton(string text, Color textColor, System.Action action)
        {
            Button button = new(action) { text = text };
            button.style.height = 26f;
            button.style.backgroundColor = new Color(0.20f, 0.21f, 0.25f, 1f);
            button.style.borderTopLeftRadius = 4f;
            button.style.borderTopRightRadius = 4f;
            button.style.borderBottomLeftRadius = 4f;
            button.style.borderBottomRightRadius = 4f;
            button.style.borderLeftWidth = 1f;
            button.style.borderRightWidth = 1f;
            button.style.borderTopWidth = 1f;
            button.style.borderBottomWidth = 1f;
            button.style.borderLeftColor = sBorderColor;
            button.style.borderRightColor = sBorderColor;
            button.style.borderTopColor = sBorderColor;
            button.style.borderBottomColor = sBorderColor;
            button.style.color = textColor;
            return button;
        }
    }
}
#endif
#endif
