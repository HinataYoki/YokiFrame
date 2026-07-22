#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace YokiFrame.Unity.Inspector
{
    /// <summary>InspectorKit 的布尔交互与嵌套折叠组件。</summary>
    public static partial class InspectorKitUi
    {
        private const string FOLDOUT_STATE_PREFIX = "YokiFrame.InspectorKit.Foldout.";

        /// <summary>创建滑块式布尔 SerializedProperty 字段行，保留旧入口名称。</summary>
        /// <param name="property">待绑定的布尔序列化属性。</param>
        /// <param name="label">字段标签。</param>
        /// <returns>可直接加入 Inspector 视觉树的字段行。</returns>
        public static VisualElement CreateToggleRow(SerializedProperty property, string label)
        {
            return CreateSwitchRow(property, label);
        }

        /// <summary>创建滑块式布尔 SerializedProperty 字段行。</summary>
        /// <param name="property">待绑定的布尔序列化属性。</param>
        /// <param name="label">字段标签。</param>
        /// <returns>可直接加入 Inspector 视觉树的字段行。</returns>
        public static VisualElement CreateSwitchRow(SerializedProperty property, string label)
        {
            if (property == null)
                return CreateInfoBox("未找到布尔序列化字段。", InspectorInfoBoxType.Error);

            InspectorSwitchField field = new(property.boolValue, value =>
            {
                property.boolValue = value;
                property.serializedObject.ApplyModifiedProperties();
            });
            field.TrackPropertyValue(property, changed => field.SetValue(changed.boolValue, false));
            return CreateFieldRow(label, field);
        }

        /// <summary>创建由普通回调同步的滑块式布尔字段行。</summary>
        /// <param name="label">字段标签。</param>
        /// <param name="value">开关初始值。</param>
        /// <param name="onChanged">开关值变化后的同步回调。</param>
        /// <returns>可直接加入 Inspector 视觉树的字段行。</returns>
        public static VisualElement CreateSwitchRow(string label, bool value, Action<bool> onChanged)
        {
            return CreateFieldRow(label, new InspectorSwitchField(value, onChanged));
        }

        /// <summary>创建可嵌套并可持久化状态的轻量折叠区。</summary>
        /// <param name="title">折叠区标题。</param>
        /// <param name="stateKey">EditorPrefs 状态键；为空时不持久化。</param>
        /// <param name="initialState">首次显示时的展开状态。</param>
        /// <param name="buildContent">向折叠内容区添加控件的回调。</param>
        /// <returns>可直接加入 Inspector 视觉树的折叠区。</returns>
        public static VisualElement CreateFoldoutSection(
            string title,
            string stateKey,
            InspectorCardInitialState initialState,
            Action<VisualElement> buildContent)
        {
            bool expanded = ResolveFoldoutState(stateKey, initialState);
            VisualElement root = new();
            root.AddToClassList("yoki-editor-inspector__foldout");
            VisualElement header = CreateFoldoutHeader(title, out Label arrow);
            VisualElement body = new();
            body.AddToClassList("yoki-editor-inspector__foldout-body");
            buildContent?.Invoke(body);
            SetFoldoutExpanded(body, arrow, expanded);
            header.RegisterCallback<ClickEvent>(_ => ToggleFoldout(body, arrow, stateKey));
            root.Add(header);
            root.Add(body);
            return root;
        }

        /// <summary>创建折叠区标题、箭头和文本节点。</summary>
        private static VisualElement CreateFoldoutHeader(string title, out Label arrow)
        {
            VisualElement header = new();
            header.AddToClassList("yoki-editor-inspector__foldout-header");
            arrow = new Label();
            arrow.AddToClassList("yoki-editor-inspector__foldout-arrow");
            header.Add(arrow);
            Label titleLabel = new(title ?? string.Empty);
            titleLabel.AddToClassList("yoki-editor-inspector__foldout-title");
            header.Add(titleLabel);
            return header;
        }

        /// <summary>解析嵌套折叠区首次显示状态。</summary>
        private static bool ResolveFoldoutState(string stateKey, InspectorCardInitialState initialState)
        {
            bool defaultValue = initialState == InspectorCardInitialState.Expanded;
            return string.IsNullOrEmpty(stateKey)
                ? defaultValue
                : EditorPrefs.GetBool(FOLDOUT_STATE_PREFIX + stateKey, defaultValue);
        }

        /// <summary>切换并持久化嵌套折叠区状态。</summary>
        private static void ToggleFoldout(VisualElement body, Label arrow, string stateKey)
        {
            bool expanded = body.style.display == DisplayStyle.None;
            SetFoldoutExpanded(body, arrow, expanded);
            if (!string.IsNullOrEmpty(stateKey))
                EditorPrefs.SetBool(FOLDOUT_STATE_PREFIX + stateKey, expanded);
        }

        /// <summary>同步嵌套折叠区内容和箭头显示状态。</summary>
        private static void SetFoldoutExpanded(VisualElement body, Label arrow, bool expanded)
        {
            body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            arrow.text = expanded ? "v" : ">";
        }
    }

    /// <summary>InspectorKit 的滑块开关控件。</summary>
    internal sealed class InspectorSwitchField : VisualElement
    {
        private readonly Action<bool> mOnChanged;
        private bool mValue;

        /// <summary>创建滑块开关并设置初始值。</summary>
        internal InspectorSwitchField(bool value, Action<bool> onChanged)
        {
            mOnChanged = onChanged;
            focusable = true;
            tabIndex = 0;
            AddToClassList("yoki-editor-inspector__switch");
            VisualElement track = new();
            track.AddToClassList("yoki-editor-inspector__switch-track");
            VisualElement thumb = new();
            thumb.AddToClassList("yoki-editor-inspector__switch-thumb");
            track.Add(thumb);
            Add(track);
            RegisterCallback<ClickEvent>(_ => SetValue(!mValue, true));
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            SetValue(value, false);
        }

        /// <summary>设置开关值，可选是否触发业务回调。</summary>
        internal void SetValue(bool value, bool notify)
        {
            mValue = value;
            EnableInClassList("yoki-editor-inspector__switch--on", value);
            EnableInClassList("yoki-editor-inspector__switch--off", !value);
            if (notify)
                mOnChanged?.Invoke(value);
        }

        /// <summary>处理键盘空格和回车，保证开关可键盘操作。</summary>
        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != UnityEngine.KeyCode.Space && evt.keyCode != UnityEngine.KeyCode.Return)
                return;

            SetValue(!mValue, true);
            evt.StopPropagation();
        }
    }
}
#endif
