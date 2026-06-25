using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 统一输入 Kit，负责接收宿主输入后端并提供动作状态查询。
    /// </summary>
    public static partial class InputKit
    {
        private const float DEFAULT_BUFFER_WINDOW_MS = 150f;

        private static readonly Dictionary<string, InputActionState> sActionStates = new();
        private static readonly List<string> sActionKeys = new();
        private static readonly StateWriter sWriter = new();
        private static IInputBackend sBackend;
        private static float sCurrentTime;
        private static InputDeviceType sCurrentDeviceType = InputDeviceType.Unknown;

        /// <summary>
        /// 当前主要输入设备变化事件。
        /// </summary>
        public static event Action<InputDeviceType> OnDeviceChanged;

        /// <summary>
        /// InputKit 是否已安装输入后端。
        /// </summary>
        public static bool IsInitialized => sBackend != null;

        /// <summary>
        /// 当前主要输入设备类型。
        /// </summary>
        public static InputDeviceType CurrentDeviceType => sCurrentDeviceType;

        /// <summary>
        /// 当前是否主要使用键盘鼠标。
        /// </summary>
        public static bool IsUsingKeyboardMouse => sCurrentDeviceType == InputDeviceType.KeyboardMouse;

        /// <summary>
        /// 当前是否主要使用手柄。
        /// </summary>
        public static bool IsUsingGamepad => sCurrentDeviceType == InputDeviceType.Gamepad;

        /// <summary>
        /// 当前是否主要使用触摸输入。
        /// </summary>
        public static bool IsUsingTouch => sCurrentDeviceType == InputDeviceType.Touch;

        /// <summary>
        /// 当前是否存在已连接手柄。
        /// </summary>
        public static bool IsGamepadConnected => sBackend != null && sBackend.IsGamepadConnected;

        /// <summary>
        /// 安装输入后端。
        /// </summary>
        public static void SetBackend(IInputBackend backend)
        {
            sBackend = backend ?? throw new ArgumentNullException(nameof(backend));
            SetCurrentDeviceType(backend.CurrentDeviceType);
        }

        /// <summary>
        /// 获取当前输入后端。
        /// </summary>
        public static IInputBackend GetBackend() => sBackend;

        /// <summary>
        /// 清除当前输入后端。
        /// </summary>
        public static void ClearBackend()
        {
            sBackend = null;
            SetCurrentDeviceType(InputDeviceType.Unknown);
        }

        /// <summary>
        /// 重置 InputKit 的动作、缓冲、上下文和后端状态。
        /// </summary>
        public static void Reset()
        {
            sActionStates.Clear();
            sActionKeys.Clear();
            sEnabledActionMaps.Clear();
            sBackend = null;
            sInputBuffer = null;
            sBufferWindowMs = DEFAULT_BUFFER_WINDOW_MS;
            sCurrentTime = 0f;
            sCurrentDeviceType = InputDeviceType.Unknown;

            if (sContextStack != null)
            {
                sContextStack.OnContextChanged -= HandleContextChanged;
                sContextStack.Clear();
                sContextStack = null;
            }

            OnDeviceChanged = null;
            OnContextChanged = null;
        }

        /// <summary>
        /// 轮询输入后端并刷新当前帧动作状态。
        /// </summary>
        public static void Update(float unscaledTime)
        {
            sCurrentTime = unscaledTime;
            ClearFrameEdges();

            if (sBackend == null)
                return;

            sWriter.BeginFrame(unscaledTime);
            sBackend.Poll(sWriter);
            SetCurrentDeviceType(sBackend.CurrentDeviceType);
        }

        /// <summary>
        /// 获取指定动作的当前状态。
        /// </summary>
        public static InputActionState GetAction(string actionName)
        {
            InputActionState state;
            return sActionStates.TryGetValue(actionName, out state)
                ? state
                : new(actionName, false, false, false, 0f, 0f);
        }

        /// <summary>
        /// 判断指定动作当前是否按下。
        /// </summary>
        public static bool IsPressed(string actionName) => GetAction(actionName).IsPressed;

        /// <summary>
        /// 判断指定动作是否在当前帧按下。
        /// </summary>
        public static bool WasPressedThisFrame(string actionName) =>
            GetAction(actionName).WasPressedThisFrame;

        /// <summary>
        /// 判断指定动作是否在当前帧释放。
        /// </summary>
        public static bool WasReleasedThisFrame(string actionName) =>
            GetAction(actionName).WasReleasedThisFrame;

        /// <summary>
        /// 获取指定动作的模拟值。
        /// </summary>
        public static float GetValue(string actionName) => GetAction(actionName).Value;

        internal static void WriteAction(string actionName, bool isPressed, float value, float timestamp)
        {
            if (string.IsNullOrEmpty(actionName))
                return;

            InputActionState previous;
            sActionStates.TryGetValue(actionName, out previous);

            var wasPressed = isPressed && !previous.IsPressed;
            var wasReleased = !isPressed && previous.IsPressed;
            var lastChangedAt = wasPressed || wasReleased ? timestamp : previous.LastChangedAt;
            sActionStates[actionName] = new(
                actionName,
                isPressed,
                wasPressed,
                wasReleased,
                value,
                lastChangedAt);
        }

        private static void ClearFrameEdges()
        {
            if (sActionStates.Count == 0)
                return;

            sActionKeys.Clear();
            foreach (var key in sActionStates.Keys)
            {
                sActionKeys.Add(key);
            }

            for (var i = 0; i < sActionKeys.Count; i++)
            {
                var key = sActionKeys[i];
                sActionStates[key] = sActionStates[key].ClearFrameEdges();
            }

            sActionKeys.Clear();
        }

        private static void SetCurrentDeviceType(InputDeviceType deviceType)
        {
            if (sCurrentDeviceType == deviceType)
                return;

            sCurrentDeviceType = deviceType;
            OnDeviceChanged?.Invoke(deviceType);
        }

        private sealed class StateWriter : IInputStateWriter
        {
            private float mTimestamp;

            /// <summary>
            /// 开始写入当前帧输入状态。
            /// </summary>
            /// <param name="timestamp">当前帧时间戳。</param>
            public void BeginFrame(float timestamp)
            {
                mTimestamp = timestamp;
            }

            /// <summary>
            /// 写入指定动作的当前状态。
            /// </summary>
            /// <param name="actionName">动作名称。</param>
            /// <param name="isPressed">动作是否按下。</param>
            /// <param name="value">动作模拟值。</param>
            public void SetAction(string actionName, bool isPressed, float value)
            {
                WriteAction(actionName, isPressed, value, mTimestamp);
            }
        }
    }
}
