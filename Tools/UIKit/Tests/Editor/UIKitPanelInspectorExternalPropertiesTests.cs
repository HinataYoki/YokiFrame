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
        /// <summary>验证派生字段的 Inspector 使用局部 IMGUI 容器，而不是 UI Toolkit PropertyField。</summary>
        [Test]
        public void CreateInspectorGUI_派生字段_使用局部IMGUI容器()
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

        /// <summary>验证字段收集保留派生业务字段，并排除 UIPanel 框架字段和生成数据字段。</summary>
        [Test]
        public void CollectCustomPropertyPaths_派生面板_只保留业务字段()
        {
            GameObject gameObject = new("ExternalPropertiesFilterPanel", typeof(RectTransform));
            try
            {
                ExternalPropertiesPanel panel = gameObject.AddComponent<ExternalPropertiesPanel>();
                List<string> paths = new();
                UIKitPanelInspector.CollectCustomPropertyPaths(
                    new SerializedObject(panel),
                    panel.GetType(),
                    paths);

                CollectionAssert.Contains(paths, "Panel");
                CollectionAssert.Contains(paths, "WayPointList");
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

        /// <summary>验证兼容层按特性类型名解析标题、标签、提示、信息框、只读和列表展开。</summary>
        [Test]
        public void ResolveAttributeTitle_按类型名标注_解析标题标签提示只读和展开()
        {
            FieldInfo field = GetFixtureField(nameof(ExternalPropertiesPanel.WayPointList));
            FieldInfo readOnlyField = GetFixtureField(nameof(ExternalPropertiesPanel.ReadOnlyStateField));
            Assert.IsNotNull(field);
            Assert.IsNotNull(readOnlyField);

            Assert.AreEqual("Inspector 兼容测试", UIKitPanelInspector.ResolveAttributeTitle(field));
            Assert.AreEqual("路径点大小", UIKitPanelInspector.ResolveAttributeLabel(field));
            Assert.AreEqual("路径点列表提示", UIKitPanelInspector.ResolveAttributeTooltip(field));
            Assert.AreEqual("字段说明", UIKitPanelInspector.ResolveAttributeInfoBoxText(field));
            Assert.AreEqual(MessageType.Info, UIKitPanelInspector.ResolveAttributeInfoBoxType(field));
            Assert.IsTrue(UIKitPanelInspector.HasReadOnlyAttribute(readOnlyField));
            Assert.IsTrue(UIKitPanelInspector.ShouldForceExpandCustomProperty(field));
        }

        /// <summary>验证包含空对象的目标集合不会被当作可创建 SerializedObject 的有效目标。</summary>
        [Test]
        public void HasValidTargets_包含空目标_返回假()
        {
            Assert.IsFalse(UIKitPanelInspector.HasValidTargets(new UnityEngine.Object[] { null }));
        }

        /// <summary>验证兼容层只收集无参数按钮，解析按钮标签，并在执行后增加可观察调用次数。</summary>
        [Test]
        public void CollectCustomButtonMethods_无参数按钮_收集标签并执行()
        {
            GameObject gameObject = new("ExternalPropertiesButtonPanel", typeof(RectTransform));
            UnityEditor.Editor editor = default;
            try
            {
                ExternalPropertiesPanel panel = gameObject.AddComponent<ExternalPropertiesPanel>();
                List<MethodInfo> methods = new();
                UIKitPanelInspector.CollectCustomButtonMethods(panel.GetType(), methods);

                Assert.AreEqual(1, methods.Count);
                Assert.AreEqual("RunCompatibilityButton", methods[0].Name);
                Assert.AreEqual("执行兼容按钮", UIKitPanelInspector.ResolveButtonLabel(methods[0]));

                editor = UnityEditor.Editor.CreateEditor(panel);
                var inspector = editor as UIKitPanelInspector;
                Assert.IsNotNull(inspector);
                inspector.InvokeCustomButton(methods[0]);

                Assert.AreEqual(1, panel.ButtonInvocationCount);
            }
            finally
            {
                if (editor != default)
                    UnityEngine.Object.DestroyImmediate(editor);
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
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

        /// <summary>
        /// 读取测试夹具的公开字段元数据。
        /// 特性解析 API 需要 FieldInfo，这里只读取本夹具的公开字段，不反射生产类型的私有成员。
        /// </summary>
        /// <param name="fieldName">夹具公开字段名。</param>
        /// <returns>公开字段元数据；字段不存在时返回空。</returns>
        private static FieldInfo GetFixtureField(string fieldName)
        {
            return typeof(ExternalPropertiesPanel).GetField(fieldName);
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
            public List<Vector2> WayPointList = new()
            {
                new Vector2(-80f, -40f),
                new Vector2(0f, 120f),
                new Vector2(80f, -40f),
            };

            [SerializeField, ReadOnly]
            public string ReadOnlyStateField = "只读";

            [SerializeField]
            private ExternalPropertiesData mData;

            private int mButtonInvocationCount;
#pragma warning restore 0649

            /// <summary>读取测试只读字段，避免测试夹具产生无意义的未使用警告。</summary>
            internal string ReadOnlyState => ReadOnlyStateField;

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
