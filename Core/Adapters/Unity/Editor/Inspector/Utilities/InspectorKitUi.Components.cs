#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace YokiFrame.Unity.Inspector
{
    /// <summary>
    /// InspectorKit 的可组合 UI Toolkit 组件。
    /// 业务 Drawer 只描述字段和操作，颜色、间距、折叠与控件布局由此处统一提供。
    /// </summary>
    public static partial class InspectorKitUi
    {
        private const string CARD_STATE_PREFIX = "YokiFrame.InspectorKit.Card.";

        /// <summary>
        /// 创建 Inspector 内容面板，用于承载一个完整配置对象。
        /// </summary>
        /// <param name="title">面板标题；为空时不创建标题。</param>
        /// <returns>已应用 InspectorKit 面板样式的容器。</returns>
        public static VisualElement CreatePanel(string title)
        {
            VisualElement panel = new();
            panel.AddToClassList("yoki-editor-inspector__panel");
            if (!string.IsNullOrEmpty(title))
            {
                Label label = new(title);
                label.AddToClassList("yoki-editor-inspector__panel-title");
                panel.Add(label);
            }

            return panel;
        }

        /// <summary>
        /// 创建可折叠卡片，并通过稳定状态键在 EditorPrefs 中保存展开状态。
        /// </summary>
        /// <param name="title">卡片标题。</param>
        /// <param name="stateKey">跨 Inspector 保存状态的稳定键；为空时不持久化。</param>
        /// <param name="initialState">首次显示时的展开状态。</param>
        /// <param name="buildContent">向卡片内容区添加控件的回调。</param>
        /// <returns>可以直接添加到视觉树的卡片根元素。</returns>
        public static VisualElement CreateCard(
            string title,
            string stateKey,
            InspectorCardInitialState initialState,
            Action<VisualElement> buildContent)
        {
            bool expanded = ResolveInitialCardState(stateKey, initialState);
            VisualElement card = new();
            card.AddToClassList("yoki-editor-inspector__card");

            VisualElement header = CreateCardHeader(title, out Label arrow);
            VisualElement body = new();
            body.AddToClassList("yoki-editor-inspector__card-body");
            buildContent?.Invoke(body);
            SetCardExpanded(body, arrow, expanded);

            header.RegisterCallback<ClickEvent>(_ => ToggleCard(body, arrow, stateKey));
            card.Add(header);
            card.Add(body);
            return card;
        }

        /// <summary>
        /// 创建左侧固定标签、右侧自适应控件的标准字段行。
        /// </summary>
        /// <param name="labelText">字段标签。</param>
        /// <param name="field">字段控件。</param>
        /// <returns>标准字段行。</returns>
        public static VisualElement CreateFieldRow(string labelText, VisualElement field)
        {
            VisualElement row = new();
            row.AddToClassList("yoki-editor-inspector__row");

            Label label = new(labelText ?? string.Empty);
            label.AddToClassList("yoki-editor-inspector__row-label");
            row.Add(label);

            if (field != null)
            {
                field.AddToClassList("yoki-editor-inspector__row-field");
                row.Add(field);
            }

            return row;
        }

        /// <summary>
        /// 创建标签位于控件上方的纵向字段行，适合窄 Inspector 或长文本输入。
        /// </summary>
        /// <param name="labelText">字段标签。</param>
        /// <param name="field">字段控件。</param>
        /// <returns>纵向排列的标准字段行。</returns>
        public static VisualElement CreateStackedFieldRow(string labelText, VisualElement field)
        {
            VisualElement row = CreateFieldRow(labelText, field);
            row.AddToClassList("yoki-editor-inspector__row--stacked");
            return row;
        }

        /// <summary>
        /// 创建由 Unity PropertyField 负责完整序列化绑定的字段行。
        /// </summary>
        /// <param name="property">目标序列化属性。</param>
        /// <param name="label">字段标签。</param>
        /// <returns>绑定后的字段行；属性为空时返回错误提示。</returns>
        public static VisualElement CreatePropertyRow(SerializedProperty property, string label)
        {
            if (property == null)
                return CreateInfoBox("未找到需要绘制的序列化字段。", InspectorInfoBoxType.Error);

            PropertyField field = new(property, string.Empty);
            field.BindProperty(property);
            return CreateFieldRow(label, field);
        }

        /// <summary>
        /// 创建与字符串 SerializedProperty 双向绑定的字段行。
        /// </summary>
        /// <param name="property">字符串属性。</param>
        /// <param name="label">字段标签。</param>
        /// <returns>绑定后的字符串字段行。</returns>
        public static VisualElement CreateStringRow(SerializedProperty property, string label)
        {
            if (property == null)
                return CreateInfoBox("未找到字符串序列化字段。", InspectorInfoBoxType.Error);

            TextField field = new();
            field.BindProperty(property);
            return CreateFieldRow(label, field);
        }

        /// <summary>
        /// 创建由回调同步的字符串字段行，适合外部设置系统而非 SerializedProperty。
        /// </summary>
        /// <param name="label">字段标签。</param>
        /// <param name="value">初始文本。</param>
        /// <param name="onChanged">文本变化回调。</param>
        /// <returns>标准字符串字段行。</returns>
        public static VisualElement CreateStringRow(
            string label,
            string value,
            Action<string> onChanged)
        {
            TextField field = new() { value = value ?? string.Empty };
            field.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
            return CreateFieldRow(label, field);
        }

        /// <summary>
        /// 创建只读字符串字段行，用于展示由其它配置推导出的结果。
        /// </summary>
        /// <param name="label">字段标签。</param>
        /// <param name="value">只读显示文本。</param>
        /// <returns>禁止编辑但保持标准字段布局的字符串行。</returns>
        public static VisualElement CreateReadOnlyStringRow(string label, string value)
        {
            TextField field = new()
            {
                value = value ?? string.Empty,
                isReadOnly = true
            };
            field.AddToClassList("yoki-editor-inspector__field--readonly");
            return CreateFieldRow(label, field);
        }

        /// <summary>
        /// 创建与整数 SerializedProperty 双向绑定的字段行。
        /// </summary>
        /// <param name="property">整数属性。</param>
        /// <param name="label">字段标签。</param>
        /// <returns>绑定后的整数字段行。</returns>
        public static VisualElement CreateIntegerRow(SerializedProperty property, string label)
        {
            if (property == null)
                return CreateInfoBox("未找到整数序列化字段。", InspectorInfoBoxType.Error);

            IntegerField field = new();
            field.BindProperty(property);
            return CreateFieldRow(label, field);
        }

        /// <summary>
        /// 创建一个索引驱动的下拉字段行，适合过滤枚举值或映射外部设置。
        /// </summary>
        /// <param name="label">字段标签。</param>
        /// <param name="choices">显示选项。</param>
        /// <param name="selectedIndex">当前选中索引。</param>
        /// <param name="onChanged">索引变化回调。</param>
        /// <returns>下拉字段行。</returns>
        public static VisualElement CreateDropdownRow(
            string label,
            IReadOnlyList<string> choices,
            int selectedIndex,
            Action<int> onChanged)
        {
            List<string> values = CopyChoices(choices);
            if (values.Count == 0)
                return CreateInfoBox("当前没有可用选项。", InspectorInfoBoxType.Warning);

            int index = ClampIndex(selectedIndex, values.Count);
            DropdownField field = new(values, index);
            field.RegisterValueChangedCallback(evt => NotifyDropdownChanged(values, evt.newValue, onChanged));
            return CreateFieldRow(label, field);
        }

        /// <summary>
        /// 创建 InspectorKit 标准操作按钮。
        /// </summary>
        /// <param name="text">按钮文本。</param>
        /// <param name="action">点击操作。</param>
        /// <param name="style">语义样式。</param>
        /// <param name="tooltip">悬停说明。</param>
        /// <returns>标准按钮。</returns>
        public static Button CreateActionButton(
            string text,
            Action action,
            InspectorActionStyle style = InspectorActionStyle.Default,
            string tooltip = null)
        {
            Button button = new(action)
            {
                text = text ?? string.Empty,
                tooltip = tooltip ?? string.Empty
            };
            button.AddToClassList("yoki-editor-inspector__button");
            AddButtonStyle(button, style);
            return button;
        }

        /// <summary>
        /// 创建水平操作区，并均匀放置传入按钮。
        /// </summary>
        /// <param name="buttons">需要排列的按钮。</param>
        /// <returns>按钮行。</returns>
        public static VisualElement CreateButtonRow(params Button[] buttons)
        {
            VisualElement row = new();
            row.AddToClassList("yoki-editor-inspector__button-row");
            if (buttons == null)
                return row;

            for (int index = 0; index < buttons.Length; index++)
            {
                Button button = buttons[index];
                if (button == null)
                    continue;

                button.AddToClassList("yoki-editor-inspector__button--grow");
                row.Add(button);
            }

            return row;
        }

        /// <summary>创建靠左排列且允许换行的紧凑按钮组。</summary>
        /// <param name="buttons">需要排列的按钮。</param>
        /// <returns>不拉伸按钮宽度的紧凑按钮组。</returns>
        public static VisualElement CreateCompactButtonRow(params Button[] buttons)
        {
            VisualElement row = new();
            row.AddToClassList("yoki-editor-inspector__button-group");
            if (buttons == null)
                return row;

            for (var index = 0; index < buttons.Length; index++)
            {
                if (buttons[index] != null)
                    row.Add(buttons[index]);
            }
            return row;
        }

        /// <summary>
        /// 创建带可选标题的语义说明卡片。
        /// </summary>
        /// <param name="title">说明标题；为空时仅显示正文。</param>
        /// <param name="message">说明正文。</param>
        /// <param name="type">语义类型。</param>
        /// <returns>说明卡片。</returns>
        public static VisualElement CreateInfoBox(
            string title,
            string message,
            InspectorInfoBoxType type)
        {
            VisualElement box = CreateInfoBox(message, type);
            if (string.IsNullOrEmpty(title))
                return box;

            Label titleLabel = new(title);
            titleLabel.AddToClassList("yoki-editor-inspector__info-title");
            box.Insert(0, titleLabel);
            return box;
        }

        /// <summary>
        /// 清空并重建动态容器，供条件字段和外部设置切换后局部刷新。
        /// </summary>
        /// <param name="container">待刷新的容器。</param>
        /// <param name="buildContent">重建内容回调。</param>
        public static void Refresh(VisualElement container, Action<VisualElement> buildContent)
        {
            if (container == null)
                return;

            container.Clear();
            buildContent?.Invoke(container);
        }

        /// <summary>
        /// 创建 Inspector 内的标准分隔线。
        /// </summary>
        /// <returns>分隔线元素。</returns>
        public static VisualElement CreateSeparator()
        {
            VisualElement separator = new();
            separator.AddToClassList("yoki-editor-inspector__separator");
            return separator;
        }

        /// <summary>创建卡片头部，并返回控制展开图标的标签。</summary>
        private static VisualElement CreateCardHeader(string title, out Label arrow)
        {
            VisualElement header = new();
            header.AddToClassList("yoki-editor-inspector__card-header");

            arrow = new Label();
            arrow.AddToClassList("yoki-editor-inspector__card-arrow");
            header.Add(arrow);

            Label titleLabel = new(title ?? string.Empty);
            titleLabel.AddToClassList("yoki-editor-inspector__card-title");
            header.Add(titleLabel);
            return header;
        }

        /// <summary>解析卡片首次显示状态，并在提供状态键时读取 EditorPrefs。</summary>
        private static bool ResolveInitialCardState(string stateKey, InspectorCardInitialState initialState)
        {
            bool defaultValue = initialState == InspectorCardInitialState.Expanded;
            return string.IsNullOrEmpty(stateKey)
                ? defaultValue
                : EditorPrefs.GetBool(CARD_STATE_PREFIX + stateKey, defaultValue);
        }

        /// <summary>切换卡片展开状态，并在提供状态键时保存结果。</summary>
        private static void ToggleCard(VisualElement body, Label arrow, string stateKey)
        {
            bool expanded = body.style.display == DisplayStyle.None;
            SetCardExpanded(body, arrow, expanded);
            if (!string.IsNullOrEmpty(stateKey))
                EditorPrefs.SetBool(CARD_STATE_PREFIX + stateKey, expanded);
        }

        /// <summary>同步卡片内容显示状态和箭头文本。</summary>
        private static void SetCardExpanded(VisualElement body, Label arrow, bool expanded)
        {
            body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            arrow.text = expanded ? "v" : ">";
        }

        /// <summary>复制选项集合，保证 DropdownField 拥有稳定的可索引列表。</summary>
        private static List<string> CopyChoices(IReadOnlyList<string> choices)
        {
            List<string> values = new();
            if (choices == null)
                return values;

            for (int index = 0; index < choices.Count; index++)
                values.Add(choices[index] ?? string.Empty);
            return values;
        }

        /// <summary>限制下拉索引，空列表统一返回零。</summary>
        private static int ClampIndex(int index, int count)
        {
            if (count <= 0 || index < 0)
                return 0;
            return index >= count ? count - 1 : index;
        }

        /// <summary>把 DropdownField 文本变化转换为稳定选项索引。</summary>
        private static void NotifyDropdownChanged(
            List<string> choices,
            string selectedValue,
            Action<int> onChanged)
        {
            int index = choices.IndexOf(selectedValue);
            if (index >= 0)
                onChanged?.Invoke(index);
        }

        /// <summary>根据语义为按钮追加单一修饰 class。</summary>
        private static void AddButtonStyle(Button button, InspectorActionStyle style)
        {
            if (style == InspectorActionStyle.Default)
                return;

            button.AddToClassList(
                "yoki-editor-inspector__button--" + style.ToString().ToLowerInvariant());
        }
    }

    /// <summary>卡片首次显示时的展开状态。</summary>
    public enum InspectorCardInitialState
    {
        /// <summary>首次显示时展开。</summary>
        Expanded,
        /// <summary>首次显示时折叠。</summary>
        Collapsed
    }

    /// <summary>Inspector 操作按钮的语义样式。</summary>
    public enum InspectorActionStyle
    {
        /// <summary>普通次要操作。</summary>
        Default,
        /// <summary>主要操作。</summary>
        Primary,
        /// <summary>成功或执行操作。</summary>
        Success,
        /// <summary>需要留意的操作。</summary>
        Warning,
        /// <summary>破坏性操作。</summary>
        Danger
    }

}
#endif
