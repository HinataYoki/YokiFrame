using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.Specs
{
    [TestFixture]
    public sealed class UIPanelInspectorSpec
    {
        [Test]
        public void CustomPropertiesFallbackUsesGenericLabelTextAttribute()
        {
            var field = typeof(LabelCompatibilityPanel).GetField(
                "mWayPointList",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var resolveLabelMethod = typeof(UIPanelInspector).GetMethod(
                "ResolveAttributeLabel",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(field);
            Assert.IsNotNull(resolveLabelMethod);
            Assert.AreEqual("路径点大小", resolveLabelMethod.Invoke(null, new object[] { field }));
        }

        [Test]
        public void CustomPropertiesResolveInspectorDecoratorAttributesByName()
        {
            var panelType = typeof(GeneratedBindingCompatibilityPanel);

            var wayPointField = panelType.GetField(
                "mWayPointList",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var readOnlyField = panelType.GetField(
                "mReadOnlyState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var resolveTitleMethod = typeof(UIPanelInspector).GetMethod(
                "ResolveAttributeTitle",
                BindingFlags.Static | BindingFlags.NonPublic);
            var resolveInfoBoxTextMethod = typeof(UIPanelInspector).GetMethod(
                "ResolveAttributeInfoBoxText",
                BindingFlags.Static | BindingFlags.NonPublic);
            var resolveTooltipMethod = typeof(UIPanelInspector).GetMethod(
                "ResolveAttributeTooltip",
                BindingFlags.Static | BindingFlags.NonPublic);
            var isReadOnlyMethod = typeof(UIPanelInspector).GetMethod(
                "HasReadOnlyAttribute",
                BindingFlags.Static | BindingFlags.NonPublic);
            var shouldForceExpandMethod = typeof(UIPanelInspector).GetMethod(
                "ShouldForceExpandCustomProperty",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(wayPointField);
            Assert.IsNotNull(readOnlyField);
            Assert.IsNotNull(resolveTitleMethod);
            Assert.IsNotNull(resolveInfoBoxTextMethod);
            Assert.IsNotNull(resolveTooltipMethod);
            Assert.IsNotNull(isReadOnlyMethod);
            Assert.IsNotNull(shouldForceExpandMethod);

            Assert.AreEqual("Inspector 兼容测试", resolveTitleMethod.Invoke(null, new object[] { wayPointField }));
            StringAssert.Contains(
                "测试内置面板",
                resolveInfoBoxTextMethod.Invoke(null, new object[] { wayPointField }) as string);
            StringAssert.Contains(
                "路径点列表",
                resolveTooltipMethod.Invoke(null, new object[] { wayPointField }) as string);
            Assert.IsTrue((bool)isReadOnlyMethod.Invoke(null, new object[] { readOnlyField }));
            Assert.IsTrue((bool)shouldForceExpandMethod.Invoke(null, new object[] { wayPointField }));
        }

        [Test]
        public void CustomPropertiesRenderDerivedFieldsWithUnityPropertyDrawerPipeline()
        {
            var gameObject = new GameObject("UIPanelInspectorSpecExternal", typeof(RectTransform));

            try
            {
                var panel = gameObject.AddComponent<ExternalInspectorCompatibilityPanel>();
                var editor = Editor.CreateEditor(panel, typeof(UIPanelInspector));

                var root = editor.CreateInspectorGUI();
                var customSection = root.Q<VisualElement>(className: "uipanel-section-custom");

                Assert.IsNotNull(customSection, DumpVisualTree(root));
                Assert.IsNotNull(customSection.Q<IMGUIContainer>(className: "uipanel-custom-imgui"), DumpVisualTree(customSection));
                Assert.IsNull(customSection.Q<VisualElement>(className: "uipanel-tri-custom-container"), DumpVisualTree(customSection));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void CustomPropertiesKeepGeneratedPanelFields()
        {
            var panelType = typeof(GeneratedBindingCompatibilityPanel);

            var gameObject = new GameObject("UIPanelInspectorSpecGeneratedPanel", typeof(RectTransform));

            try
            {
                var panel = gameObject.AddComponent(panelType);
                var collectMethod = typeof(UIPanelInspector).GetMethod(
                    "CollectCustomPropertyPaths",
                    BindingFlags.Static | BindingFlags.NonPublic);
                var propertyPaths = new List<string>();

                Assert.IsNotNull(collectMethod);
                collectMethod.Invoke(null, new object[] { new SerializedObject(panel), panelType, propertyPaths });

                CollectionAssert.Contains(propertyPaths, "Panel");
                CollectionAssert.Contains(propertyPaths, "mWayPointList");
                CollectionAssert.DoesNotContain(propertyPaths, "mShowAnimationConfig");
                CollectionAssert.DoesNotContain(propertyPaths, "mHideAnimationConfig");
                CollectionAssert.DoesNotContain(propertyPaths, "mAutoFocusOnShow");
                CollectionAssert.DoesNotContain(propertyPaths, "mDefaultSelectable");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ValidTargetsRejectNullTargetEntries()
        {
            var hasValidTargetsMethod = typeof(UIPanelInspector).GetMethod(
                "HasValidTargets",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(hasValidTargetsMethod);
            Assert.IsFalse((bool)hasValidTargetsMethod.Invoke(null, new object[]
            {
                new UnityEngine.Object[] { null }
            }));
        }

        [Test]
        public void CustomMemberFilterKeepsOnlySerializedPanelFields()
        {
            var panelType = typeof(GeneratedBindingCompatibilityPanel);

            var shouldShowMethod = typeof(UIPanelInspector).GetMethod(
                "ShouldShowCustomMember",
                BindingFlags.Static | BindingFlags.NonPublic);
            var wayPointField = panelType.GetField(
                "mWayPointList",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var buttonMethod = panelType.GetMethod(
                "LogInspectorPreview",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var boundPanelProperty = panelType.GetProperty(
                "BoundPanelName",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var frameworkField = typeof(UIPanel).GetField(
                "mShowAnimationConfig",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(shouldShowMethod);
            Assert.IsNotNull(wayPointField);
            Assert.IsNotNull(buttonMethod);
            Assert.IsNotNull(boundPanelProperty);
            Assert.IsNotNull(frameworkField);
            Assert.IsTrue((bool)shouldShowMethod.Invoke(null, new object[] { wayPointField, panelType }));
            Assert.IsFalse((bool)shouldShowMethod.Invoke(null, new object[] { buttonMethod, panelType }));
            Assert.IsFalse((bool)shouldShowMethod.Invoke(null, new object[] { boundPanelProperty, panelType }));
            Assert.IsFalse((bool)shouldShowMethod.Invoke(null, new object[] { frameworkField, panelType }));
        }

        [Test]
        public void GeneratedPanelInspectorShowsGeneratedBindingField()
        {
            var gameObject = new GameObject("UIPanelInspectorSpecGeneratedBinding", typeof(RectTransform));

            Editor editor = null;
            try
            {
                var panel = gameObject.AddComponent<GeneratedBindingCompatibilityPanel>();
                editor = Editor.CreateEditor(panel, typeof(UIPanelInspector));
                var root = editor.CreateInspectorGUI();
                var customSection = root.Q<VisualElement>(className: "uipanel-section-custom");

                Assert.IsNotNull(customSection, DumpVisualTree(root));
                Assert.IsNotNull(customSection.Q<IMGUIContainer>(className: "uipanel-custom-imgui"), DumpVisualTree(customSection));
                Assert.IsNull(customSection.Q<VisualElement>(className: "uipanel-tri-custom-container"), DumpVisualTree(customSection));
            }
            finally
            {
                if (editor != null)
                    UnityEngine.Object.DestroyImmediate(editor);
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static string DumpVisualTree(VisualElement root)
        {
            if (root == null)
                return "<null>";

            var lines = new List<string>();
            DumpVisualTreeRecursive(root, 0, lines);
            return string.Join("\n", lines.ToArray());
        }

        private static void DumpVisualTreeRecursive(VisualElement element, int depth, List<string> lines)
        {
            if (element == null)
                return;

            lines.Add(new string(' ', depth * 2) + element.GetType().Name + " name=" + element.name);
            for (var i = 0; i < element.childCount; i++)
                DumpVisualTreeRecursive(element[i], depth + 1, lines);
        }

        private sealed class LabelCompatibilityPanel : UIPanel
        {
#pragma warning disable 0649
            [SerializeField, LabelText("路径点大小")] private List<Vector2> mWayPointList;
#pragma warning restore 0649
        }

        private sealed class ExternalInspectorCompatibilityPanel : UIPanel
        {
#pragma warning disable 0649
            [SerializeField, LabelText("路径点大小")] private List<Vector2> mWayPointList;
#pragma warning restore 0649
        }

        private sealed class GeneratedBindingCompatibilityData : IUIData
        {
        }

        private sealed class GeneratedBindingCompatibilityPanel : UIPanel
        {
#pragma warning disable 0649
            public GameObject Panel;

            [SerializeField]
            [Title("Inspector 兼容测试")]
            [InfoBox("测试内置面板：用于验证 UIPanel 自定义 Inspector 的其他属性区域。")]
            [LabelText("路径点大小")]
            [PropertyTooltip("路径点列表：用于验证第三方属性标签、提示和列表展开兼容。")]
            [ListDrawerSettings(AlwaysExpanded = true)]
            private List<Vector2> mWayPointList = new List<Vector2>
            {
                new Vector2(-120f, 0f),
                new Vector2(0f, 80f),
                new Vector2(120f, 0f)
            };

            [SerializeField, LabelText("显示标题")]
            private string mPreviewTitle = "Generated Binding Fixture";

            [SerializeField, ReadOnly, LabelText("只读状态")]
            private string mReadOnlyState = "Fixture 只读字段";

            [SerializeField]
            private GeneratedBindingCompatibilityData mData;
#pragma warning restore 0649

            private string BoundPanelName
            {
                get { return Panel != null ? Panel.name : "Panel 未绑定"; }
            }

            private void LogInspectorPreview()
            {
            }
        }
    }

    public sealed class LabelTextAttribute : Attribute
    {
        public LabelTextAttribute(string text)
        {
            Text = text;
        }

        public string Text { get; }
    }

    public sealed class TitleAttribute : Attribute
    {
        public TitleAttribute(string title)
        {
            Title = title;
        }

        public string Title { get; }
        public bool HorizontalLine { get; set; } = true;
    }

    public sealed class InfoBoxAttribute : Attribute
    {
        public InfoBoxAttribute(string text)
        {
            Text = text;
        }

        public string Text { get; }
    }

    public sealed class PropertyTooltipAttribute : Attribute
    {
        public PropertyTooltipAttribute(string tooltip)
        {
            Tooltip = tooltip;
        }

        public string Tooltip { get; }
    }

    public sealed class ReadOnlyAttribute : Attribute
    {
    }

    public sealed class ListDrawerSettingsAttribute : Attribute
    {
        public bool AlwaysExpanded { get; set; }
    }
}
