#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// UIPanel 自定义 Inspector，提供面板设置、绑定树和生成代码入口。
    /// </summary>
    [CustomEditor(typeof(UIPanel), true)]
    [CanEditMultipleObjects]
    public partial class UIPanelInspector : Editor
    {
        private const string PANEL_CONFIG_FOLDOUT_KEY = "YokiFrame.UIKit.UIPanelInspector.PanelConfig";
        private const string ANIMATION_SECTION_FOLDOUT_KEY = "YokiFrame.UIKit.UIPanelInspector.AnimationSection";
        private const string FOCUS_SECTION_FOLDOUT_KEY = "YokiFrame.UIKit.UIPanelInspector.FocusSection";
        private const string CUSTOM_PROPERTIES_FOLDOUT_KEY = "YokiFrame.UIKit.UIPanelInspector.CustomProperties";
        private const string STYLE_SHEET_PATH = "Tools/UIKit/Editor/Inspectors/UIPanelInspectorStyles.uss";

        private static readonly Type[] sAnimationConfigTypes =
        {
            null,
            typeof(FadeAnimationConfig),
            typeof(ScaleAnimationConfig),
            typeof(SlideAnimationConfig)
        };

        private static readonly string[] sAnimationConfigLabels =
        {
            "无",
            "淡入淡出",
            "缩放",
            "滑动"
        };

        private SerializedProperty mShowAnimationConfigProp;
        private SerializedProperty mHideAnimationConfigProp;
        private SerializedProperty mAutoFocusOnShowProp;
        private SerializedProperty mDefaultSelectableProp;
        private readonly List<string> mCustomPropertyPaths = new List<string>(8);

        private void OnEnable()
        {
            SerializedObject currentSerializedObject;
            if (!TryGetSerializedObject(out currentSerializedObject))
                return;

            mShowAnimationConfigProp = currentSerializedObject.FindProperty("mShowAnimationConfig");
            mHideAnimationConfigProp = currentSerializedObject.FindProperty("mHideAnimationConfig");
            mAutoFocusOnShowProp = currentSerializedObject.FindProperty("mAutoFocusOnShow");
            mDefaultSelectableProp = currentSerializedObject.FindProperty("mDefaultSelectable");
            LoadCollapsedBindPaths();
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.AddToClassList("uipanel-inspector");

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UIKitEditorAssetPaths.ResolveStyleSheetPath(STYLE_SHEET_PATH));
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);

            if (!HasValidTargets(targets))
            {
                root.Add(CreateHelpBox("Inspector 目标已失效，等待 Unity 刷新选择后会自动恢复。"));
                return root;
            }

            CreatePanelSettings(root);
            CreateBindTree(root);
            CreateCustomProperties(root);
            return root;
        }

        private void CreatePanelSettings(VisualElement root)
        {
            var section = CreateSectionContainer("uipanel-section-panelconfig");

            var foldout = CreateRememberedFoldout(
                "面板设置",
                PANEL_CONFIG_FOLDOUT_KEY,
                true,
                "uipanel-panelconfig-foldout");
            section.Add(foldout);

            var content = new VisualElement();
            content.AddToClassList("uipanel-section-content");
            foldout.Add(content);

            var animationSection = CreateSubSection("动画设置", "uipanel-subsection-animation", ANIMATION_SECTION_FOLDOUT_KEY);
            var animationContent = animationSection.Q<VisualElement>(className: "uipanel-subsection-content");
            animationContent.Add(CreateHelpBox("配置面板显示和隐藏时播放的动画效果。"));
            animationContent.Add(CreateAnimationField("显示动画", mShowAnimationConfigProp));
            animationContent.Add(CreateAnimationField("隐藏动画", mHideAnimationConfigProp));
            content.Add(animationSection);

            var focusSection = CreateSubSection("焦点设置", "uipanel-subsection-focus", FOCUS_SECTION_FOLDOUT_KEY);
            var focusContent = focusSection.Q<VisualElement>(className: "uipanel-subsection-content");
            focusContent.Add(CreateHelpBox("定义面板打开时默认获得焦点的可选控件。"));
            if (mAutoFocusOnShowProp != null)
                focusContent.Add(CreatePropertyRow("自动聚焦", mAutoFocusOnShowProp));
            if (mDefaultSelectableProp != null)
                focusContent.Add(CreatePropertyRow("默认选中对象", mDefaultSelectableProp));
            content.Add(focusSection);

            root.Add(section);
        }

        private VisualElement CreateAnimationField(string label, SerializedProperty property)
        {
            var container = new VisualElement();
            container.AddToClassList("uipanel-animation-config");
            if (property == null)
                return container;

            var row = CreateFieldRow(label);
            var choices = new List<string>(sAnimationConfigLabels);
            var dropdown = new DropdownField(choices, GetAnimationIndex(property));
            dropdown.AddToClassList("uipanel-animation-dropdown");
            dropdown.RegisterValueChangedCallback(evt =>
            {
                var index = choices.IndexOf(evt.newValue);
                SetAnimationConfig(property, index);
                RebuildInspector();
            });
            row.Add(dropdown);
            container.Add(row);

            if (property.managedReferenceValue != null)
            {
                var field = new PropertyField(property, "参数");
                field.AddToClassList("uipanel-animation-params");
                container.Add(field);
            }

            return container;
        }

        private int GetAnimationIndex(SerializedProperty property)
        {
            if (property == null || property.managedReferenceValue == null)
                return 0;

            var type = property.managedReferenceValue.GetType();
            for (var i = 1; i < sAnimationConfigTypes.Length; i++)
            {
                if (sAnimationConfigTypes[i] == type)
                    return i;
            }

            return 0;
        }

        private void SetAnimationConfig(SerializedProperty property, int index)
        {
            serializedObject.Update();
            property.managedReferenceValue = index > 0 ? Activator.CreateInstance(sAnimationConfigTypes[index]) : null;
            serializedObject.ApplyModifiedProperties();

            var panel = target as UIPanel;
            if (panel != null && property.managedReferenceValue is FadeAnimationConfig && panel.GetComponent<CanvasGroup>() == null)
                Undo.AddComponent<CanvasGroup>(panel.gameObject);
        }

        private void GenerateUICode()
        {
            var prefab = ResolvePrefabAsset();
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("无法生成 UI 代码", "请在 Prefab 资源或 Prefab 实例上使用生成入口。", "确定");
                return;
            }

            try
            {
                UIKitPanelPrefabCreator.GenerateCodeForPrefab(prefab, new UIKitPanelCreateRequest
                {
                    PanelName = prefab.name,
                    ScriptFolder = UIKitPanelPrefabCreator.DEFAULT_SCRIPT_FOLDER,
                    ScriptNamespace = UIKitPanelPrefabCreator.DEFAULT_SCRIPT_NAMESPACE,
                    AssemblyName = UIKitPanelPrefabCreator.DEFAULT_ASSEMBLY_NAME,
                    CodeTemplate = UIKitPanelPrefabCreator.DEFAULT_CODE_TEMPLATE
                });
                RefreshBindTree();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("生成失败", ex.Message, "确定");
                LogKit.Exception(ex);
            }
        }

        private GameObject ResolvePrefabAsset()
        {
            var panel = target as UIPanel;
            if (panel == null)
                return null;

            var assetPath = AssetDatabase.GetAssetPath(panel.gameObject);
            if (!string.IsNullOrEmpty(assetPath))
                return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(panel.gameObject);
            return string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        }

        private void OpenPanelScript()
        {
            var panel = target as UIPanel;
            if (panel == null)
                return;

            var path = UIKitPanelPrefabCreator.DEFAULT_SCRIPT_FOLDER + "/" + panel.gameObject.name + "/" + panel.gameObject.name + ".cs";
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset != null)
                AssetDatabase.OpenAsset(asset);
            else
                EditorUtility.DisplayDialog("脚本不存在", "尚未找到脚本文件：\n" + path, "确定");
        }

        private void CreateCustomProperties(VisualElement root)
        {
            SerializedObject currentSerializedObject;
            if (!TryGetSerializedObject(out currentSerializedObject))
                return;

            var targetType = target != null ? target.GetType() : null;
            CollectCustomPropertyPaths(currentSerializedObject, targetType, mCustomPropertyPaths);

            if (mCustomPropertyPaths.Count == 0)
                return;

            var section = CreateSectionContainer("uipanel-section-custom");
            var foldout = CreateRememberedFoldout(
                "其他属性",
                CUSTOM_PROPERTIES_FOLDOUT_KEY,
                false,
                "uipanel-custom-foldout");
            section.Add(foldout);

            var content = new VisualElement();
            content.AddToClassList("uipanel-section-content");
            foldout.Add(content);

            AddCustomPropertyFields(content);
            root.Add(section);
        }

        private void AddCustomPropertyFields(VisualElement content)
        {
            var imguiContainer = new IMGUIContainer(DrawCustomPropertiesIMGUI);
            imguiContainer.AddToClassList("uipanel-custom-imgui");
            content.Add(imguiContainer);
        }

        private void DrawCustomPropertiesIMGUI()
        {
            SerializedObject currentSerializedObject;
            if (!TryGetSerializedObject(out currentSerializedObject))
                return;

            currentSerializedObject.UpdateIfRequiredOrScript();

            for (var i = 0; i < mCustomPropertyPaths.Count; i++)
            {
                var property = currentSerializedObject.FindProperty(mCustomPropertyPaths[i]);
                if (property == null)
                    continue;

                var field = target != null
                    ? FindFieldInHierarchy(target.GetType(), GetRootPropertyName(property.propertyPath))
                    : null;
                DrawCustomPropertyDecorators(field);
                if (ShouldForceExpandCustomProperty(field))
                    property.isExpanded = true;

                var label = CreateCustomPropertyLabel(property);
                EditorGUI.BeginDisabledGroup(HasReadOnlyAttribute(field));
                try
                {
                    if (label != null)
                        EditorGUILayout.PropertyField(property, label, true);
                    else
                        EditorGUILayout.PropertyField(property, true);
                }
                finally
                {
                    EditorGUI.EndDisabledGroup();
                }
            }

            currentSerializedObject.ApplyModifiedProperties();
        }

        private static FieldInfo FindFieldInHierarchy(Type type, string fieldName)
        {
            while (type != null)
            {
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field;

                type = type.BaseType;
            }

            return null;
        }

        private static string GetRootPropertyName(string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath))
                return string.Empty;

            var dotIndex = propertyPath.IndexOf('.');
            return dotIndex >= 0 ? propertyPath.Substring(0, dotIndex) : propertyPath;
        }

        private GUIContent CreateCustomPropertyLabel(SerializedProperty property)
        {
            if (property == null || target == null)
                return null;

            var field = FindFieldInHierarchy(target.GetType(), GetRootPropertyName(property.propertyPath));
            if (field == null)
                return null;

            var labelText = ResolveAttributeLabel(field);
            var tooltip = ResolveAttributeTooltip(field);
            if (string.IsNullOrEmpty(labelText) && string.IsNullOrEmpty(tooltip))
                return null;

            return new GUIContent(string.IsNullOrEmpty(labelText) ? property.displayName : labelText, tooltip);
        }

        private static void DrawCustomPropertyDecorators(MemberInfo memberInfo)
        {
            if (memberInfo == null)
                return;

            var title = ResolveAttributeTitle(memberInfo);
            if (!string.IsNullOrEmpty(title))
                DrawTitleDecorator(title, ShouldDrawTitleHorizontalLine(memberInfo));

            var infoBoxText = ResolveAttributeInfoBoxText(memberInfo);
            if (!string.IsNullOrEmpty(infoBoxText))
                EditorGUILayout.HelpBox(infoBoxText, ResolveAttributeInfoBoxType(memberInfo));
        }

        private static void DrawTitleDecorator(string title, bool horizontalLine)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (!horizontalLine)
                return;

            var lineRect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(lineRect, new Color(0.45f, 0.45f, 0.45f, 1f));
        }

        private static string ResolveAttributeTitle(MemberInfo memberInfo)
        {
            var attributes = memberInfo.GetCustomAttributes(true);
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (attribute == null || !LooksLikeTitleAttribute(attribute.GetType()))
                    continue;

                var title = TryReadStringMember(attribute, "Title") ??
                            TryReadStringMember(attribute, "title") ??
                            TryReadStringMember(attribute, "Text") ??
                            TryReadStringMember(attribute, "text") ??
                            TryReadStringMember(attribute, "Name") ??
                            TryReadStringMember(attribute, "name");
                if (!string.IsNullOrEmpty(title))
                    return title;
            }

            return null;
        }

        private static bool ShouldDrawTitleHorizontalLine(MemberInfo memberInfo)
        {
            var attributes = memberInfo.GetCustomAttributes(true);
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (attribute == null || !LooksLikeTitleAttribute(attribute.GetType()))
                    continue;

                var horizontalLine = TryReadBoolMember(attribute, "HorizontalLine") ??
                                     TryReadBoolMember(attribute, "horizontalLine");
                return !horizontalLine.HasValue || horizontalLine.Value;
            }

            return false;
        }

        private static string ResolveAttributeInfoBoxText(MemberInfo memberInfo)
        {
            var attributes = memberInfo.GetCustomAttributes(true);
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (attribute == null || !LooksLikeInfoBoxAttribute(attribute.GetType()))
                    continue;

                var text = TryReadStringMember(attribute, "Text") ??
                           TryReadStringMember(attribute, "text") ??
                           TryReadStringMember(attribute, "Message") ??
                           TryReadStringMember(attribute, "message");
                if (!string.IsNullOrEmpty(text))
                    return text;
            }

            return null;
        }

        private static MessageType ResolveAttributeInfoBoxType(MemberInfo memberInfo)
        {
            var attributes = memberInfo.GetCustomAttributes(true);
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (attribute == null || !LooksLikeInfoBoxAttribute(attribute.GetType()))
                    continue;

                var type = TryReadObjectMember(attribute, "MessageType") ??
                           TryReadObjectMember(attribute, "messageType") ??
                           TryReadObjectMember(attribute, "Type") ??
                           TryReadObjectMember(attribute, "type");
                return ToUnityMessageType(type);
            }

            return MessageType.Info;
        }

        private static MessageType ToUnityMessageType(object value)
        {
            if (value == null)
                return MessageType.Info;

            var name = value.ToString();
            if (string.Equals(name, "Warning", StringComparison.OrdinalIgnoreCase))
                return MessageType.Warning;
            if (string.Equals(name, "Error", StringComparison.OrdinalIgnoreCase))
                return MessageType.Error;
            if (string.Equals(name, "None", StringComparison.OrdinalIgnoreCase))
                return MessageType.None;
            return MessageType.Info;
        }

        private static bool HasReadOnlyAttribute(MemberInfo memberInfo)
        {
            if (memberInfo == null)
                return false;

            var attributes = memberInfo.GetCustomAttributes(true);
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (attribute != null && LooksLikeReadOnlyAttribute(attribute.GetType()))
                    return true;
            }

            return false;
        }

        private static bool ShouldForceExpandCustomProperty(MemberInfo memberInfo)
        {
            if (memberInfo == null)
                return false;

            var attributes = memberInfo.GetCustomAttributes(true);
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (attribute == null || !LooksLikeListDrawerSettingsAttribute(attribute.GetType()))
                    continue;

                var alwaysExpanded = TryReadBoolMember(attribute, "AlwaysExpanded") ??
                                     TryReadBoolMember(attribute, "alwaysExpanded");
                if (alwaysExpanded.HasValue && alwaysExpanded.Value)
                    return true;
            }

            return false;
        }

        private static string ResolveAttributeLabel(MemberInfo memberInfo)
        {
            var attributes = memberInfo.GetCustomAttributes(true);
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (attribute == null || !LooksLikeLabelAttribute(attribute.GetType()))
                    continue;

                var label = TryReadStringMember(attribute, "Text") ??
                            TryReadStringMember(attribute, "Label") ??
                            TryReadStringMember(attribute, "Name") ??
                            TryReadStringMember(attribute, "DisplayName") ??
                            TryReadStringMember(attribute, "displayName") ??
                            TryReadStringMember(attribute, "LabelText") ??
                            TryReadStringMember(attribute, "text") ??
                            TryReadStringMember(attribute, "label");

                if (!string.IsNullOrEmpty(label))
                    return label;
            }

            return null;
        }

        private static string ResolveAttributeTooltip(MemberInfo memberInfo)
        {
            var attributes = memberInfo.GetCustomAttributes(true);
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (attribute == null)
                    continue;

                var type = attribute.GetType();
                if (type == typeof(TooltipAttribute) || LooksLikeTooltipAttribute(type))
                {
                    var tooltip = TryReadStringMember(attribute, "tooltip") ??
                                  TryReadStringMember(attribute, "Tooltip") ??
                                  TryReadStringMember(attribute, "Text") ??
                                  TryReadStringMember(attribute, "text");
                    if (!string.IsNullOrEmpty(tooltip))
                        return tooltip;
                }
            }

            return null;
        }

        private static void CollectCustomPropertyPaths(SerializedObject serializedObject, Type targetType, List<string> propertyPaths)
        {
            if (propertyPaths == null)
                return;

            propertyPaths.Clear();
            if (serializedObject == null || targetType == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();

            var iterator = serializedObject.GetIterator();
            if (!iterator.NextVisible(true))
                return;

            do
            {
                if (ShouldShowCustomProperty(iterator.propertyPath, targetType))
                    propertyPaths.Add(iterator.propertyPath);
            }
            while (iterator.NextVisible(false));
        }

        private static bool ShouldShowCustomProperty(string propertyPath, Type targetType)
        {
            if (targetType == null || string.IsNullOrEmpty(propertyPath))
                return false;

            var rootName = GetRootPropertyName(propertyPath);
            if (rootName == "m_Script")
                return false;

            var field = FindFieldInHierarchy(targetType, rootName);
            if (field == null)
                return false;

            return ShouldShowCustomMember(field, targetType);
        }

        private static bool ShouldShowCustomMember(MemberInfo memberInfo, Type targetType)
        {
            if (memberInfo == null || targetType == null)
                return false;

            var field = memberInfo as FieldInfo;
            if (field == null)
                return false;

            var declaringType = field.DeclaringType;
            if (declaringType == null)
                return false;

            if (!declaringType.IsAssignableFrom(targetType))
                return false;

            return !IsFrameworkPanelField(field) && !IsGeneratedPanelDataField(field);
        }

        private static bool IsFrameworkPanelField(FieldInfo field)
        {
            if (field == null || field.DeclaringType == null)
                return true;

            return IsFrameworkPanelType(field.DeclaringType);
        }

        private static bool IsFrameworkPanelType(Type declaringType)
        {
            return declaringType != null &&
                   typeof(UIPanel).IsAssignableFrom(declaringType) &&
                   declaringType.Assembly == typeof(UIPanel).Assembly;
        }

        private static bool IsGeneratedPanelDataField(FieldInfo field)
        {
            return field != null &&
                   field.Name == "mData" &&
                   typeof(IUIData).IsAssignableFrom(field.FieldType);
        }

        private static bool HasValidTargets(UnityEngine.Object[] editorTargets)
        {
            if (editorTargets == null || editorTargets.Length == 0)
                return false;

            for (var i = 0; i < editorTargets.Length; i++)
            {
                if (editorTargets[i] == null)
                    return false;
            }

            return true;
        }

        private bool TryGetSerializedObject(out SerializedObject currentSerializedObject)
        {
            currentSerializedObject = null;
            if (!HasValidTargets(targets))
                return false;

            try
            {
                currentSerializedObject = serializedObject;
            }
            catch (Exception exception)
            {
                if (IsInvalidSerializedObjectException(exception))
                    return false;

                throw;
            }

            return currentSerializedObject != null && currentSerializedObject.targetObject != null;
        }

        private static bool IsInvalidSerializedObjectException(Exception exception)
        {
            return exception != null &&
                   (exception is InvalidOperationException ||
                    exception.GetType().Name == "SerializedObjectNotCreatableException");
        }

        private static bool LooksLikeTitleAttribute(Type attributeType)
        {
            if (attributeType == null)
                return false;

            return attributeType.Name == "TitleAttribute";
        }

        private static bool LooksLikeInfoBoxAttribute(Type attributeType)
        {
            if (attributeType == null)
                return false;

            var name = attributeType.Name;
            return name == "InfoBoxAttribute" ||
                   name == "HelpBoxAttribute";
        }

        private static bool LooksLikeReadOnlyAttribute(Type attributeType)
        {
            if (attributeType == null)
                return false;

            return attributeType.Name == "ReadOnlyAttribute";
        }

        private static bool LooksLikeTooltipAttribute(Type attributeType)
        {
            if (attributeType == null)
                return false;

            var name = attributeType.Name;
            return name == "TooltipAttribute" ||
                   name == "PropertyTooltipAttribute";
        }

        private static bool LooksLikeListDrawerSettingsAttribute(Type attributeType)
        {
            if (attributeType == null)
                return false;

            return attributeType.Name == "ListDrawerSettingsAttribute";
        }

        private static bool LooksLikeLabelAttribute(Type attributeType)
        {
            if (attributeType == null)
                return false;

            var name = attributeType.Name;
            return name == "LabelTextAttribute" ||
                   name == "LabelAttribute" ||
                   name == "PropertyLabelAttribute" ||
                   name == "DisplayNameAttribute" ||
                   name == "InspectorNameAttribute";
        }

        private static string TryReadStringMember(object instance, string memberName)
        {
            var type = instance.GetType();
            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.PropertyType == typeof(string))
                return property.GetValue(instance, null) as string;

            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(string))
                return field.GetValue(instance) as string;

            return null;
        }

        private static bool? TryReadBoolMember(object instance, string memberName)
        {
            var type = instance.GetType();
            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.PropertyType == typeof(bool))
                return (bool)property.GetValue(instance, null);

            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(bool))
                return (bool)field.GetValue(instance);

            return null;
        }

        private static object TryReadObjectMember(object instance, string memberName)
        {
            var type = instance.GetType();
            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
                return property.GetValue(instance, null);

            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null ? field.GetValue(instance) : null;
        }

        private static VisualElement CreateSectionContainer(string modifierClass)
        {
            var section = new VisualElement();
            section.AddToClassList("uipanel-section");
            section.AddToClassList(modifierClass);
            return section;
        }

        private static VisualElement CreateSubSection(string title, string modifierClass, string sessionStateKey)
        {
            var subSection = new VisualElement();
            subSection.AddToClassList("uipanel-subsection");
            subSection.AddToClassList(modifierClass);

            var foldout = CreateRememberedFoldout(
                title,
                sessionStateKey,
                true,
                "uipanel-subsection-foldout");
            subSection.Add(foldout);

            var content = new VisualElement();
            content.AddToClassList("uipanel-subsection-content");
            foldout.Add(content);
            return subSection;
        }

        private static VisualElement CreateHelpBox(string text)
        {
            var helpBox = new VisualElement();
            helpBox.AddToClassList("uipanel-helpbox");

            var icon = new Label("i");
            icon.AddToClassList("uipanel-helpbox-icon");
            helpBox.Add(icon);

            var label = new Label(text);
            label.AddToClassList("uipanel-helpbox-text");
            helpBox.Add(label);
            return helpBox;
        }

        private static VisualElement CreatePropertyRow(string label, SerializedProperty property)
        {
            var row = CreateFieldRow(label);
            var field = new PropertyField(property, string.Empty);
            field.AddToClassList("uipanel-field");
            row.Add(field);
            return row;
        }

        private static VisualElement CreateFieldRow(string label)
        {
            var row = new VisualElement();
            row.AddToClassList("uipanel-field-row");

            var labelElement = new Label(label);
            labelElement.AddToClassList("uipanel-field-label");
            row.Add(labelElement);
            return row;
        }

        private void RebuildInspector()
        {
            var active = ActiveEditorTracker.sharedTracker;
            if (active != null)
                active.ForceRebuild();
        }

        private static Foldout CreateRememberedFoldout(string title, string stateKey, bool defaultValue, string className)
        {
            var foldout = new Foldout
            {
                text = title,
                value = GetSavedBool(stateKey, defaultValue),
                viewDataKey = stateKey
            };

            if (!string.IsNullOrEmpty(className))
                foldout.AddToClassList(className);

            foldout.RegisterValueChangedCallback(evt => SetSavedBool(stateKey, evt.newValue));
            return foldout;
        }

        private static bool GetSavedBool(string key, bool defaultValue)
        {
            if (EditorPrefs.HasKey(key))
                return EditorPrefs.GetBool(key, defaultValue);

            return SessionState.GetBool(key, defaultValue);
        }

        private static void SetSavedBool(string key, bool value)
        {
            SessionState.SetBool(key, value);
            EditorPrefs.SetBool(key, value);
        }

        private static string GetSavedString(string key, string defaultValue)
        {
            const string MissingValue = "__YOKIFRAME_MISSING_SESSION_STATE__";
            var sessionValue = SessionState.GetString(key, MissingValue);
            if (sessionValue != MissingValue)
                return sessionValue;

            return EditorPrefs.HasKey(key) ? EditorPrefs.GetString(key, defaultValue) : defaultValue;
        }

        private static void SetSavedString(string key, string value)
        {
            SessionState.SetString(key, value);
            EditorPrefs.SetString(key, value);
        }
    }
}
#endif
