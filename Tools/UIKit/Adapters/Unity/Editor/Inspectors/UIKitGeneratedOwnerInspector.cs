#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.Unity.Inspector;

namespace YokiFrame
{
    /// <summary>实现 UIElement/UIComponent 共用的 InspectorKit owner 界面。</summary>
    internal abstract class UIKitGeneratedOwnerInspector : UnityEditor.Editor
    {
        private UIKitBindingTreeView mBindingTree;

        /// <summary>获取当前 Inspector 对应的生成 owner kind。</summary>
        protected abstract UIKitGeneratedOwnerKind OwnerKind { get; }

        /// <summary>获取 Inspector 设置卡片标题。</summary>
        protected abstract string SettingsTitle { get; }

        /// <summary>获取当前 owner 的明确生成按钮文本。</summary>
        protected abstract string GenerateLabel { get; }

        /// <summary>获取绑定树卡片展开状态键。</summary>
        protected abstract string BindingTreeStateKey { get; }

        /// <summary>初始化当前 owner 的共享绑定树。</summary>
        protected virtual void OnEnable()
        {
            mBindingTree = new UIKitBindingTreeView(
                () => target as Component,
                OpenOwnerScript,
                GenerateOwnerCode,
                BindingTreeStateKey,
                GenerateLabel,
                OwnerKind);
        }

        /// <summary>创建 owner 信息、绑定树和具体类型其它属性。</summary>
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = InspectorKitUi.CreateRoot();
            Component owner = target as Component;
            if (owner == default)
            {
                root.Add(InspectorKitUi.CreateInfoBox(
                    "Inspector 目标已失效，请重新选择组件。",
                    InspectorInfoBoxType.Warning));
                return root;
            }
            root.Add(CreateSettings(owner));
            root.Add(mBindingTree.Create());
            VisualElement otherProperties = CreateOtherProperties(owner);
            if (otherProperties != null)
                root.Add(otherProperties);
            return root;
        }

        /// <summary>创建显示具体类型和用户脚本路径的设置卡片。</summary>
        private VisualElement CreateSettings(Component owner)
        {
            return InspectorKitUi.CreateCard(
                SettingsTitle,
                SettingsTitle,
                InspectorCardInitialState.Expanded,
                body =>
                {
                    body.Add(InspectorKitUi.CreateReadOnlyStringRow(
                        "类型",
                        owner.GetType().FullName));
                    body.Add(InspectorKitUi.CreateReadOnlyStringRow(
                        "脚本",
                        GetScriptPathSafe(owner)));
                });
        }

        /// <summary>创建当前具体 owner 声明的其它序列化属性卡片。</summary>
        private VisualElement CreateOtherProperties(Component owner)
        {
            VisualElement properties = InspectorKitUi.CreatePropertyFields(
                serializedObject,
                owner.GetType());
            if (properties.childCount == 0)
                return default;
            return InspectorKitUi.CreateCard(
                "其他属性",
                SettingsTitle + ".OtherProperties",
                InspectorCardInitialState.Collapsed,
                body => body.Add(properties));
        }

        /// <summary>调用独立 owner 生成服务并刷新绑定树。</summary>
        private void GenerateOwnerCode()
        {
            try
            {
                UIKitGeneratedOwnerCodeService.Generate(target as Component, OwnerKind);
                mBindingTree.Refresh();
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("生成失败", exception.Message, "确定");
                LogKit.Exception(exception);
            }
        }

        /// <summary>打开当前 owner 的用户脚本，并显示可诊断失败。</summary>
        private void OpenOwnerScript()
        {
            try
            {
                UIKitGeneratedOwnerCodeService.OpenScript(target as Component);
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("无法打开脚本", exception.Message, "确定");
                LogKit.Exception(exception);
            }
        }

        /// <summary>安全读取脚本路径，未导入 MonoScript 时返回明确状态。</summary>
        private static string GetScriptPathSafe(Component owner)
        {
            try
            {
                return UIKitGeneratedOwnerCodeService.GetScriptPath(owner);
            }
            catch (InvalidOperationException)
            {
                return "未找到 MonoScript";
            }
        }
    }
}
#endif
