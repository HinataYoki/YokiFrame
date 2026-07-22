#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    internal sealed partial class UIKitPanelInspector
    {
        private UIKitBindingTreeView mBindingTree;

        /// <summary>为当前 UIPanel 创建共享绑定树状态。</summary>
        private void InitializeBindingTree()
        {
            mBindingTree = new UIKitBindingTreeView(
                () => target as Component,
                OpenPanelScript,
                GenerateUICode,
                "UIKit.Panel.BindingTree",
                "生成 UIPanel 代码");
        }

        /// <summary>创建当前 UIPanel 的 InspectorKit 绑定树。</summary>
        private VisualElement CreateBindingTree()
        {
            return mBindingTree.Create();
        }

        /// <summary>重新扫描并刷新当前 UIPanel 的绑定树。</summary>
        private void RefreshBindingTree()
        {
            mBindingTree.Refresh();
        }
    }
}
#endif
