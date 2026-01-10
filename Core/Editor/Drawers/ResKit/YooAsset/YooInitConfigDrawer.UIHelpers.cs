#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT && UNITY_2022_1_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YooInitConfig 属性绘制器 - UI 辅助方法
    /// </summary>
    public partial class YooInitConfigDrawer
    {
        #region 验证提示

        /// <summary>
        /// 创建验证提示标签
        /// </summary>
        private static VisualElement CreateValidationHint()
        {
            var hint = new Label();
            hint.style.fontSize = 10;
            hint.style.marginTop = Spacing.XS;
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.display = DisplayStyle.None;
            return hint;
        }

        #endregion

        #region 按钮

        /// <summary>
        /// 创建重置按钮
        /// </summary>
        private static VisualElement CreateResetButton(string text, Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.style.marginTop = Spacing.SM;
            btn.style.height = 22;
            btn.style.backgroundColor = new StyleColor(Colors.LayerElevated);
            btn.style.borderTopLeftRadius = Radius.SM;
            btn.style.borderTopRightRadius = Radius.SM;
            btn.style.borderBottomLeftRadius = Radius.SM;
            btn.style.borderBottomRightRadius = Radius.SM;
            btn.style.borderLeftWidth = 1;
            btn.style.borderRightWidth = 1;
            btn.style.borderTopWidth = 1;
            btn.style.borderBottomWidth = 1;
            btn.style.borderLeftColor = new StyleColor(Colors.BorderDefault);
            btn.style.borderRightColor = new StyleColor(Colors.BorderDefault);
            btn.style.borderTopColor = new StyleColor(Colors.BorderDefault);
            btn.style.borderBottomColor = new StyleColor(Colors.BorderDefault);
            btn.style.color = new StyleColor(Colors.TextSecondary);
            btn.style.fontSize = 10;
            return btn;
        }

        /// <summary>
        /// 创建操作按钮
        /// </summary>
        private static Button CreateActionButton(string text, Color textColor, Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.style.height = 26;
            btn.style.backgroundColor = new StyleColor(Colors.LayerElevated);
            btn.style.borderTopLeftRadius = Radius.SM;
            btn.style.borderTopRightRadius = Radius.SM;
            btn.style.borderBottomLeftRadius = Radius.SM;
            btn.style.borderBottomRightRadius = Radius.SM;
            btn.style.borderLeftWidth = 1;
            btn.style.borderRightWidth = 1;
            btn.style.borderTopWidth = 1;
            btn.style.borderBottomWidth = 1;
            btn.style.borderLeftColor = new StyleColor(Colors.BorderDefault);
            btn.style.borderRightColor = new StyleColor(Colors.BorderDefault);
            btn.style.borderTopColor = new StyleColor(Colors.BorderDefault);
            btn.style.borderBottomColor = new StyleColor(Colors.BorderDefault);
            btn.style.color = new StyleColor(textColor);
            btn.style.fontSize = 11;
            return btn;
        }

        #endregion

        #region 下拉框

        /// <summary>
        /// 创建偏移量下拉框
        /// </summary>
        private static VisualElement CreateOffsetDropdown(UnityEditor.SerializedProperty parent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = Spacing.XS;
            row.style.marginBottom = Spacing.XS;

            var label = new Label("偏移量");
            label.style.minWidth = 130;
            label.style.width = 130;
            label.style.color = new StyleColor(Colors.TextSecondary);
            row.Add(label);

            var offsetProp = parent.FindPropertyRelative("FileOffset");
            int currentIndex = Array.IndexOf(sOffsetValues, offsetProp.intValue);
            if (currentIndex < 0) currentIndex = 1;

            var dropdown = new DropdownField(sOffsetChoices, currentIndex);
            dropdown.style.flexGrow = 1;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                int index = sOffsetChoices.IndexOf(evt.newValue);
                if (index >= 0 && index < sOffsetValues.Length)
                {
                    offsetProp.intValue = sOffsetValues[index];
                    parent.serializedObject.ApplyModifiedProperties();
                }
            });
            row.Add(dropdown);

            return row;
        }

        /// <summary>
        /// 创建带标签的下拉框
        /// </summary>
        private static VisualElement CreateLabeledDropdown(string label, List<string> choices, string defaultValue, Action<string> onValueChanged)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = Spacing.XS;
            row.style.marginBottom = Spacing.XS;

            var labelElement = new Label(label);
            labelElement.style.minWidth = 80;
            labelElement.style.width = 80;
            labelElement.style.color = new StyleColor(Colors.TextSecondary);
            labelElement.style.fontSize = 11;
            row.Add(labelElement);

            var dropdown = new DropdownField(choices, choices.IndexOf(defaultValue));
            dropdown.style.flexGrow = 1;
            dropdown.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));
            row.Add(dropdown);

            return row;
        }

        #endregion

        #region 文本框

        /// <summary>
        /// 创建带标签的文本框
        /// </summary>
        private static VisualElement CreateLabeledTextField(string label, string defaultValue, string placeholder, Action<string> onValueChanged)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = Spacing.XS;
            row.style.marginBottom = Spacing.XS;

            var labelElement = new Label(label);
            labelElement.style.minWidth = 80;
            labelElement.style.width = 80;
            labelElement.style.color = new StyleColor(Colors.TextSecondary);
            labelElement.style.fontSize = 11;
            row.Add(labelElement);

            var textField = new TextField { value = defaultValue };
            textField.style.flexGrow = 1;
            textField.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));
            row.Add(textField);

            return row;
        }

        #endregion

        #region 开关

        /// <summary>
        /// 创建带标签的开关（未使用，保留备用）
        /// </summary>
        private static VisualElement CreateLabeledToggle(string label, bool defaultValue, Action<bool> onValueChanged)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = Spacing.XS;
            row.style.marginBottom = Spacing.XS;

            var labelElement = new Label(label);
            labelElement.style.minWidth = 80;
            labelElement.style.width = 80;
            labelElement.style.color = new StyleColor(Colors.TextSecondary);
            labelElement.style.fontSize = 11;
            row.Add(labelElement);

            var toggle = new Toggle { value = defaultValue };
            toggle.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));
            row.Add(toggle);

            return row;
        }

        #endregion

        #region 属性字段

        /// <summary>
        /// 创建属性字段
        /// </summary>
        private static PropertyField CreatePropertyField(UnityEditor.SerializedProperty parent, string propertyName, string label)
        {
            var prop = parent.FindPropertyRelative(propertyName);
            var field = new PropertyField(prop, label);
            field.style.marginTop = Spacing.XS;
            field.style.marginBottom = Spacing.XS;
            field.BindProperty(prop);

            field.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var labelElement = field.Q<Label>();
                if (labelElement != null)
                {
                    labelElement.style.minWidth = 130;
                    labelElement.style.width = 130;
                }
            });

            return field;
        }

        /// <summary>
        /// 创建播放模式字段
        /// </summary>
        private static VisualElement CreatePlayModeField(UnityEditor.SerializedProperty parent, string propertyName, string label, bool filterEditorMode)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = Spacing.XS;
            row.style.marginBottom = Spacing.XS;

            var labelElement = new Label(label);
            labelElement.style.minWidth = 130;
            labelElement.style.width = 130;
            labelElement.style.color = new StyleColor(Colors.TextSecondary);
            row.Add(labelElement);

            var prop = parent.FindPropertyRelative(propertyName);

            if (filterEditorMode)
            {
                var choices = new List<string> { "OfflinePlayMode", "HostPlayMode", "WebPlayMode", "CustomPlayMode" };
                var values = new[] { 1, 2, 3, 4 };

                int currentValue = prop.enumValueIndex;
                int currentIndex = Array.IndexOf(values, currentValue);
                if (currentIndex < 0)
                {
                    currentIndex = 0;
                    prop.enumValueIndex = values[0];
                    parent.serializedObject.ApplyModifiedProperties();
                }

                var dropdown = new DropdownField(choices, currentIndex);
                dropdown.style.flexGrow = 1;
                dropdown.RegisterValueChangedCallback(evt =>
                {
                    int index = choices.IndexOf(evt.newValue);
                    if (index >= 0 && index < values.Length)
                    {
                        prop.enumValueIndex = values[index];
                        parent.serializedObject.ApplyModifiedProperties();
                    }
                });
                row.Add(dropdown);
            }
            else
            {
                var field = new PropertyField(prop, "");
                field.style.flexGrow = 1;
                field.BindProperty(prop);
                row.Add(field);
            }

            return row;
        }

        #endregion

        #region 列表字段

        /// <summary>
        /// 创建资源包列表字段
        /// </summary>
        private static VisualElement CreatePackageListField(UnityEditor.SerializedProperty parent, string propertyName, string label)
        {
            var container = new VisualElement();
            container.style.marginTop = Spacing.SM;
            container.style.marginBottom = Spacing.SM;

            var labelRow = new VisualElement();
            labelRow.style.flexDirection = FlexDirection.Row;
            labelRow.style.alignItems = Align.Center;
            labelRow.style.marginBottom = Spacing.XS;

            var labelElement = new Label(label);
            labelElement.style.color = new StyleColor(Colors.TextSecondary);
            labelElement.style.fontSize = 12;
            labelElement.style.flexGrow = 1;
            labelRow.Add(labelElement);

            container.Add(labelRow);

            var listContainer = new VisualElement();
            listContainer.style.backgroundColor = new StyleColor(Colors.LayerElevated);
            listContainer.style.borderTopLeftRadius = Radius.MD;
            listContainer.style.borderTopRightRadius = Radius.MD;
            listContainer.style.borderBottomLeftRadius = Radius.MD;
            listContainer.style.borderBottomRightRadius = Radius.MD;
            listContainer.style.paddingLeft = Spacing.SM;
            listContainer.style.paddingRight = Spacing.SM;
            listContainer.style.paddingTop = Spacing.SM;
            listContainer.style.paddingBottom = Spacing.SM;
            container.Add(listContainer);

            var listProp = parent.FindPropertyRelative(propertyName);

            void RefreshList()
            {
                listContainer.Clear();

                for (int i = 0; i < listProp.arraySize; i++)
                {
                    int index = i;
                    var itemProp = listProp.GetArrayElementAtIndex(i);
                    listContainer.Add(CreatePackageListItem(listProp, itemProp, index, parent, RefreshList));
                }

                listContainer.Add(CreateAddPackageButton(listProp, parent, RefreshList));
            }

            RefreshList();

            return container;
        }

        /// <summary>
        /// 创建资源包列表项
        /// </summary>
        private static VisualElement CreatePackageListItem(UnityEditor.SerializedProperty listProp, UnityEditor.SerializedProperty itemProp, int index, UnityEditor.SerializedProperty parent, Action refreshList)
        {
            var itemRow = new VisualElement();
            itemRow.style.flexDirection = FlexDirection.Row;
            itemRow.style.alignItems = Align.Center;
            itemRow.style.marginBottom = Spacing.XS;

            var indexLabel = new Label(index == 0 ? "默认" : $"#{index + 1}");
            indexLabel.style.width = 32;
            indexLabel.style.color = new StyleColor(index == 0 ? Colors.BrandPrimary : Colors.TextTertiary);
            indexLabel.style.fontSize = 10;
            indexLabel.style.unityFontStyleAndWeight = index == 0 ? FontStyle.Bold : FontStyle.Normal;
            itemRow.Add(indexLabel);

            var textField = new TextField { value = itemProp.stringValue };
            textField.style.flexGrow = 1;
            textField.RegisterValueChangedCallback(evt =>
            {
                itemProp.stringValue = evt.newValue;
                parent.serializedObject.ApplyModifiedProperties();
            });
            itemRow.Add(textField);

            if (listProp.arraySize > 1)
            {
                var deleteBtn = new Button(() =>
                {
                    listProp.DeleteArrayElementAtIndex(index);
                    parent.serializedObject.ApplyModifiedProperties();
                    refreshList();
                }) { text = "×" };
                deleteBtn.style.width = 20;
                deleteBtn.style.height = 20;
                deleteBtn.style.marginLeft = Spacing.XS;
                deleteBtn.style.backgroundColor = new StyleColor(Color.clear);
                deleteBtn.style.borderLeftWidth = 0;
                deleteBtn.style.borderRightWidth = 0;
                deleteBtn.style.borderTopWidth = 0;
                deleteBtn.style.borderBottomWidth = 0;
                deleteBtn.style.color = new StyleColor(Colors.StatusError);
                deleteBtn.style.fontSize = 14;
                deleteBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
                itemRow.Add(deleteBtn);
            }

            return itemRow;
        }

        /// <summary>
        /// 创建添加资源包按钮
        /// </summary>
        private static VisualElement CreateAddPackageButton(UnityEditor.SerializedProperty listProp, UnityEditor.SerializedProperty parent, Action refreshList)
        {
            var addBtn = new Button(() =>
            {
                listProp.InsertArrayElementAtIndex(listProp.arraySize);
                var newItem = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
                newItem.stringValue = $"Package{listProp.arraySize}";
                parent.serializedObject.ApplyModifiedProperties();
                refreshList();
            }) { text = "+ 添加包" };
            addBtn.style.marginTop = Spacing.SM;
            addBtn.style.height = 24;
            addBtn.style.backgroundColor = new StyleColor(Colors.LayerCard);
            addBtn.style.borderTopLeftRadius = Radius.SM;
            addBtn.style.borderTopRightRadius = Radius.SM;
            addBtn.style.borderBottomLeftRadius = Radius.SM;
            addBtn.style.borderBottomRightRadius = Radius.SM;
            addBtn.style.borderLeftWidth = 1;
            addBtn.style.borderRightWidth = 1;
            addBtn.style.borderTopWidth = 1;
            addBtn.style.borderBottomWidth = 1;
            addBtn.style.borderLeftColor = new StyleColor(Colors.BorderDefault);
            addBtn.style.borderRightColor = new StyleColor(Colors.BorderDefault);
            addBtn.style.borderTopColor = new StyleColor(Colors.BorderDefault);
            addBtn.style.borderBottomColor = new StyleColor(Colors.BorderDefault);
            addBtn.style.color = new StyleColor(Colors.TextSecondary);
            return addBtn;
        }

        #endregion

        #region 卡片

        /// <summary>
        /// EditorPrefs 键前缀
        /// </summary>
        private const string PREFS_KEY_PREFIX = "YokiFrame.YooInitConfig.Foldout.";

        /// <summary>
        /// 获取折叠状态的 EditorPrefs 键
        /// </summary>
        private static string GetFoldoutPrefsKey(string cardId) => PREFS_KEY_PREFIX + cardId;

        /// <summary>
        /// 创建配置卡片（带持久化折叠状态）
        /// </summary>
        /// <param name="title">卡片标题</param>
        /// <param name="iconId">图标 ID</param>
        /// <param name="cardId">卡片唯一标识（用于持久化折叠状态）</param>
        /// <param name="collapsible">是否可折叠</param>
        /// <param name="defaultExpanded">默认展开状态（仅首次使用时生效）</param>
        private static (VisualElement card, VisualElement body) CreateConfigCard(string title, string iconId, string cardId, bool collapsible = true, bool defaultExpanded = true)
        {
            // 从 EditorPrefs 读取折叠状态，如果没有则使用默认值
            bool isExpanded = EditorPrefs.GetBool(GetFoldoutPrefsKey(cardId), defaultExpanded);

            var card = new VisualElement();
            card.style.backgroundColor = new StyleColor(Colors.LayerCard);
            card.style.borderTopLeftRadius = Radius.LG;
            card.style.borderTopRightRadius = Radius.LG;
            card.style.borderBottomLeftRadius = Radius.LG;
            card.style.borderBottomRightRadius = Radius.LG;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftColor = new StyleColor(Colors.BorderDefault);
            card.style.borderRightColor = new StyleColor(Colors.BorderDefault);
            card.style.borderTopColor = new StyleColor(Colors.BorderDefault);
            card.style.borderBottomColor = new StyleColor(Colors.BorderDefault);

            var header = CreateCardHeader(title, iconId, collapsible, isExpanded);
            card.Add(header);

            var body = new VisualElement();
            body.style.paddingLeft = Spacing.MD;
            body.style.paddingRight = Spacing.MD;
            body.style.paddingTop = Spacing.SM;
            body.style.paddingBottom = Spacing.SM;
            body.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            card.Add(body);

            // 折叠交互
            if (collapsible)
            {
                var arrowLabel = header.Q<Label>("arrow-label");
                SetupCollapsibleBehavior(header, body, arrowLabel, cardId);
            }

            return (card, body);
        }

        /// <summary>
        /// 创建配置卡片（兼容旧版本，不带 cardId）
        /// </summary>
        private static (VisualElement card, VisualElement body) CreateConfigCard(string title, string iconId, bool collapsible = true, bool defaultExpanded = true)
        {
            // 使用标题作为 cardId
            return CreateConfigCard(title, iconId, title, collapsible, defaultExpanded);
        }

        /// <summary>
        /// 创建卡片头部
        /// </summary>
        private static VisualElement CreateCardHeader(string title, string iconId, bool collapsible, bool defaultExpanded)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = Spacing.MD;
            header.style.paddingRight = Spacing.MD;
            header.style.paddingTop = Spacing.SM;
            header.style.paddingBottom = Spacing.SM;
            header.style.backgroundColor = new StyleColor(Colors.LayerElevated);
            header.style.borderTopLeftRadius = Radius.LG;
            header.style.borderTopRightRadius = Radius.LG;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(Colors.BorderLight);

            // 折叠箭头
            if (collapsible)
            {
                var arrowLabel = new Label(defaultExpanded ? "▼" : "▶");
                arrowLabel.name = "arrow-label";
                arrowLabel.style.fontSize = 10;
                arrowLabel.style.color = new StyleColor(Colors.TextTertiary);
                arrowLabel.style.marginRight = Spacing.XS;
                arrowLabel.style.width = 12;
                arrowLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                header.Add(arrowLabel);
            }

            if (!string.IsNullOrEmpty(iconId))
            {
                var icon = new Image { image = KitIcons.GetTexture(iconId) };
                icon.style.width = 14;
                icon.style.height = 14;
                icon.style.marginRight = Spacing.XS;
                icon.tintColor = Colors.TextSecondary;
                header.Add(icon);
            }

            var titleLabel = new Label(title);
            titleLabel.style.color = new StyleColor(Colors.TextPrimary);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 12;
            titleLabel.style.flexGrow = 1;
            header.Add(titleLabel);

            // 初始状态下的圆角
            if (collapsible && !defaultExpanded)
            {
                header.style.borderBottomLeftRadius = Radius.LG;
                header.style.borderBottomRightRadius = Radius.LG;
                header.style.borderBottomWidth = 0;
            }

            return header;
        }

        /// <summary>
        /// 设置折叠行为（带持久化）
        /// </summary>
        private static void SetupCollapsibleBehavior(VisualElement header, VisualElement body, Label arrowLabel, string cardId)
        {
            header.RegisterCallback<MouseEnterEvent>(_ => header.style.backgroundColor = new StyleColor(new Color(Colors.LayerElevated.r * 1.1f, Colors.LayerElevated.g * 1.1f, Colors.LayerElevated.b * 1.1f)));
            header.RegisterCallback<MouseLeaveEvent>(_ => header.style.backgroundColor = new StyleColor(Colors.LayerElevated));
            header.RegisterCallback<ClickEvent>(_ =>
            {
                bool isExpanded = body.style.display == DisplayStyle.Flex;
                bool newState = !isExpanded;
                
                body.style.display = newState ? DisplayStyle.Flex : DisplayStyle.None;
                if (arrowLabel != null)
                    arrowLabel.text = newState ? "▼" : "▶";
                
                // 折叠时调整底部圆角
                header.style.borderBottomLeftRadius = newState ? 0 : Radius.LG;
                header.style.borderBottomRightRadius = newState ? 0 : Radius.LG;
                header.style.borderBottomWidth = newState ? 1 : 0;

                // 保存折叠状态到 EditorPrefs
                EditorPrefs.SetBool(GetFoldoutPrefsKey(cardId), newState);
            });
        }

        /// <summary>
        /// 创建加密配置卡片
        /// </summary>
        private static VisualElement CreateEncryptionCard(string title, string description, Color accentColor, VisualElement content)
        {
            var card = new VisualElement();
            card.style.backgroundColor = new StyleColor(new Color(accentColor.r * 0.15f, accentColor.g * 0.15f, accentColor.b * 0.15f, 0.5f));
            card.style.borderTopLeftRadius = Radius.MD;
            card.style.borderTopRightRadius = Radius.MD;
            card.style.borderBottomLeftRadius = Radius.MD;
            card.style.borderBottomRightRadius = Radius.MD;
            card.style.borderLeftWidth = 2;
            card.style.borderLeftColor = new StyleColor(accentColor);
            card.style.paddingLeft = Spacing.MD;
            card.style.paddingRight = Spacing.MD;
            card.style.paddingTop = Spacing.SM;
            card.style.paddingBottom = Spacing.SM;

            var titleLabel = new Label(title);
            titleLabel.style.color = new StyleColor(accentColor);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 11;
            titleLabel.style.marginBottom = Spacing.XS;
            card.Add(titleLabel);

            var descLabel = new Label(description);
            descLabel.style.color = new StyleColor(Colors.TextSecondary);
            descLabel.style.fontSize = 10;
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            descLabel.style.marginBottom = content != null ? Spacing.SM : 0;
            card.Add(descLabel);

            if (content != null)
                card.Add(content);

            return card;
        }

        #endregion
    }
}
#endif
