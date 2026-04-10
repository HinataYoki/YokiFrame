#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UIKit SelectableGroup 文档
    /// </summary>
    internal static class UIKitDocSelectableGroup
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "导航分组",
                Description = "SelectableGroup 用于定义 UI 导航区域和边界行为，支持循环导航和跨组跳转。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基本使用",
                        Code = @"// 在 Hierarchy 中添加 SelectableGroup 组件到容器对象
// 组件会自动收集子对象中的所有 Selectable

// 代码获取组内元素
var group = GetComponent<SelectableGroup>();
IReadOnlyList<Selectable> selectables = group.GetSelectables();

// 获取第一个可交互元素
Selectable first = group.GetFirstSelectable();

// 设置默认选中元素（优先于第一个元素）
group.DefaultSelectable = myButton;",
                        Explanation = "SelectableGroup 自动管理子对象中的 Selectable 元素。"
                    },
                    new()
                    {
                        Title = "边界行为",
                        Code = @"// 边界行为类型：
// - NavigationBoundaryBehavior.Stop: 停止在边界
// - NavigationBoundaryBehavior.Wrap: 循环到另一端
// - NavigationBoundaryBehavior.JumpToGroup: 跳转到指定组

var group = GetComponent<SelectableGroup>();

// 设置各方向的边界行为
group.LeftBoundary = NavigationBoundaryBehavior.Stop;
group.RightBoundary = NavigationBoundaryBehavior.Wrap;
group.UpBoundary = NavigationBoundaryBehavior.JumpToGroup;
group.DownBoundary = NavigationBoundaryBehavior.JumpToGroup;

// 获取边界行为
var behavior = group.GetBoundaryBehavior(MoveDirection.Up);",
                        Explanation = "边界行为决定导航到组边缘时的处理方式。"
                    },
                    new()
                    {
                        Title = "跨组跳转",
                        Code = @"// 设置跳转目标组
var menuGroup = menuPanel.GetComponent<SelectableGroup>();
var sidebarGroup = sidebar.GetComponent<SelectableGroup>();

// 菜单组向左跳转到侧边栏
menuGroup.LeftBoundary = NavigationBoundaryBehavior.JumpToGroup;
menuGroup.SetJumpTarget(MoveDirection.Left, sidebarGroup);

// 侧边栏向右跳转回菜单
sidebarGroup.RightBoundary = NavigationBoundaryBehavior.JumpToGroup;
sidebarGroup.SetJumpTarget(MoveDirection.Right, menuGroup);

// 获取跳转目标
SelectableGroup target = menuGroup.GetJumpTarget(MoveDirection.Left);",
                        Explanation = "跨组跳转实现复杂 UI 布局的无缝导航。"
                    },
                    new()
                    {
                        Title = "配置导航",
                        Code = @"// 自动配置组内元素的导航关系
var group = GetComponent<SelectableGroup>();
group.ConfigureNavigation();

// 当子元素变化时标记需要刷新
group.SetDirty();

// 组件会在以下情况自动刷新：
// - OnEnable 时
// - 子对象变化时（OnTransformChildrenChanged）",
                        Explanation = "ConfigureNavigation 会根据边界行为自动设置元素间的导航关系。"
                    },
                    new()
                    {
                        Title = "Inspector 配置",
                        Code = @"// SelectableGroup Inspector 配置项：

// 边界行为（四个方向）
// - Left Boundary: 左边界行为
// - Right Boundary: 右边界行为
// - Up Boundary: 上边界行为
// - Down Boundary: 下边界行为

// 跳转目标（当边界行为为 JumpToGroup 时生效）
// - Left Jump Target: 左跳转目标组
// - Right Jump Target: 右跳转目标组
// - Up Jump Target: 上跳转目标组
// - Down Jump Target: 下跳转目标组

// 默认选中
// - Default Selectable: 默认选中的元素",
                        Explanation = "推荐在 Inspector 中配置边界行为和跳转目标。"
                    }
                }
            };
        }
    }
}
#endif
