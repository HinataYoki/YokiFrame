#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YooAsset;
using YooAsset.Editor;

namespace YokiFrame.Unity
{
    /// <summary>
    /// YooInitConfig 的 UIToolkit Inspector 绘制器。
    /// </summary>
    [CustomPropertyDrawer(typeof(YooInitConfig))]
    public sealed partial class YooInitConfigDrawer : PropertyDrawer
    {
        private const string FOLDOUT_PREFS_PREFIX = "YokiFrame.YooInitConfig.Foldout.";

        private static readonly Color sRootBackground = YokiFrameUIComponents.Colors.LayerSection;
        private static readonly Color sCardBackground = YokiFrameUIComponents.Colors.LayerCard;
        private static readonly Color sCardHeaderBackground = YokiFrameUIComponents.Colors.LayerElevated;
        private static readonly Color sFieldBackground = YokiFrameUIComponents.Colors.LayerElevated;
        private static readonly Color sNestedBackground = YokiFrameUIComponents.Colors.LayerToolbar;
        private static readonly Color sBorderColor = YokiFrameUIComponents.Colors.BorderDefault;
        private static readonly Color sTextPrimary = YokiFrameUIComponents.Colors.TextPrimary;
        private static readonly Color sTextSecondary = YokiFrameUIComponents.Colors.TextSecondary;
        private static readonly Color sTextMuted = YokiFrameUIComponents.Colors.TextTertiary;
        private static readonly Color sBrandBlue = YokiFrameUIComponents.Colors.BrandPrimary;
        private static readonly Color sBrandGreen = YokiFrameUIComponents.Colors.BrandSuccess;
        private static readonly Color sWarning = YokiFrameUIComponents.Colors.BrandWarning;

        private static readonly List<string> sOffsetChoices = new()
        {
            "16", "32", "64", "128", "256", "512", "1024"
        };

        private static readonly int[] sOffsetValues =
        {
            16, 32, 64, 128, 256, 512, 1024
        };

        private static readonly List<string> sCompressChoices = new(Enum.GetNames(typeof(ECompressOption)));

        private static readonly List<string> sCopyOptionDisplayNames = new()
        {
            "不拷贝",
            "清空后拷贝全部",
            "清空后按标签拷贝",
            "直接拷贝全部",
            "直接按标签拷贝"
        };

        private static readonly List<string> sBuildPipelineChoices = CreateBuildPipelineChoices();

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            root.name = "yoo-init-config-card";
            root.AddToClassList("yoo-init-config-root");
            YokiStyleService.Apply(root, YokiStyleProfile.CoreOnly);
            YokiStyleService.ApplyKitStyleToElement(root, "ResKit");
            root.style.backgroundColor = sRootBackground;
            root.style.borderTopLeftRadius = YokiFrameUIComponents.Radius.LG;
            root.style.borderTopRightRadius = YokiFrameUIComponents.Radius.LG;
            root.style.borderBottomLeftRadius = YokiFrameUIComponents.Radius.LG;
            root.style.borderBottomRightRadius = YokiFrameUIComponents.Radius.LG;
            root.style.marginTop = YokiFrameUIComponents.Spacing.SM;
            root.style.marginBottom = YokiFrameUIComponents.Spacing.SM;
            root.style.paddingLeft = YokiFrameUIComponents.Spacing.SM;
            root.style.paddingRight = YokiFrameUIComponents.Spacing.SM;
            root.style.paddingTop = YokiFrameUIComponents.Spacing.SM;
            root.style.paddingBottom = YokiFrameUIComponents.Spacing.SM;

            var title = new Label(property.displayName);
            title.AddToClassList("yoki-kit-panel__title");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = sTextPrimary;
            title.style.marginLeft = 2f;
            title.style.marginBottom = YokiFrameUIComponents.Spacing.SM;
            root.Add(title);

            AddBasicCard(root, property);
            AddEncryptionCard(root, property);
            AddBuildCard(root, property);

            return root;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            property.isExpanded = EditorGUI.Foldout(
                new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
                property.isExpanded,
                label,
                true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                var y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                DrawProperty(position, property, nameof(YooInitConfig.EditorPlayMode), ref y);
                DrawProperty(position, property, nameof(YooInitConfig.RuntimePlayMode), ref y);
                DrawProperty(position, property, nameof(YooInitConfig.PackageNames), ref y, true);
                DrawProperty(position, property, nameof(YooInitConfig.AutoLoadManifest), ref y);
                DrawProperty(position, property, nameof(YooInitConfig.ManifestTimeoutSeconds), ref y);
                DrawProperty(position, property, nameof(YooInitConfig.DefaultHostServer), ref y);
                DrawProperty(position, property, nameof(YooInitConfig.FallbackHostServer), ref y);
                DrawProperty(position, property, nameof(YooInitConfig.EncryptionType), ref y);
                DrawProperty(position, property, nameof(YooInitConfig.XorKeySeed), ref y);
                DrawProperty(position, property, nameof(YooInitConfig.FileOffset), ref y);
                DrawProperty(position, property, nameof(YooInitConfig.AesPassword), ref y);
                DrawProperty(position, property, nameof(YooInitConfig.AesSalt), ref y);

                var buttonRect = new Rect(position.x + EditorGUI.indentLevel * 15f, y, position.width - EditorGUI.indentLevel * 15f, EditorGUIUtility.singleLineHeight);
                if (GUI.Button(buttonRect, "打开 YooAsset 资源收集器"))
                    YooAssetEditorMenuBridge.OpenCollector();

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            var height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            height += GetChildHeight(property, nameof(YooInitConfig.EditorPlayMode));
            height += GetChildHeight(property, nameof(YooInitConfig.RuntimePlayMode));
            height += GetChildHeight(property, nameof(YooInitConfig.PackageNames), true);
            height += GetChildHeight(property, nameof(YooInitConfig.AutoLoadManifest));
            height += GetChildHeight(property, nameof(YooInitConfig.ManifestTimeoutSeconds));
            height += GetChildHeight(property, nameof(YooInitConfig.DefaultHostServer));
            height += GetChildHeight(property, nameof(YooInitConfig.FallbackHostServer));
            height += GetChildHeight(property, nameof(YooInitConfig.EncryptionType));
            height += GetChildHeight(property, nameof(YooInitConfig.XorKeySeed));
            height += GetChildHeight(property, nameof(YooInitConfig.FileOffset));
            height += GetChildHeight(property, nameof(YooInitConfig.AesPassword));
            height += GetChildHeight(property, nameof(YooInitConfig.AesSalt));
            height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            return height;
        }

        private static void AddBasicCard(VisualElement root, SerializedProperty property)
        {
            var body = CreateCard(root, "基础配置", "yoo-init-basic-card", true);
            body.Add(CreateEnumDropdownRow(property.FindPropertyRelative(nameof(YooInitConfig.EditorPlayMode)), "编辑器下初始化模式", true, null));
            body.Add(CreateRuntimePlayModeRow(property));
            body.Add(CreatePackageListField(property));
            body.Add(CreateBoolRow(property.FindPropertyRelative(nameof(YooInitConfig.AutoLoadManifest)), "自动加载清单"));
            body.Add(CreateIntRow(property.FindPropertyRelative(nameof(YooInitConfig.ManifestTimeoutSeconds)), "清单超时秒数"));
            body.Add(CreateStringRow(property.FindPropertyRelative(nameof(YooInitConfig.DefaultHostServer)), "主资源服务器"));
            body.Add(CreateStringRow(property.FindPropertyRelative(nameof(YooInitConfig.FallbackHostServer)), "备用资源服务器"));
        }

        private static void AddEncryptionCard(VisualElement root, SerializedProperty property)
        {
            var body = CreateCard(root, "加密配置", "yoo-init-encryption-card", true);
            var encryptionProperty = property.FindPropertyRelative(nameof(YooInitConfig.EncryptionType));
            var dynamicContainer = new VisualElement();

            body.Add(CreateEnumDropdownRow(encryptionProperty, "加密类型", false, () =>
            {
                RefreshEncryptionContent(dynamicContainer, property);
            }));

            dynamicContainer.style.marginTop = 8f;
            body.Add(dynamicContainer);
            RefreshEncryptionContent(dynamicContainer, property);
        }

        private static void AddBuildCard(VisualElement root, SerializedProperty property)
        {
            var body = CreateCard(root, "打包配置", "yoo-init-build-card", false);
            var dynamicContainer = new VisualElement();
            body.Add(dynamicContainer);
            RefreshBuildContent(dynamicContainer, property);
        }

        private static VisualElement CreateRow(string labelText)
        {
            var row = new VisualElement();
            row.AddToClassList("yoki-field-row");
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 6f;
            row.style.marginBottom = 6f;

            var label = new Label(labelText);
            label.AddToClassList("yoki-field-row__label");
            label.style.width = 132f;
            label.style.minWidth = 132f;
            label.style.color = sTextSecondary;
            label.style.fontSize = 11f;
            row.Add(label);
            return row;
        }

        private static VisualElement CreateRuntimePlayModeRow(SerializedProperty property)
        {
            var child = property.FindPropertyRelative(nameof(YooInitConfig.RuntimePlayMode));
            var choices = new List<string>();
            var values = new List<int>();
            AddEnumChoice(child, choices, values, nameof(EPlayMode.OfflinePlayMode));
            AddEnumChoice(child, choices, values, nameof(EPlayMode.HostPlayMode));
            AddEnumChoice(child, choices, values, nameof(EPlayMode.WebPlayMode));
            AddEnumChoice(child, choices, values, nameof(EPlayMode.CustomPlayMode));
            return CreateDropdownRow("真机初始化模式", choices, values, child, true, null, null);
        }

        private static VisualElement CreateEnumDropdownRow(SerializedProperty property, string label, bool useDisplayNames, Action onChanged)
        {
            var choices = new List<string>();
            var values = new List<int>();
            var names = useDisplayNames ? property.enumDisplayNames : property.enumNames;
            for (var i = 0; i < names.Length; i++)
            {
                choices.Add(names[i]);
                values.Add(i);
            }

            return CreateDropdownRow(label, choices, values, property, false, null, onChanged);
        }

        private static VisualElement CreateDropdownRow(string label, List<string> choices, List<int> values, SerializedProperty property, bool applyDefaultWhenInvalid, string elementName, Action onChanged)
        {
            var row = CreateRow(label);
            var index = values.IndexOf(property.enumValueIndex);
            if (index < 0)
            {
                index = 0;
                if (applyDefaultWhenInvalid && values.Count > 0)
                {
                    property.enumValueIndex = values[index];
                    property.serializedObject.ApplyModifiedProperties();
                }
            }

            var dropdown = new DropdownField(choices, index);
            if (index >= 0 && index < choices.Count)
                dropdown.value = choices[index];
            dropdown.name = elementName;
            dropdown.AddToClassList("yoki-field-row__field");
            dropdown.style.flexGrow = 1f;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                var newIndex = choices.IndexOf(evt.newValue);
                if (newIndex < 0 || newIndex >= values.Count)
                    return;

                property.enumValueIndex = values[newIndex];
                property.serializedObject.ApplyModifiedProperties();
                if (onChanged != null)
                    onChanged();
            });
            row.Add(dropdown);
            return row;
        }

        private static VisualElement CreateStringRow(SerializedProperty property, string label)
        {
            var row = CreateRow(label);
            var field = new TextField { value = property.stringValue };
            field.AddToClassList("yoki-field-row__field");
            field.style.flexGrow = 1f;
            field.RegisterValueChangedCallback(evt =>
            {
                property.stringValue = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
            });
            row.Add(field);
            return row;
        }

        private static VisualElement CreateIntRow(SerializedProperty property, string label)
        {
            var row = CreateRow(label);
            var field = new IntegerField { value = property.intValue };
            field.AddToClassList("yoki-field-row__field");
            field.style.flexGrow = 1f;
            field.RegisterValueChangedCallback(evt =>
            {
                property.intValue = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
            });
            row.Add(field);
            return row;
        }

        private static VisualElement CreateBoolRow(SerializedProperty property, string label)
        {
            var toggle = YokiFrameUIComponents.CreateModernToggle(label, property.boolValue, value =>
            {
                property.boolValue = value;
                property.serializedObject.ApplyModifiedProperties();
            });
            toggle.style.marginTop = 6f;
            toggle.style.marginBottom = 6f;
            return toggle;
        }

        private static VisualElement CreatePackageListField(SerializedProperty parent)
        {
            var container = new VisualElement();
            container.AddToClassList("yoki-config-section");
            container.style.marginTop = 8f;
            container.style.marginBottom = 10f;

            var label = new Label("资源包列表");
            label.AddToClassList("yoki-config-section__title");
            label.style.color = sTextSecondary;
            label.style.fontSize = 11f;
            label.style.marginBottom = 6f;
            container.Add(label);

            var listContainer = new VisualElement();
            listContainer.AddToClassList("yoki-section");
            listContainer.style.backgroundColor = sNestedBackground;
            listContainer.style.borderTopLeftRadius = 5f;
            listContainer.style.borderTopRightRadius = 5f;
            listContainer.style.borderBottomLeftRadius = 5f;
            listContainer.style.borderBottomRightRadius = 5f;
            listContainer.style.paddingLeft = 10f;
            listContainer.style.paddingRight = 10f;
            listContainer.style.paddingTop = 9f;
            listContainer.style.paddingBottom = 9f;
            container.Add(listContainer);

            var list = parent.FindPropertyRelative(nameof(YooInitConfig.PackageNames));
            Action refresh = null;
            refresh = () =>
            {
                parent.serializedObject.Update();
                listContainer.Clear();

                for (var i = 0; i < list.arraySize; i++)
                {
                    var item = list.GetArrayElementAtIndex(i);
                    listContainer.Add(CreatePackageRow(parent, list, item, i, refresh));
                }

                var addButton = CreateActionButton("+ 添加包", sTextSecondary, () =>
                {
                    list.InsertArrayElementAtIndex(list.arraySize);
                    list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = "Package" + list.arraySize;
                    parent.serializedObject.ApplyModifiedProperties();
                    refresh();
                });
                addButton.style.marginTop = 8f;
                listContainer.Add(addButton);
            };

            refresh();
            return container;
        }

        private static VisualElement CreatePackageRow(SerializedProperty parent, SerializedProperty list, SerializedProperty item, int index, Action refresh)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 5f;

            var marker = new Label(index == 0 ? "默认" : "#" + (index + 1));
            marker.style.width = 42f;
            marker.style.color = index == 0 ? sBrandBlue : sTextMuted;
            marker.style.unityFontStyleAndWeight = index == 0 ? FontStyle.Bold : FontStyle.Normal;
            marker.style.fontSize = 10f;
            row.Add(marker);

            var field = new TextField { value = item.stringValue };
            field.style.flexGrow = 1f;
            field.RegisterValueChangedCallback(evt =>
            {
                item.stringValue = evt.newValue;
                parent.serializedObject.ApplyModifiedProperties();
            });
            row.Add(field);

            if (list.arraySize > 1)
            {
                var remove = CreateActionButton("-", sWarning, () =>
                {
                    list.DeleteArrayElementAtIndex(index);
                    parent.serializedObject.ApplyModifiedProperties();
                    refresh();
                });
                remove.style.width = 24f;
                remove.style.marginLeft = 6f;
                row.Add(remove);
            }

            return row;
        }

        private static void RefreshEncryptionContent(VisualElement container, SerializedProperty property)
        {
            container.Clear();
            var encryption = (YooEncryptionType)property.FindPropertyRelative(nameof(YooInitConfig.EncryptionType)).enumValueIndex;
            switch (encryption)
            {
                case YooEncryptionType.XorStream:
                    container.Add(CreateInfoPanel("XOR 流式加密", "性能好，安全性适中。密钥种子会通过 SHA256 生成 32 字节密钥。", sBrandBlue));
                    container.Add(CreateStringRow(property.FindPropertyRelative(nameof(YooInitConfig.XorKeySeed)), "密钥种子"));
                    container.Add(CreateResetButton("重置为默认密钥", () =>
                    {
                        property.FindPropertyRelative(nameof(YooInitConfig.XorKeySeed)).stringValue = YooInitConfig.DEFAULT_XOR_KEY_SEED;
                        property.serializedObject.ApplyModifiedProperties();
                    }));
                    break;
                case YooEncryptionType.FileOffset:
                    container.Add(CreateInfoPanel("文件偏移", "仅防止直接打开，无实际加密。适合简单防护场景。", sWarning));
                    container.Add(CreateOffsetDropdown(property));
                    container.Add(CreateResetButton("重置为默认偏移量", () =>
                    {
                        property.FindPropertyRelative(nameof(YooInitConfig.FileOffset)).intValue = YooInitConfig.DEFAULT_FILE_OFFSET;
                        property.serializedObject.ApplyModifiedProperties();
                    }));
                    break;
                case YooEncryptionType.Aes:
                    container.Add(CreateInfoPanel("AES 加密", "安全性高，但性能开销大。密码和盐值通过 PBKDF2 派生密钥。", sBrandGreen));
                    container.Add(CreateStringRow(property.FindPropertyRelative(nameof(YooInitConfig.AesPassword)), "密码"));
                    container.Add(CreateStringRow(property.FindPropertyRelative(nameof(YooInitConfig.AesSalt)), "盐值"));
                    container.Add(CreateResetButton("重置为默认密钥", () =>
                    {
                        property.FindPropertyRelative(nameof(YooInitConfig.AesPassword)).stringValue = YooInitConfig.DEFAULT_AES_PASSWORD;
                        property.FindPropertyRelative(nameof(YooInitConfig.AesSalt)).stringValue = YooInitConfig.DEFAULT_AES_SALT;
                        property.serializedObject.ApplyModifiedProperties();
                    }));
                    break;
                case YooEncryptionType.Custom:
                    container.Add(CreateInfoPanel("自定义加密", "需要在项目代码中接入 YooAsset 的加密/解密服务。", sBrandBlue));
                    break;
                default:
                    container.Add(CreateInfoPanel(null, "资源将以明文形式存储。", sTextSecondary));
                    break;
            }
        }

        private static VisualElement CreateOffsetDropdown(SerializedProperty parent)
        {
            var row = CreateRow("偏移量");
            var offset = parent.FindPropertyRelative(nameof(YooInitConfig.FileOffset));
            var currentIndex = Array.IndexOf(sOffsetValues, offset.intValue);
            if (currentIndex < 0)
                currentIndex = 1;

            var dropdown = new DropdownField(sOffsetChoices, currentIndex);
            dropdown.value = sOffsetChoices[currentIndex];
            dropdown.AddToClassList("yoki-field-row__field");
            dropdown.style.flexGrow = 1f;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                var index = sOffsetChoices.IndexOf(evt.newValue);
                if (index < 0)
                    return;

                offset.intValue = sOffsetValues[index];
                parent.serializedObject.ApplyModifiedProperties();
            });
            row.Add(dropdown);
            return row;
        }

        private static void DrawProperty(Rect position, SerializedProperty root, string propertyName, ref float y, bool includeChildren = false)
        {
            var child = root.FindPropertyRelative(propertyName);
            if (child == null)
                return;

            var height = EditorGUI.GetPropertyHeight(child, includeChildren);
            var rect = new Rect(position.x, y, position.width, height);
            EditorGUI.PropertyField(rect, child, includeChildren);
            y += height + EditorGUIUtility.standardVerticalSpacing;
        }

        private static float GetChildHeight(SerializedProperty root, string propertyName, bool includeChildren = false)
        {
            var child = root.FindPropertyRelative(propertyName);
            if (child == null)
                return 0f;

            return EditorGUI.GetPropertyHeight(child, includeChildren) + EditorGUIUtility.standardVerticalSpacing;
        }
    }
}
#endif
#endif
