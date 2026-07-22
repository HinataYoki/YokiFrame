using Avalonia.Controls;

namespace YokiFrame.Workbench.Avalonia.Views.Pages.ActionKit;

/// <summary>复用 ActionKit 当前节点详情，使宽屏右栏与紧凑抽屉保持同一布局。</summary>
public sealed partial class ActionKitNodeInspectorView : UserControl
{
    /// <summary>初始化 ActionKit 节点详情组件。</summary>
    public ActionKitNodeInspectorView()
    {
        InitializeComponent();
    }
}
