#if YOKIFRAME_INPUTSYSTEM_SUPPORT
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// InputKit - 触屏/虚拟控件管理
    /// </summary>
    public static partial class InputKit
    {
        private static readonly Dictionary<string, VirtualJoystick> sJoysticks = new();
        private static readonly Dictionary<string, VirtualButton> sButtons = new();
        private static GestureRecognizer sGestureRecognizer;

        #region 属性

        /// <summary>手势识别器</summary>
        public static GestureRecognizer GestureRecognizer => sGestureRecognizer;

        /// <summary>已注册的虚拟摇杆数量</summary>
        public static int JoystickCount => sJoysticks.Count;

        /// <summary>已注册的虚拟按钮数量</summary>
        public static int ButtonCount => sButtons.Count;

        #endregion

        #region 虚拟摇杆

        /// <summary>
        /// 注册虚拟摇杆
        /// </summary>
        public static void RegisterJoystick(string name, VirtualJoystick joystick)
        {
            if (string.IsNullOrEmpty(name) || joystick == default)
            {
                Debug.LogWarning("[InputKit] 注册摇杆失败：参数无效");
                return;
            }

            if (sJoysticks.ContainsKey(name))
            {
                Debug.LogWarning($"[InputKit] 摇杆 '{name}' 已存在，将被覆盖");
            }

            sJoysticks[name] = joystick;
        }

        /// <summary>
        /// 注销虚拟摇杆
        /// </summary>
        public static void UnregisterJoystick(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            sJoysticks.Remove(name);
        }

        /// <summary>
        /// 获取虚拟摇杆
        /// </summary>
        public static VirtualJoystick GetJoystick(string name)
        {
            if (string.IsNullOrEmpty(name)) return default;
            return sJoysticks.TryGetValue(name, out var joystick) ? joystick : default;
        }

        /// <summary>
        /// 获取摇杆输入
        /// </summary>
        public static Vector2 GetJoystickInput(string name)
        {
            var joystick = GetJoystick(name);
            return joystick != default ? joystick.InputWithDeadZone : Vector2.zero;
        }

        /// <summary>
        /// 检查摇杆是否激活
        /// </summary>
        public static bool IsJoystickActive(string name)
        {
            var joystick = GetJoystick(name);
            return joystick != default && joystick.IsActive;
        }

        #endregion

        #region 虚拟按钮

        /// <summary>
        /// 注册虚拟按钮
        /// </summary>
        public static void RegisterButton(string name, VirtualButton button)
        {
            if (string.IsNullOrEmpty(name) || button == default)
            {
                Debug.LogWarning("[InputKit] 注册按钮失败：参数无效");
                return;
            }

            if (sButtons.ContainsKey(name))
            {
                Debug.LogWarning($"[InputKit] 按钮 '{name}' 已存在，将被覆盖");
            }

            sButtons[name] = button;
        }

        /// <summary>
        /// 注销虚拟按钮
        /// </summary>
        public static void UnregisterButton(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            sButtons.Remove(name);
        }

        /// <summary>
        /// 获取虚拟按钮
        /// </summary>
        public static VirtualButton GetVirtualButton(string name)
        {
            if (string.IsNullOrEmpty(name)) return default;
            return sButtons.TryGetValue(name, out var button) ? button : default;
        }

        /// <summary>
        /// 检查虚拟按钮是否按下
        /// </summary>
        public static bool IsVirtualButtonPressed(string name)
        {
            var button = GetVirtualButton(name);
            return button != default && button.IsPressed;
        }

        /// <summary>
        /// 获取虚拟按钮按住时长
        /// </summary>
        public static float GetVirtualButtonHoldDuration(string name)
        {
            var button = GetVirtualButton(name);
            return button != default ? button.HoldDuration : 0f;
        }

        #endregion

        #region 手势识别

        /// <summary>
        /// 设置手势识别器
        /// </summary>
        public static void SetGestureRecognizer(GestureRecognizer recognizer)
        {
            sGestureRecognizer = recognizer;
        }

        #endregion

        #region 重置

        /// <summary>
        /// 重置触屏系统（内部调用）
        /// </summary>
        internal static void ResetTouch()
        {
            sJoysticks.Clear();
            sButtons.Clear();
            sGestureRecognizer = default;
        }

        #endregion
    }
}

#endif