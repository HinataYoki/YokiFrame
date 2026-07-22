#if UNITY_2022_3_OR_NEWER && YOKIFRAME_INPUTSYSTEM_SUPPORT && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine;
using UnityEngine.InputSystem.UI;

namespace YokiFrame
{
    /// <summary>在 Input System-only 项目中为 UIKit 注册对应的 EventSystem 输入模块。</summary>
    internal static class UIKitInputSystemModuleInstaller
    {
        /// <summary>每次 Player 子系统初始化时重新安装工厂，兼容禁用 Domain Reload 的运行方式。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            UIKit.RegisterInputModuleFactory(static owner =>
            {
                return owner.AddComponent<InputSystemUIInputModule>();
            });
        }
    }
}
#endif
