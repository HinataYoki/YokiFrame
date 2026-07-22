using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>验证 Editor-only UIKit 项目代码模板注册、排序和源码转换边界。</summary>
    public sealed class UIKitCodeTemplateTests
    {
        private const string TEST_ROOT = "Assets/__YokiFrameUIKitGenerationTests__";
        private const string CUSTOM_TEMPLATE = "TestTemplate";
        private const string SECOND_CUSTOM_TEMPLATE = "AlphaTemplate";
        private const string THIRD_CUSTOM_TEMPLATE = "ZuluTemplate";

        /// <summary>每条测试后注销显式项目模板，避免污染其它生成测试。</summary>
        [TearDown]
        public void TearDown()
        {
            UIKitCodeTemplateRegistry.Unregister(CUSTOM_TEMPLATE);
            UIKitCodeTemplateRegistry.Unregister(SECOND_CUSTOM_TEMPLATE);
            UIKitCodeTemplateRegistry.Unregister(THIRD_CUSTOM_TEMPLATE);
        }

        /// <summary>验证项目模板只转换内存源码，并能同时覆盖 Panel 用户与 Designer 文件。</summary>
        [Test]
        public void RegisteredTemplateTransformsPanelAndDesignerSources()
        {
            UIKitCodeTemplateRegistry.Register(new TestCodeTemplate(CUSTOM_TEMPLATE));
            UIKitPanelCodeLayout layout = CreateLayout("TemplatedPanel", CUSTOM_TEMPLATE);
            Dictionary<string, string> sources = UIKitPanelCodeGenerator.BuildSources(
                layout,
                new UIKitBindScanResult("TemplatedPanel"));

            Assert.AreEqual(2, sources.Count);
            StringAssert.Contains("// template:PanelUser:TemplatedPanel", sources[layout.PanelScriptPath]);
            StringAssert.Contains("// template:PanelDesigner:TemplatedPanel", sources[layout.PanelDesignerPath]);
        }

        /// <summary>验证注销项目模板后，布局不会继续接受未知模板名。</summary>
        [Test]
        public void UnregisteredTemplateIsRejectedByGenerationLayout()
        {
            UIKitCodeTemplateRegistry.Register(new TestCodeTemplate(CUSTOM_TEMPLATE));
            Assert.IsTrue(UIKitCodeTemplateRegistry.Unregister(CUSTOM_TEMPLATE));

            Assert.Throws<ArgumentException>(() => CreateLayout(
                "UnknownTemplatePanel",
                CUSTOM_TEMPLATE));
        }

        /// <summary>验证内置模板固定排在项目模板前，项目模板按名称稳定排序。</summary>
        [Test]
        public void TemplateNamesKeepBuiltInsFirstAndProjectNamesStable()
        {
            UIKitCodeTemplateRegistry.Register(new TestCodeTemplate(THIRD_CUSTOM_TEMPLATE));
            UIKitCodeTemplateRegistry.Register(new TestCodeTemplate(SECOND_CUSTOM_TEMPLATE));

            IReadOnlyList<string> names = UIKitCodeTemplateRegistry.GetTemplateNames();
            Assert.AreEqual(UIKitCodeTemplateRegistry.DEFAULT_TEMPLATE_NAME, names[0]);
            Assert.AreEqual(UIKitCodeTemplateRegistry.MINIMAL_TEMPLATE_NAME, names[1]);
            List<string> orderedNames = new(names);
            Assert.Less(
                orderedNames.IndexOf(SECOND_CUSTOM_TEMPLATE),
                orderedNames.IndexOf(THIRD_CUSTOM_TEMPLATE));
        }

        /// <summary>创建指定模板和隔离输出路径的内存生成布局。</summary>
        private static UIKitPanelCodeLayout CreateLayout(string panelName, string codeTemplate)
        {
            return new UIKitPanelCodeLayout(new UIKitPanelGenerationRequest
            {
                panelName = panelName,
                prefabFolder = TEST_ROOT,
                scriptFolder = TEST_ROOT + "/Scripts",
                scriptNamespace = "YokiFrame.GeneratedTests",
                assemblyName = "Assembly-CSharp",
                codeTemplate = codeTemplate,
                prefabPath = TEST_ROOT + "/" + panelName + ".prefab",
            });
        }

        /// <summary>提供只修改源码前缀的显式测试模板，不拥有文件写入权限。</summary>
        private sealed class TestCodeTemplate : IUIKitCodeTemplate
        {
            /// <summary>创建指定名称的测试模板。</summary>
            internal TestCodeTemplate(string name)
            {
                Name = name;
                Description = "UIKit generation test template";
            }

            /// <summary>获取模板协议名称。</summary>
            public string Name { get; }

            /// <summary>获取模板说明。</summary>
            public string Description { get; }

            /// <summary>为生成源码添加可断言的稳定注释前缀。</summary>
            public string Transform(
                UIKitCodeTemplatePart part,
                UIKitCodeTemplateContext context,
                string generatedSource)
            {
                return "// template:" + part + ":" + context.OwnerTypeName + "\n" + generatedSource;
            }
        }
    }
}
