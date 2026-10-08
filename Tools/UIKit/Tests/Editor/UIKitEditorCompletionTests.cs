#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace YokiFrame.Tests
{
    /// <summary>验证 UILevel 解析、PropertyDrawer 注册、面板校验和选择上下文契约。</summary>
    public sealed class UIKitEditorCompletionTests
    {
        /// <summary>验证预定义名称 Pop 解析为排序值 100。</summary>
        [Test]
        public void TryParse_预定义Pop_返回排序100()
        {
            bool parsed = UILevel.TryParse("Pop", out UILevel level);
            Assert.IsTrue(parsed);
            Assert.AreEqual(100, level.Order);
        }

        /// <summary>验证未知名称解析失败，并且输出回落到 Common。</summary>
        [Test]
        public void TryParse_未知名称_返回假()
        {
            bool parsed = UILevel.TryParse("Missing", out UILevel level);
            Assert.IsFalse(parsed);
            Assert.AreEqual(UILevel.Common, level);
        }

        /// <summary>
        /// 验证绘制器注册到 UILevel。
        /// CustomPropertyDrawer.m_Type 是 Unity 内部字段，不是本仓库 private。
        /// 该特性没有公开 type 属性，编辑器也不提供 PropertyDrawer 注册查询。
        /// </summary>
        [Test]
        public void UILevelPropertyDrawer_只读取Unity内部CustomPropertyDrawer字段_注册到UILevel()
        {
            CustomPropertyDrawer attribute = (CustomPropertyDrawer)Attribute.GetCustomAttribute(
                typeof(UILevelPropertyDrawer),
                typeof(CustomPropertyDrawer),
                false);
            Assert.IsNotNull(attribute);
            FieldInfo targetType = typeof(CustomPropertyDrawer).GetField(
                "m_Type",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(targetType);
            Assert.AreEqual(typeof(UILevel), targetType.GetValue(attribute));
        }

        /// <summary>验证只挂具体 UIPanel 的根节点不会产生错误或警告。</summary>
        [Test]
        public void ValidatePanel_有效具体面板_没有错误和警告()
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

        /// <summary>验证面板外的默认 Selectable 被报告为唯一焦点错误。</summary>
        [Test]
        public void ValidatePanel_外部默认Selectable_报告焦点错误()
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
                UIPanelValidationIssue focusError = FindSingleFocusError(result);
                Assert.AreSame(selectable, focusError.Context);
                Assert.AreEqual("默认 Selectable 不属于当前面板层级。", focusError.Message);
                Assert.AreEqual(
                    "将默认 Selectable 移入面板 Prefab，或清空该引用。",
                    focusError.FixSuggestion);
            }
            finally
            {
                Object.DestroyImmediate(external);
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>验证选择 revision 过期时拒绝为当前选择生成代码。</summary>
        [Test]
        public void GenerateCodeForSelection_选择revision过期_拒绝生成()
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

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => UIKitPanelPrefabService.GenerateCodeForSelection(payload));
                Assert.AreEqual("Unity Editor 选择已变化，请刷新当前选择后重试。", exception.Message);
            }
            finally
            {
                Selection.objects = previousSelection;
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        /// <summary>验证空对象和只含上下文字段的选择 payload 会被接受。</summary>
        [Test]
        public void RequireSelectionContext_合法字段_通过()
        {
            Assert.DoesNotThrow(() => UIKitPayloadValidator.RequireSelectionContext("{}"));
            Assert.DoesNotThrow(() => UIKitPayloadValidator.RequireSelectionContext(
                "{\"expectedContextRevision\":12,\"targetGlobalObjectId\":\"GlobalObjectId_V1-test\"}"));
        }

        /// <summary>验证选择 payload 出现未登记字段时抛出参数异常。</summary>
        [Test]
        public void RequireSelectionContext_未知字段_抛出参数异常()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => UIKitPayloadValidator.RequireSelectionContext("{\"unexpected\":true}"));
            Assert.AreEqual(
                "UIKit selection payload may only contain expectedContextRevision and targetGlobalObjectId.",
                exception.Message);
        }

        /// <summary>取出唯一焦点错误，避免把其它类别的问题误当成外部 Selectable 契约。</summary>
        /// <param name="result">面板校验结果。</param>
        /// <returns>唯一的焦点错误。</returns>
        private static UIPanelValidationIssue FindSingleFocusError(UIPanelValidationResult result)
        {
            Assert.AreEqual(1, result.Count(UIPanelValidationSeverity.Error));
            UIPanelValidationIssue focusError = null;
            for (int index = 0; index < result.Issues.Count; index++)
            {
                UIPanelValidationIssue issue = result.Issues[index];
                if (issue.Severity != UIPanelValidationSeverity.Error
                    || issue.Category != UIPanelValidationCategory.Focus)
                {
                    continue;
                }

                Assert.IsNull(focusError);
                focusError = issue;
            }

            Assert.IsNotNull(focusError);
            return focusError;
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
