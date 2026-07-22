#if UNITY_2022_3_OR_NEWER
using UnityEngine;

namespace YokiFrame
{
    /// <summary>由项目或可选 Integration 提供的 UIKit 导航输入快照。</summary>
    public interface IGamepadInput
    {
        /// <summary>获取左摇杆、十字键或键盘映射后的导航轴。</summary>
        Vector2 NavigationAxis { get; }

        /// <summary>获取确认键当前是否按下。</summary>
        bool SubmitPressed { get; }

        /// <summary>获取取消键当前是否按下。</summary>
        bool CancelPressed { get; }

        /// <summary>获取上一 Tab 键当前是否按下。</summary>
        bool TabLeftPressed { get; }

        /// <summary>获取下一 Tab 键当前是否按下。</summary>
        bool TabRightPressed { get; }

        /// <summary>获取左扳机当前是否按下。</summary>
        bool TriggerLeftPressed { get; }

        /// <summary>获取右扳机当前是否按下。</summary>
        bool TriggerRightPressed { get; }

        /// <summary>获取菜单键当前是否按下。</summary>
        bool MenuPressed { get; }

        /// <summary>获取当前帧指针位移。</summary>
        Vector2 MouseDelta { get; }

        /// <summary>获取指针主键当前是否按下。</summary>
        bool MouseLeftPressed { get; }

        /// <summary>获取当前是否存在可用手柄。</summary>
        bool IsGamepadConnected { get; }

        /// <summary>启用输入采集。</summary>
        void Enable();

        /// <summary>禁用输入采集。</summary>
        void Disable();
    }
}
#endif
