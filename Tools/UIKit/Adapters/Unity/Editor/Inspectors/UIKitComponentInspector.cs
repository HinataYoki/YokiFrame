#if UNITY_EDITOR
using UnityEditor;

namespace YokiFrame
{
    /// <summary>为具体 UIComponent 提供 InspectorKit 绑定树和独立代码生成入口。</summary>
    [CustomEditor(typeof(UIComponent), true)]
    [CanEditMultipleObjects]
    internal sealed class UIKitComponentInspector : UIKitGeneratedOwnerInspector
    {
        /// <inheritdoc />
        protected override UIKitGeneratedOwnerKind OwnerKind => UIKitGeneratedOwnerKind.Component;

        /// <inheritdoc />
        protected override string SettingsTitle => "UIComponent 设置";

        /// <inheritdoc />
        protected override string GenerateLabel => "生成 UIComponent 代码";

        /// <inheritdoc />
        protected override string BindingTreeStateKey => "UIKit.UIComponent.BindingTree";
    }
}
#endif
