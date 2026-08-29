#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UiButton = UnityEngine.UIElements.Button;
using UnityButton = UnityEngine.UI.Button;
using UnityInspectorEditor = UnityEditor.Editor;

namespace YokiFrame.Tests
{
    /// <summary>固定 UIKit 自定义 Inspector 的 InspectorKit 结构与关键用户入口。</summary>
    public sealed class UIKitInspectorPresentationTests
    {
        private GameObject mPanelObject;
        private GameObject mBoundObject;

        /// <summary>为每个测试创建包含一个 Member Bind 的最小面板层级。</summary>
        [SetUp]
        public void SetUp()
        {
            mPanelObject = new GameObject("InspectorPanel", typeof(RectTransform), typeof(UIKitInspectorTestPanel));
            mBoundObject = new GameObject("ConfirmButton", typeof(RectTransform), typeof(UnityButton), typeof(Bind));
            mBoundObject.transform.SetParent(mPanelObject.transform, false);
            Bind bind = mBoundObject.GetComponent<Bind>();
            bind.Name = "ConfirmButton";
            bind.Target = mBoundObject.GetComponent<UnityButton>();
            bind.AutoType = typeof(UnityButton).FullName;
            bind.Type = bind.AutoType;
        }

        /// <summary>销毁测试创建的 Unity 对象，避免污染后续 Inspector 测试。</summary>
        [TearDown]
        public void TearDown()
        {
            if (mPanelObject != default)
                Object.DestroyImmediate(mPanelObject);
            mPanelObject = default;
            mBoundObject = default;
        }

        /// <summary>验证 UIPanel Inspector 使用 InspectorKit 并恢复设置、绑定树和生成入口。</summary>
        [Test]
        public void PanelInspectorRestoresInspectorKitSectionsAndBindingTree()
        {
            Bind bind = mBoundObject.GetComponent<Bind>();
            bind.MemberTargets.Add(new BindMemberTarget
            {
                Target = mBoundObject.GetComponent<UnityButton>(),
                Name = "ConfirmButton",
            });
            bind.MemberTargets.Add(new BindMemberTarget
            {
                Target = mBoundObject.GetComponent<RectTransform>(),
                Name = "ConfirmButtonRectTransform",
            });
            UnityInspectorEditor editor = UnityInspectorEditor.CreateEditor(
                mPanelObject.GetComponent<UIKitInspectorTestPanel>());
            try
            {
                VisualElement root = editor.CreateInspectorGUI();
                Assert.IsNotNull(root);
                Assert.IsTrue(root.ClassListContains("yoki-editor-inspector"));
                AssertVisualText(root, "面板设置");
                AssertVisualText(root, "动画设置");
                AssertVisualText(root, "焦点设置");
                AssertVisualText(root, "绑定树");
                AssertVisualText(root, "ConfirmButton");
                AssertVisualText(root, "ConfirmButtonRectTransform");
                AssertButton(root, "打开脚本");
                AssertButton(root, "刷新绑定树");
                AssertButton(root, "生成 UIPanel 代码");
            }
            finally
            {
                Object.DestroyImmediate(editor);
            }
        }

        /// <summary>验证 UIRoot 使用 InspectorKit 展示稳定层级、加载和缓存配置。</summary>
        [Test]
        public void UIRootInspectorUsesInspectorKitConfigurationCards()
        {
            UIRoot.Dispose();
            UIRoot uiRoot = UIRoot.CreateProceduralFallback();
            UnityInspectorEditor editor = UnityInspectorEditor.CreateEditor(uiRoot);
            try
            {
                VisualElement root = editor.CreateInspectorGUI();
                Assert.IsNotNull(root);
                Assert.IsTrue(root.ClassListContains("yoki-editor-inspector"));
                AssertVisualText(root, "UIKit Root");
                AssertVisualText(root, "唯一运行时根");
                AssertVisualText(root, "Root 概览");
                AssertVisualText(root, "运行时层级");
                AssertVisualText(root, "面板加载");
                AssertVisualText(root, "预制体路径前缀");
                AssertVisualText(root, "使用 Addressable Location");
                AssertVisualText(root, "缓存策略");
                AssertVisualText(root, "Reusable 缓存容量");
                Assert.IsNotNull(FindTextField(root, "UIKit/UIRoot"));
            }
            finally
            {
                Object.DestroyImmediate(editor);
                UIRoot.Dispose();
            }
        }

        /// <summary>验证 Bind Inspector 使用 InspectorKit 并恢复旧版编辑、预览和跳转入口。</summary>
        [Test]
        public void BindInspectorRestoresInspectorKitEditingSurface()
        {
            UnityInspectorEditor editor = UnityInspectorEditor.CreateEditor(mBoundObject.GetComponent<Bind>());
            try
            {
                VisualElement root = editor.CreateInspectorGUI();
                Assert.IsNotNull(root);
                Assert.IsTrue(root.ClassListContains("yoki-editor-inspector"));
                AssertVisualText(root, "绑定类型");
                AssertVisualText(root, "快速转换");
                AssertVisualText(root, "字段名称");
                AssertVisualText(root, "组件列表");
                AssertVisualText(root, "路径");
                AssertVisualText(root, "代码预览");
                Assert.IsFalse(ContainsText(root, "高级设置"));
                Assert.IsFalse(ContainsText(root, "自定义策略 ID"));
                AssertButton(root, "+ 添加组件");
                AssertButton(root, "跳转到代码", "代码未生成");
                Assert.IsNull(FindToggle(root, nameof(RectTransform)));
                Assert.IsNotNull(FindSelectionLabel(root, nameof(UnityEngine.UI.Button)));
                Assert.IsNull(FindSelectionLabel(root, nameof(RectTransform)));
            }
            finally
            {
                Object.DestroyImmediate(editor);
            }
        }

        /// <summary>验证非 C# 标识符的场景根不会中断 Bind Inspector 渲染，只禁用代码跳转入口。</summary>
        [Test]
        public void BindInspectorAllowsNonIdentifierRootName()
        {
            GameObject root = new("Canvas (Environment)", typeof(RectTransform));
            GameObject boundObject = new("ItemsSlot", typeof(RectTransform), typeof(UnityButton), typeof(Bind));
            boundObject.transform.SetParent(root.transform, false);
            Bind bind = boundObject.GetComponent<Bind>();
            bind.Name = "ItemsSlot";
            bind.Target = boundObject.GetComponent<UnityButton>();
            UnityInspectorEditor editor = UnityInspectorEditor.CreateEditor(bind);
            try
            {
                VisualElement visualRoot = editor.CreateInspectorGUI();
                Assert.IsNotNull(visualRoot);
                Assert.IsTrue(visualRoot.ClassListContains("yoki-editor-inspector"));
                AssertVisualText(visualRoot, "绑定类型");
                AssertButton(visualRoot, "代码未生成");
            }
            finally
            {
                Object.DestroyImmediate(editor);
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>验证空 Member 配置打开 Inspector 后默认选择最后一个非 Bind 组件。</summary>
        [Test]
        public void BindInspectorDefaultsToLastNonBindComponent()
        {
            GameObject target = new(
                "DefaultTarget",
                typeof(RectTransform),
                typeof(UnityButton),
                typeof(CanvasGroup),
                typeof(Bind));
            UnityInspectorEditor editor = UnityInspectorEditor.CreateEditor(target.GetComponent<Bind>());
            try
            {
                editor.CreateInspectorGUI();
                Assert.AreSame(target.GetComponent<CanvasGroup>(), target.GetComponent<Bind>().Target);
            }
            finally
            {
                Object.DestroyImmediate(editor);
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>验证按需添加第二个组件会迁移旧目标并生成独立稳定字段名。</summary>
        [Test]
        public void BindInspectorSelectingSecondComponentCreatesMultiMemberTargets()
        {
            Bind bind = mBoundObject.GetComponent<Bind>();
            UnityInspectorEditor editor = UnityInspectorEditor.CreateEditor(bind);
            try
            {
                VisualElement root = editor.CreateInspectorGUI();
                AssertButton(root, "+ 添加组件");
                Assert.IsNull(FindToggle(root, nameof(RectTransform)));
                MethodInfo selectMember = editor.GetType().GetMethod(
                    "SetMemberSelected",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(selectMember);
                selectMember.Invoke(editor, new object[]
                {
                    mBoundObject.GetComponent<RectTransform>(),
                    true,
                });

                SerializedObject serializedBind = new(bind);
                Assert.AreEqual(2, serializedBind.FindProperty("mMemberTargets").arraySize);
                Assert.AreEqual(2, bind.MemberTargets.Count);
                Assert.AreSame(mBoundObject.GetComponent<UnityButton>(), bind.MemberTargets[0].Target);
                Assert.AreEqual("ConfirmButton", bind.MemberTargets[0].Name);
                Assert.AreSame(mBoundObject.GetComponent<RectTransform>(), bind.MemberTargets[1].Target);
                Assert.AreEqual("ConfirmButtonRectTransform", bind.MemberTargets[1].Name);
                Assert.IsNotNull(FindSelectionLabel(root, nameof(RectTransform)));

                selectMember.Invoke(editor, new object[]
                {
                    mBoundObject.GetComponent<UnityButton>(),
                    false,
                });
                Assert.AreEqual(0, bind.MemberTargets.Count);
                Assert.AreSame(mBoundObject.GetComponent<RectTransform>(), bind.Target);
                Assert.AreEqual("ConfirmButtonRectTransform", bind.Name);
                Assert.AreEqual(typeof(RectTransform).FullName, bind.Type);
            }
            finally
            {
                Object.DestroyImmediate(editor);
            }
        }

        /// <summary>验证默认首项名称会提交，编辑独立字段名时不会重建组件列表并打断输入。</summary>
        [Test]
        public void BindInspectorMemberNameEditingKeepsComponentListStable()
        {
            Bind bind = mBoundObject.GetComponent<Bind>();
            bind.MemberTargets.Add(new BindMemberTarget
            {
                Target = mBoundObject.GetComponent<UnityButton>(),
                Name = string.Empty,
            });
            bind.MemberTargets.Add(new BindMemberTarget
            {
                Target = mBoundObject.GetComponent<RectTransform>(),
                Name = "ConfirmButtonRectTransform",
            });
            UnityInspectorEditor editor = UnityInspectorEditor.CreateEditor(bind);
            try
            {
                VisualElement root = editor.CreateInspectorGUI();
                Assert.AreEqual("ConfirmButton", bind.MemberTargets[0].Name);
                TextField before = FindTextField(root, "ConfirmButtonRectTransform");
                Assert.IsNotNull(before);
                MethodInfo writeName = editor.GetType().GetMethod(
                    "WriteMemberName",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(writeName);
                writeName.Invoke(editor, new object[]
                {
                    mBoundObject.GetComponent<RectTransform>(),
                    "ConfirmButtonRect",
                });
                Assert.AreSame(before, FindTextField(root, "ConfirmButtonRectTransform"));
                Assert.AreEqual("ConfirmButtonRect", bind.MemberTargets[1].Name);
            }
            finally
            {
                Object.DestroyImmediate(editor);
            }
        }

        /// <summary>验证 UIElement 使用 InspectorKit 专有 Inspector 和明确的独立生成入口。</summary>
        [Test]
        public void UIElementInspectorProvidesBindingTreeAndOwnGenerationAction()
        {
            AssertGeneratedOwnerInspector(
                typeof(UIKitInspectorTestElement),
                "UIElement 设置",
                "生成 UIElement 代码");
        }

        /// <summary>验证 UIComponent 使用 InspectorKit 专有 Inspector 和明确的独立生成入口。</summary>
        [Test]
        public void UIComponentInspectorProvidesBindingTreeAndOwnGenerationAction()
        {
            AssertGeneratedOwnerInspector(
                typeof(UIKitInspectorTestComponent),
                "UIComponent 设置",
                "生成 UIComponent 代码");
        }

        /// <summary>验证旧的 Assets/Prefab 生成入口和独立 Panel 创建菜单均已移除。</summary>
        [Test]
        public void PrefabContextMenuGenerationEntryIsRemoved()
        {
            const BindingFlags FLAGS = BindingFlags.Static | BindingFlags.NonPublic;
            Assert.IsNull(typeof(UIKitBindShortcuts).GetMethod("GenerateSelectedPrefab", FLAGS));
            Assert.IsNull(typeof(UIKitBindShortcuts).GetMethod("CanGenerateSelectedPrefab", FLAGS));
            Assert.IsNull(typeof(UIKitBindShortcuts).GetMethod("OpenPanelCreator", FLAGS));
        }

        /// <summary>创建指定绑定 owner，并验证 InspectorKit、绑定树和专有生成按钮。</summary>
        private static void AssertGeneratedOwnerInspector(
            System.Type ownerType,
            string settingsTitle,
            string generateLabel)
        {
            GameObject root = new(ownerType.Name, typeof(RectTransform), ownerType);
            GameObject child = new("ConfirmButton", typeof(RectTransform), typeof(UnityButton), typeof(Bind));
            child.transform.SetParent(root.transform, false);
            Bind bind = child.GetComponent<Bind>();
            bind.Name = "ConfirmButton";
            bind.Target = child.GetComponent<UnityButton>();
            UnityInspectorEditor editor = UnityInspectorEditor.CreateEditor(root.GetComponent(ownerType));
            try
            {
                VisualElement visualRoot = editor.CreateInspectorGUI();
                Assert.IsNotNull(visualRoot);
                Assert.IsTrue(visualRoot.ClassListContains("yoki-editor-inspector"));
                AssertVisualText(visualRoot, settingsTitle);
                AssertVisualText(visualRoot, "绑定树");
                AssertVisualText(visualRoot, "ConfirmButton");
                AssertButton(visualRoot, "打开脚本");
                AssertButton(visualRoot, generateLabel);
            }
            finally
            {
                Object.DestroyImmediate(editor);
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>递归检查视觉树是否包含指定文本。</summary>
        private static void AssertVisualText(VisualElement root, string expected)
        {
            Assert.IsTrue(ContainsText(root, expected), "Inspector 缺少文本: " + expected);
        }

        /// <summary>递归检查视觉树是否包含任一指定按钮文本。</summary>
        private static void AssertButton(VisualElement root, params string[] expected)
        {
            Assert.IsTrue(ContainsButton(root, expected), "Inspector 缺少按钮: " + string.Join(" / ", expected));
        }

        /// <summary>深度优先查找 Label、Button 和 TextElement 文本。</summary>
        private static bool ContainsText(VisualElement element, string expected)
        {
            if (element is TextElement textElement && textElement.text == expected)
                return true;
            for (var index = 0; index < element.childCount; index++)
            {
                if (ContainsText(element[index], expected))
                    return true;
            }
            return false;
        }

        /// <summary>深度优先查找匹配任一文本的按钮。</summary>
        private static bool ContainsButton(VisualElement element, string[] expected)
        {
            if (element is UiButton button)
            {
                for (var index = 0; index < expected.Length; index++)
                {
                    if (button.text == expected[index])
                        return true;
                }
            }
            for (var index = 0; index < element.childCount; index++)
            {
                if (ContainsButton(element[index], expected))
                    return true;
            }
            return false;
        }

        /// <summary>递归查找显示指定组件短类型名的兼容复选框。</summary>
        private static Toggle FindToggle(VisualElement element, string label)
        {
            if (element is Toggle toggle && toggle.label == label)
                return toggle;
            for (var index = 0; index < element.childCount; index++)
            {
                Toggle matched = FindToggle(element[index], label);
                if (matched != null)
                    return matched;
            }
            return null;
        }

        /// <summary>递归查找值匹配的文本字段，用于验证列表详情编辑不重建控件。</summary>
        private static TextField FindTextField(VisualElement element, string value)
        {
            if (element is TextField field && field.value == value)
                return field;
            for (var index = 0; index < element.childCount; index++)
            {
                TextField matched = FindTextField(element[index], value);
                if (matched != null)
                    return matched;
            }
            return null;
        }

        /// <summary>递归查找按需选择列表中匹配组件名称的标签。</summary>
        private static Label FindSelectionLabel(VisualElement element, string text)
        {
            if (element is Label label
                && label.ClassListContains("yoki-editor-inspector__selection-list-name")
                && label.text == text)
                return label;
            for (var index = 0; index < element.childCount; index++)
            {
                Label matched = FindSelectionLabel(element[index], text);
                if (matched != null)
                    return matched;
            }
            return null;
        }
    }

    /// <summary>提供派生序列化字段的 Inspector 测试面板。</summary>
    internal sealed class UIKitInspectorTestPanel : UIPanel
    {
        [SerializeField] private string mTitle = "Inspector";

        /// <summary>提供测试派生字段的只读访问，避免字段被编译器视为未读取。</summary>
        internal string Title => mTitle;
    }

    /// <summary>提供 UIElement 专有 Inspector 的具体测试类型。</summary>
    internal sealed partial class UIKitInspectorTestElement : UIElement
    {
        [SerializeField] private string mTitle = "Element";

        /// <summary>读取测试业务字段。</summary>
        internal string Title => mTitle;
    }

    /// <summary>提供 UIComponent 专有 Inspector 的具体测试类型。</summary>
    internal sealed partial class UIKitInspectorTestComponent : UIComponent
    {
        [SerializeField] private string mTitle = "Component";

        /// <summary>读取测试业务字段。</summary>
        internal string Title => mTitle;
    }
}
#endif
