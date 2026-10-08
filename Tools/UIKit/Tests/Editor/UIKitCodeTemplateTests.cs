using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>验证 Editor-only UIKit 项目代码模板注册、排序和生成声明。</summary>
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

        /// <summary>验证项目模板会给 Panel 用户脚本和 Designer 各插入一次稳定标记行。</summary>
        [Test]
        public void BuildSources_已注册项目模板_同时标记用户和Designer源码()
        {
            UIKitCodeTemplateRegistry.Register(new TestCodeTemplate(CUSTOM_TEMPLATE));
            UIKitPanelCodeLayout layout = CreateLayout("TemplatedPanel", CUSTOM_TEMPLATE);
            Dictionary<string, string> sources = UIKitPanelCodeGenerator.BuildSources(
                layout,
                new UIKitBindScanResult("TemplatedPanel"));

            Assert.AreEqual(2, sources.Count);
            Assert.AreEqual(1, CountExactLines(
                sources[layout.PanelScriptPath],
                "// template:" + UIKitCodeTemplatePart.PanelUser + ":TemplatedPanel"));
            Assert.AreEqual(1, CountExactLines(
                sources[layout.PanelDesignerPath],
                "// template:" + UIKitCodeTemplatePart.PanelDesigner + ":TemplatedPanel"));
            Assert.IsTrue(HasDeclaration(
                sources[layout.PanelScriptPath],
                "public partial class TemplatedPanel : UIPanel"));
            Assert.IsTrue(HasDeclaration(
                sources[layout.PanelDesignerPath],
                "public partial class TemplatedPanel"));
        }

        /// <summary>验证注销项目模板后，布局构造会拒绝该模板名。</summary>
        [Test]
        public void UIKitPanelCodeLayout_模板已注销_拒绝未知模板()
        {
            UIKitCodeTemplateRegistry.Register(new TestCodeTemplate(CUSTOM_TEMPLATE));
            Assert.IsTrue(UIKitCodeTemplateRegistry.Unregister(CUSTOM_TEMPLATE));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => CreateLayout(
                "UnknownTemplatePanel",
                CUSTOM_TEMPLATE));
            Assert.AreEqual("templateName", exception.ParamName);
            Assert.AreEqual(
                "未知 UIKit 代码模板: " + CUSTOM_TEMPLATE + " (Parameter 'templateName')",
                exception.Message);
        }

        /// <summary>验证内置模板固定排在项目模板前，项目模板按名称稳定排序。</summary>
        [Test]
        public void GetTemplateNames_同时存在项目模板_内置在前且名称稳定()
        {
            UIKitCodeTemplateRegistry.Register(new TestCodeTemplate(THIRD_CUSTOM_TEMPLATE));
            UIKitCodeTemplateRegistry.Register(new TestCodeTemplate(SECOND_CUSTOM_TEMPLATE));

            IReadOnlyList<string> names = UIKitCodeTemplateRegistry.GetTemplateNames();
            Assert.AreEqual(UIKitCodeTemplateRegistry.DEFAULT_TEMPLATE_NAME, names[0]);
            Assert.AreEqual(UIKitCodeTemplateRegistry.MINIMAL_TEMPLATE_NAME, names[1]);
            Assert.AreEqual(UIKitCodeTemplateRegistry.DIALOG_TEMPLATE_NAME, names[2]);
            List<string> orderedNames = new(names);
            Assert.Less(
                orderedNames.IndexOf(SECOND_CUSTOM_TEMPLATE),
                orderedNames.IndexOf(THIRD_CUSTOM_TEMPLATE));
        }

        /// <summary>验证 Dialog 模板的用户脚本声明继承 UIDialogPanel，并重写 SetupDialog。</summary>
        [Test]
        public void BuildSources_Dialog模板_声明UIDialogPanel和SetupDialog()
        {
            UIKitPanelCodeLayout layout = CreateLayout("ConfirmDialog", UIKitPanelGenerationRequest.DIALOG_TEMPLATE);
            Dictionary<string, string> sources = UIKitPanelCodeGenerator.BuildSources(
                layout,
                new UIKitBindScanResult("ConfirmDialog"));

            string userSource = sources[layout.PanelScriptPath];
            Assert.IsTrue(HasDeclaration(userSource, "public partial class ConfirmDialog : UIDialogPanel"));
            Assert.IsTrue(HasDeclaration(
                userSource,
                "protected override void SetupDialog(DialogConfig config)"));
        }

        /// <summary>创建指定模板和隔离输出路径的内存生成布局。</summary>
        /// <param name="panelName">面板类型名。</param>
        /// <param name="codeTemplate">已注册模板名。</param>
        /// <returns>只存在于内存中的生成布局。</returns>
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

        /// <summary>统计与目标文本完全相等的物理行。</summary>
        /// <param name="source">生成源码。</param>
        /// <param name="line">不含缩进的完整行。</param>
        /// <returns>命中次数。</returns>
        private static int CountExactLines(string source, string line)
        {
            int count = 0;
            string[] lines = source.Split('\n');
            for (int index = 0; index < lines.Length; index++)
            {
                if (lines[index] == line) count++;
            }

            return count;
        }

        /// <summary>判断是否存在去掉 Tab 缩进后与声明完全相等的行。</summary>
        /// <param name="source">生成源码。</param>
        /// <param name="declaration">不含缩进的声明文本。</param>
        /// <returns>存在完全相等的声明行时返回 true。</returns>
        private static bool HasDeclaration(string source, string declaration)
        {
            string[] lines = source.Split('\n');
            for (int index = 0; index < lines.Length; index++)
            {
                if (lines[index].TrimStart('\t') == declaration) return true;
            }

            return false;
        }


        /// <summary>提供只修改源码前缀的显式测试模板，不拥有文件写入权限。</summary>
        private sealed class TestCodeTemplate : IUIKitCodeTemplate
        {
            /// <summary>创建指定名称的测试模板。</summary>
            /// <param name="name">模板协议名称。</param>
            internal TestCodeTemplate(string name)
            {
                Name = name;
                Description = "UIKit generation test template";
            }

            /// <summary>获取模板协议名称。</summary>
            public string Name { get; }

            /// <summary>获取模板说明。</summary>
            public string Description { get; }

            /// <summary>为生成源码添加可按整行断言的稳定注释。</summary>
            /// <param name="part">当前文件角色。</param>
            /// <param name="context">模板上下文。</param>
            /// <param name="generatedSource">内置生成器产生的源码。</param>
            /// <returns>带单行标记的完整源码。</returns>
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