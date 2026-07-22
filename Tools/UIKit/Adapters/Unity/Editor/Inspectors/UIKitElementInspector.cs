#if UNITY_EDITOR
using UnityEditor;

namespace YokiFrame
{
    /// <summary>为具体 UIElement 提供 InspectorKit 绑定树和独立代码生成入口。</summary>
    [CustomEditor(typeof(UIElement), true)]
    [CanEditMultipleObjects]
    internal sealed class UIKitElementInspector : UIKitGeneratedOwnerInspector
    {
        /// <inheritdoc />
        protected override UIKitGeneratedOwnerKind OwnerKind => UIKitGeneratedOwnerKind.Element;

        /// <inheritdoc />
        protected override string SettingsTitle => "UIElement 设置";

        /// <inheritdoc />
        protected override string GenerateLabel => "生成 UIElement 代码";

        /// <inheritdoc />
        protected override string BindingTreeStateKey => "UIKit.UIElement.BindingTree";
    }
}
#endif
