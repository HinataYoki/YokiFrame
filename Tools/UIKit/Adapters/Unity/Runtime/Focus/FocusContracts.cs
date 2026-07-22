#if UNITY_2022_3_OR_NEWER
namespace YokiFrame
{
    /// <summary>UIKit 当前输入焦点来源。</summary>
    public enum UIInputMode
    {
        Pointer,
        Navigation
    }

    /// <summary>自动导航组件支持的排列方式。</summary>
    public enum AutoNavigationMode
    {
        Horizontal,
        Vertical,
        Grid
    }

    /// <summary>导航移动方向。</summary>
    public enum MoveDirection
    {
        Left,
        Right,
        Up,
        Down
    }

    /// <summary>可选择组到达边界时的处理方式。</summary>
    public enum NavigationBoundaryBehavior
    {
        Stop,
        Wrap,
        JumpToGroup
    }
}
#endif
