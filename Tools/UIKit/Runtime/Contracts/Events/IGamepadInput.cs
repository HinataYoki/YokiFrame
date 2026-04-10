using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 手柄输入接口 - 抽象输入系统，便于测试和扩展
    /// </summary>
    public interface IGamepadInput
    {
        /// <summary>
        /// 导航轴输入（左摇杆/十字键）
        /// </summary>
        Vector2 NavigationAxis { get; }

        /// <summary>
        /// 确认键是否按下（A键/Cross）
        /// </summary>
        bool SubmitPressed { get; }

        /// <summary>
        /// 取消键是否按下（B键/Circle）
        /// </summary>
        bool CancelPressed { get; }

        /// <summary>
        /// 左肩键是否按下（LB/L1）
        /// </summary>
        bool TabLeftPressed { get; }

        /// <summary>
        /// 右肩键是否按下（RB/R1）
        /// </summary>
        bool TabRightPressed { get; }

        /// <summary>
        /// 左扳机是否按下（LT/L2）
        /// </summary>
        bool TriggerLeftPressed { get; }

        /// <summary>
        /// 右扳机是否按下（RT/R2）
        /// </summary>
        bool TriggerRightPressed { get; }

        /// <summary>
        /// 菜单键是否按下（Start/Options）
        /// </summary>
        bool MenuPressed { get; }

        /// <summary>
        /// 鼠标位移增量
        /// </summary>
        Vector2 MouseDelta { get; }

        /// <summary>
        /// 鼠标左键是否按下
        /// </summary>
        bool MouseLeftPressed { get; }

        /// <summary>
        /// 是否有手柄连接
        /// </summary>
        bool IsGamepadConnected { get; }

        /// <summary>
        /// 启用输入
        /// </summary>
        void Enable();

        /// <summary>
        /// 禁用输入
        /// </summary>
        void Disable();
    }
}
