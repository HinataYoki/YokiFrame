using UnityEngine;
using UnityEngine.EventSystems;

namespace YokiFrame
{
    /// <summary>
    /// 手柄导航事件
    /// </summary>
    public struct GamepadNavigateEvent
    {
        /// <summary>
        /// 导航方向
        /// </summary>
        public MoveDirection Direction;

        /// <summary>
        /// 之前的焦点
        /// </summary>
        public GameObject Previous;

        /// <summary>
        /// 当前焦点
        /// </summary>
        public GameObject Current;
    }

    /// <summary>
    /// 手柄确认事件
    /// </summary>
    public struct GamepadSubmitEvent
    {
        /// <summary>
        /// 触发确认的对象
        /// </summary>
        public GameObject Target;
    }

    /// <summary>
    /// 手柄取消事件
    /// </summary>
    public struct GamepadCancelEvent
    {
        /// <summary>
        /// 当前面板
        /// </summary>
        public IPanel CurrentPanel;
    }

    /// <summary>
    /// Tab 切换事件
    /// </summary>
    public struct GamepadTabSwitchEvent
    {
        /// <summary>
        /// 切换方向（-1 左，1 右）
        /// </summary>
        public int Direction;

        /// <summary>
        /// 之前的 Tab 索引
        /// </summary>
        public int PreviousIndex;

        /// <summary>
        /// 当前的 Tab 索引
        /// </summary>
        public int CurrentIndex;
    }

    /// <summary>
    /// 手柄连接状态变更事件
    /// </summary>
    public struct GamepadConnectionChangedEvent
    {
        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected;

        /// <summary>
        /// 手柄名称
        /// </summary>
        public string DeviceName;
    }
}
