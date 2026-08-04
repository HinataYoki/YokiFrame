#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.Tests
{
    /// <summary>验证 UIPanel 其它属性区域保留第三方 IMGUI 绘制管线。</summary>
    public sealed class UIKitPanelInspectorExternalPropertiesTests
    {
        /// <summary>验证其它属性卡片使用局部 IMGUI 容器而不是 UI Toolkit PropertyField。</summary>
        [Test]
        public void OtherPropertiesUseIMGUIBridge()
        {
            GameObject gameObject = new("ExternalPropertiesPanel", typeof(RectTransform));
            UnityEditor.Editor editor = default;
            try
            {
                ExternalPropertiesPanel panel = gameObject.AddComponent<ExternalPropertiesPanel>();
                editor = UnityEditor.Editor.CreateEditor(panel);
                VisualElement root = editor.CreateInspectorGUI();
                IMGUIContainer container = FindElement<IMGUIContainer>(root);

                Assert.IsNotNull(container);
                Assert.IsTrue(container.ClassListContains("uipanel-custom-imgui"));
            }
            finally
            {
                if (editor != default)
                    UnityEngine.Object.DestroyImmediate(editor);
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        /// <summary>验证字段收集保留生成绑定字段和业务字段，并过滤 UIPanel 内部字段。</summary>
        [Test]
        public void CustomPropertyPathsKeepDerivedFieldsOnly()
        {
            GameObject gameObject = new("ExternalPropertiesFilterPanel", typeof(RectTransform));
            try
            {
                ExternalPropertiesPanel panel = gameObject.AddComponent<ExternalPropertiesPanel>();
                List<string> paths = new();
                Type inspectorType = GetInspectorType();
                MethodInfo collect = inspectorType == null
                    ? default
                    : inspectorType.GetMethod(
                        "CollectCustomPropertyPaths",
                        BindingFlags.Static | BindingFlags.NonPublic);

                Assert.IsNotNull(collect);
                collect.Invoke(null, new object[] { new SerializedObject(panel), panel.GetType(), paths });

                CollectionAssert.Contains(paths, "Panel");
                CollectionAssert.Contains(paths, "mWayPointList");
                CollectionAssert.DoesNotContain(paths, "mShowAnimationConfig");
                CollectionAssert.DoesNotContain(paths, "mHideAnimationConfig");
                CollectionAssert.DoesNotContain(paths, "mAutoFocusOnShow");
                CollectionAssert.DoesNotContain(paths, "mDefaultSelectable");
                CollectionAssert.DoesNotContain(paths, "mData");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        /// <summary>验证兼容层按属性类型名读取标题、标签、提示、只读和列表展开配置。</summary>
        [Test]
        public void ExternalAttributesResolveByTypeName()
        {
            FieldInfo field = typeof(ExternalPropertiesPanel).GetField(
                "mWayPointList",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Type inspectorType = GetInspectorType();
            Assert.IsNotNull(field);
            Assert.IsNotNull(inspectorType);

            MethodInfo resolveTitle = inspectorType.GetMethod(
                "ResolveAttributeTitle",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo resolveLabel = inspectorType.GetMethod(
                "ResolveAttributeLabel",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo resolveTooltip = inspectorType.GetMethod(
                "ResolveAttributeTooltip",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo resolveInfoBoxText = inspectorType.GetMethod(
                "ResolveAttributeInfoBoxText",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo resolveInfoBoxType = inspectorType.GetMethod(
                "ResolveAttributeInfoBoxType",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo readOnly = inspectorType.GetMethod(
                "HasReadOnlyAttribute",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo alwaysExpanded = inspectorType.GetMethod(
                "ShouldForceExpandCustomProperty",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.AreEqual("Inspector 兼容测试", resolveTitle.Invoke(null, new object[] { field }));
            Assert.AreEqual("路径点大小", resolveLabel.Invoke(null, new object[] { field }));
            Assert.AreEqual("路径点列表提示", resolveTooltip.Invoke(null, new object[] { field }));
            Assert.AreEqual("字段说明", resolveInfoBoxText.Invoke(null, new object[] { field }));
            Assert.AreEqual(MessageType.Info, resolveInfoBoxType.Invoke(null, new object[] { field }));
            Assert.IsTrue((bool)readOnly.Invoke(null, new object[] { typeof(ExternalPropertiesPanel).GetField(
                "mReadOnlyState", BindingFlags.Instance | BindingFlags.NonPublic) }));
            Assert.IsTrue((bool)alwaysExpanded.Invoke(null, new object[] { field }));
        }

        /// <summary>验证 Unity 刷新期间的空目标不会进入 SerializedObject 创建流程。</summary>
        [Test]
        public void InvalidTargetsAreRejected()
        {
            Type inspectorType = GetInspectorType();
            MethodInfo hasValidTargets = inspectorType == null
                ? default
                : inspectorType.GetMethod("HasValidTargets", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(hasValidTargets);
            Assert.IsFalse((bool)hasValidTargets.Invoke(null, new object[] { new UnityEngine.Object[] { null } }));
        }

        /// <summary>验证兼容层收集无参数按钮、过滤带参数按钮并解析按钮标签。</summary>
        [Test]
        public void CustomButtonsKeepParameterlessMethodsOnly()
        {
            GameObject gameObject = new("ExternalPropertiesButtonPanel", typeof(RectTransform));
            UnityEditor.Editor editor = default;
            try
            {
                ExternalPropertiesPanel panel = gameObject.AddComponent<ExternalPropertiesPanel>();
                Type inspectorType = GetInspectorType();
                MethodInfo collect = inspectorType == null
                    ? default
                    : inspectorType.GetMethod(
                        "CollectCustomButtonMethods",
                        BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo resolveLabel = inspectorType == null
                    ? default
                    : inspectorType.GetMethod(
                        "ResolveButtonLabel",
                        BindingFlags.Static | BindingFlags.NonPublic);

                Assert.IsNotNull(collect);
                Assert.IsNotNull(resolveLabel);
                List<MethodInfo> methods = new();
                collect.Invoke(null, new object[] { panel.GetType(), methods });

                Assert.AreEqual(1, methods.Count);
                Assert.AreEqual("RunCompatibilityButton", methods[0].Name);
                Assert.AreEqual("执行兼容按钮", resolveLabel.Invoke(null, new object[] { methods[0] }));

                editor = UnityEditor.Editor.CreateEditor(panel);
                MethodInfo invoke = inspectorType.GetMethod(
                    "InvokeCustomButton",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(invoke);
                invoke.Invoke(editor, new object[] { methods[0] });

                Assert.AreEqual(1, panel.ButtonInvocationCount);
            }
            finally
            {
                if (editor != default)
                    UnityEngine.Object.DestroyImmediate(editor);
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        /// <summary>从当前已加载的 Unity Editor 程序集中定位 UIPanel Inspector 类型。</summary>
        /// <returns>Inspector 类型；编辑器程序集尚未加载时返回空。</returns>
        private static Type GetInspectorType()
        {
            var editorTypes = TypeCache.GetTypesDerivedFrom<UnityEditor.Editor>();
            for (var index = 0; index < editorTypes.Count; index++)
            {
                Type type = editorTypes[index];
                if (type != null)
                {
                    if (type.FullName == "YokiFrame.UIKitPanelInspector")
                        return type;
                }
            }

            return default;
        }

        /// <summary>递归查找指定类型的视觉元素。</summary>
        /// <typeparam name="T">目标视觉元素类型。</typeparam>
        /// <param name="root">搜索根节点。</param>
        /// <returns>找到的第一个元素；不存在时返回空。</returns>
        private static T FindElement<T>(VisualElement root)
            where T : VisualElement
        {
            if (root is T matched)
                return matched;
            for (var index = 0; index < root.childCount; index++)
            {
                T childMatch = FindElement<T>(root[index]);
                if (childMatch != null)
                    return childMatch;
            }

            return default;
        }

        /// <summary>提供带第三方属性名的最小 UIPanel 测试类型。</summary>
        private sealed class ExternalPropertiesPanel : UIPanel
        {
#pragma warning disable 0649
            public GameObject Panel;

            [SerializeField]
            [Title("Inspector 兼容测试")]
            [InfoBox("字段说明")]
            [LabelText("路径点大小")]
            [PropertyTooltip("路径点列表提示")]
            [ListDrawerSettings(AlwaysExpanded = true)]
            private List<Vector2> mWayPointList = new()
            {
                new Vector2(-80f, -40f),
                new Vector2(0f, 120f),
                new Vector2(80f, -40f),
            };

            [SerializeField, ReadOnly]
            private string mReadOnlyState = "只读";

            [SerializeField]
            private ExternalPropertiesData mData;

            private int mButtonInvocationCount;
#pragma warning restore 0649

            /// <summary>读取测试只读字段，避免测试夹具产生无意义的未使用警告。</summary>
            internal string ReadOnlyState => mReadOnlyState;

            /// <summary>获取兼容按钮被调用的次数。</summary>
            internal int ButtonInvocationCount => mButtonInvocationCount;

            /// <summary>提供一个带 TriInspector/Odin 常见命名参数的兼容按钮。</summary>
            [Button("执行兼容按钮")]
            private void RunCompatibilityButton()
            {
                mButtonInvocationCount++;
            }

            /// <summary>提供一个带参数的按钮，验证兼容层不会尝试猜测参数值。</summary>
            [Button("应被过滤")]
            private void ParameterizedButton(int value)
            {
                mButtonInvocationCount += value;
            }
        }

        /// <summary>用于验证生成数据字段过滤的 IUIData 实现。</summary>
        [Serializable]
        private sealed class ExternalPropertiesData : IUIData
        {
        }
    }

    /// <summary>模拟 TriInspector/Odin 常见标题属性，不引入第三方程序集引用。</summary>
    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class TitleAttribute : Attribute
    {
        /// <summary>创建标题属性。</summary>
        /// <param name="title">标题文本。</param>
        public TitleAttribute(string title)
        {
            Title = title;
        }

        /// <summary>获取标题文本。</summary>
        public string Title { get; }
        /// <summary>获取是否绘制分隔线。</summary>
        public bool HorizontalLine { get; set; } = true;
    }

    /// <summary>模拟常见信息框属性。</summary>
    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class InfoBoxAttribute : Attribute
    {
        /// <summary>创建信息框属性。</summary>
        /// <param name="text">提示文本。</param>
        public InfoBoxAttribute(string text)
        {
            Text = text;
        }

        /// <summary>获取提示文本。</summary>
        public string Text { get; }
    }

    /// <summary>模拟常见字段标签属性。</summary>
    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class LabelTextAttribute : Attribute
    {
        /// <summary>创建字段标签属性。</summary>
        /// <param name="text">字段标签。</param>
        public LabelTextAttribute(string text)
        {
            Text = text;
        }

        /// <summary>获取字段标签。</summary>
        public string Text { get; }
    }

    /// <summary>模拟常见字段提示属性。</summary>
    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class PropertyTooltipAttribute : Attribute
    {
        /// <summary>创建字段提示属性。</summary>
        /// <param name="tooltip">提示文本。</param>
        public PropertyTooltipAttribute(string tooltip)
        {
            Tooltip = tooltip;
        }

        /// <summary>获取提示文本。</summary>
        public string Tooltip { get; }
    }

    /// <summary>模拟常见只读属性。</summary>
    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class ReadOnlyAttribute : Attribute
    {
    }

    /// <summary>模拟常见列表绘制配置属性。</summary>
    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class ListDrawerSettingsAttribute : Attribute
    {
        /// <summary>获取始终展开设置。</summary>
        public bool AlwaysExpanded { get; set; }
    }

    /// <summary>模拟 TriInspector/Odin 的方法按钮属性，避免测试程序集引入第三方依赖。</summary>
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class ButtonAttribute : Attribute
    {
        /// <summary>创建无自定义文本的按钮属性。</summary>
        public ButtonAttribute()
        {
        }

        /// <summary>创建带自定义按钮文本的属性。</summary>
        /// <param name="name">按钮显示文本。</param>
        public ButtonAttribute(string name)
        {
            Name = name;
        }

        /// <summary>获取按钮显示文本。</summary>
        public string Name { get; }
    }
}
#endif
