#if UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace YokiFrame.Tests
{
    /// <summary>验证 UIKit Editor 的 UILevel 绘制、只读校验和上下文过期保护。</summary>
    public sealed class UIKitEditorCompletionTests
    {
        /// <summary>验证 UILevel 保留预定义层级、自定义值和专有 PropertyDrawer。</summary>
        [Test]
        public void UILevelKeepsPredefinedAndCustomEditorContract()
        {
            Assert.IsTrue(UILevel.TryParse("Pop", out UILevel pop));
            Assert.AreEqual(100, pop.Order);
            Assert.IsFalse(UILevel.TryParse("Missing", out _));
            Assert.Greater(
                typeof(UILevelPropertyDrawer).GetCustomAttributes(
                    typeof(CustomPropertyDrawer), false).Length,
                0);
        }

        /// <summary>验证有效具体 Panel 可以通过公共只读校验且不会伪造问题。</summary>
        [Test]
        public void ValidPanelProducesNoValidationIssues()
        {
            GameObject root = new("ValidationPanel");
            try
            {
                root.AddComponent<UIKitValidationTestPanel>();
                UIPanelValidationResult result = UIPanelValidator.ValidatePanel(root);
                Assert.IsFalse(result.HasErrors);
                Assert.IsFalse(result.HasWarnings);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>验证自动聚焦引用到面板外部控件时报告阻断错误。</summary>
        [Test]
        public void PanelValidatorRejectsExternalDefaultSelectable()
        {
            GameObject root = new("ValidationPanel");
            GameObject external = new("ExternalSelectable");
            try
            {
                UIKitValidationTestPanel panel = root.AddComponent<UIKitValidationTestPanel>();
                Selectable selectable = external.AddComponent<Button>();
                panel.SetAutoFocusOnShow(true);
                panel.SetDefaultSelectable(selectable);

                UIPanelValidationResult result = UIPanelValidator.ValidatePanel(root);
                Assert.IsTrue(result.HasErrors);
                StringAssert.Contains("不属于当前面板层级", result.Issues[0].Message);
            }
            finally
            {
                Object.DestroyImmediate(external);
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>验证携带旧 revision 的生成请求在 Selection 变化后被拒绝。</summary>
        [Test]
        public void StaleSelectionContextRejectsCodeGeneration()
        {
            Object[] previousSelection = Selection.objects;
            GameObject first = new("FirstSelection");
            GameObject second = new("SecondSelection");
            try
            {
                Selection.activeGameObject = first;
                UnityEditorContextSnapshot context = UnityEditorContextService.Capture();
                Selection.activeGameObject = second;
                string payload = "{\"panelName\":\"Panel\",\"prefabFolder\":\"Assets/UI\","
                    + "\"scriptFolder\":\"Assets/Scripts/UI\",\"scriptNamespace\":\"Game.UI\","
                    + "\"assemblyName\":\"Assembly-CSharp\",\"codeTemplate\":\"Default\","
                    + "\"expectedContextRevision\":" + context.revision + ","
                    + "\"targetGlobalObjectId\":\"" + context.selection.activeGlobalObjectId + "\"}";

                Assert.DoesNotThrow(() => UIKitPayloadValidator.RequirePanelGenerationRequest(payload));
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => UIKitPanelPrefabService.GenerateCodeForSelection(payload));
                StringAssert.Contains("选择已变化", exception.Message);
            }
            finally
            {
                Selection.objects = previousSelection;
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        /// <summary>验证上下文扩展字段保持严格类型并支持空选择操作。</summary>
        [Test]
        public void SelectionPayloadAcceptsOnlyContextFields()
        {
            Assert.DoesNotThrow(() => UIKitPayloadValidator.RequireSelectionContext("{}"));
            Assert.DoesNotThrow(() => UIKitPayloadValidator.RequireSelectionContext(
                "{\"expectedContextRevision\":12,\"targetGlobalObjectId\":\"GlobalObjectId_V1-test\"}"));
            Assert.Throws<System.ArgumentException>(() => UIKitPayloadValidator.RequireSelectionContext(
                "{\"unexpected\":true}"));
        }

        /// <summary>供 Editor 校验测试使用的最小具体 UIPanel 类型。</summary>
        private sealed class UIKitValidationTestPanel : UIPanel
        {
            /// <summary>测试面板初始化钩子。</summary>
            protected override void OnInit(IUIData data = null) { }
        }
    }
}
#endif
