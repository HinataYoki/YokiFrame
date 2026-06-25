using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// InputKit 诊断快照。
    /// 这些数据只用于 CommandBridge、Tauri 和 AI 查询，真实输入轮询仍由宿主 backend 驱动。
    /// </summary>
    internal sealed class InputKitDiagnosticsSnapshot
    {
        /// <summary>
        /// 创建 InputKit 诊断快照。
        /// </summary>
        public InputKitDiagnosticsSnapshot(
            bool isInitialized,
            string backendName,
            InputDeviceType currentDeviceType,
            bool isGamepadConnected,
            float currentTime,
            float bufferWindowMs,
            int bufferedInputCount,
            List<string> enabledActionMaps,
            List<InputActionDiagnosticsSnapshot> actions,
            List<InputContextDiagnosticsSnapshot> activeContexts,
            List<InputContextDiagnosticsSnapshot> registeredContexts)
        {
            IsInitialized = isInitialized;
            BackendName = backendName ?? string.Empty;
            CurrentDeviceType = currentDeviceType;
            IsGamepadConnected = isGamepadConnected;
            CurrentTime = currentTime;
            BufferWindowMs = bufferWindowMs;
            BufferedInputCount = bufferedInputCount;
            EnabledActionMaps = enabledActionMaps ?? new List<string>();
            Actions = actions ?? new List<InputActionDiagnosticsSnapshot>();
            ActiveContexts = activeContexts ?? new List<InputContextDiagnosticsSnapshot>();
            RegisteredContexts = registeredContexts ?? new List<InputContextDiagnosticsSnapshot>();
        }

        /// <summary>
        /// InputKit 是否已安装输入后端。
        /// </summary>
        public bool IsInitialized { get; }

        /// <summary>
        /// 当前输入后端名称。
        /// </summary>
        public string BackendName { get; }

        /// <summary>
        /// 当前主要输入设备类型。
        /// </summary>
        public InputDeviceType CurrentDeviceType { get; }

        /// <summary>
        /// 当前是否存在已连接手柄。
        /// </summary>
        public bool IsGamepadConnected { get; }

        /// <summary>
        /// 当前输入时间。
        /// </summary>
        public float CurrentTime { get; }

        /// <summary>
        /// 输入缓冲窗口毫秒数。
        /// </summary>
        public float BufferWindowMs { get; }

        /// <summary>
        /// 当前缓冲输入数量。
        /// </summary>
        public int BufferedInputCount { get; }

        /// <summary>
        /// 当前启用的动作映射列表。
        /// </summary>
        public List<string> EnabledActionMaps { get; }

        /// <summary>
        /// 当前动作状态快照列表。
        /// </summary>
        public List<InputActionDiagnosticsSnapshot> Actions { get; }

        /// <summary>
        /// 当前激活的输入上下文列表。
        /// </summary>
        public List<InputContextDiagnosticsSnapshot> ActiveContexts { get; }

        /// <summary>
        /// 当前已注册的输入上下文列表。
        /// </summary>
        public List<InputContextDiagnosticsSnapshot> RegisteredContexts { get; }
    }

    /// <summary>
    /// 单个输入动作的诊断快照。
    /// </summary>
    internal readonly struct InputActionDiagnosticsSnapshot
    {
        /// <summary>
        /// 根据输入动作状态创建诊断快照。
        /// </summary>
        /// <param name="state">输入动作状态。</param>
        public InputActionDiagnosticsSnapshot(InputActionState state)
        {
            ActionName = state.ActionName ?? string.Empty;
            IsPressed = state.IsPressed;
            WasPressedThisFrame = state.WasPressedThisFrame;
            WasReleasedThisFrame = state.WasReleasedThisFrame;
            Value = state.Value;
            LastChangedAt = state.LastChangedAt;
        }

        /// <summary>
        /// 动作名称。
        /// </summary>
        public string ActionName { get; }

        /// <summary>
        /// 当前动作是否按下。
        /// </summary>
        public bool IsPressed { get; }

        /// <summary>
        /// 当前帧是否按下。
        /// </summary>
        public bool WasPressedThisFrame { get; }

        /// <summary>
        /// 当前帧是否释放。
        /// </summary>
        public bool WasReleasedThisFrame { get; }

        /// <summary>
        /// 动作模拟值。
        /// </summary>
        public float Value { get; }

        /// <summary>
        /// 最近一次状态变化时间。
        /// </summary>
        public float LastChangedAt { get; }
    }

    /// <summary>
    /// 输入上下文的诊断快照。
    /// </summary>
    internal readonly struct InputContextDiagnosticsSnapshot
    {
        /// <summary>
        /// 根据输入上下文创建诊断快照。
        /// </summary>
        /// <param name="context">输入上下文。</param>
        /// <param name="stackIndex">上下文在栈中的索引。</param>
        public InputContextDiagnosticsSnapshot(InputContext context, int stackIndex)
        {
            ContextName = context != null ? context.ContextName ?? string.Empty : string.Empty;
            Priority = context != null ? context.Priority : 0;
            EnabledActionMaps = context != null ? Copy(context.EnabledActionMaps) : new string[0];
            BlockedActions = context != null ? Copy(context.BlockedActions) : new string[0];
            BlockAllLowerPriority = context != null && context.BlockAllLowerPriority;
            StackIndex = stackIndex;
        }

        /// <summary>
        /// 上下文名称。
        /// </summary>
        public string ContextName { get; }

        /// <summary>
        /// 上下文优先级。
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// 上下文启用的动作映射。
        /// </summary>
        public string[] EnabledActionMaps { get; }

        /// <summary>
        /// 上下文屏蔽的动作。
        /// </summary>
        public string[] BlockedActions { get; }

        /// <summary>
        /// 是否屏蔽全部低优先级上下文。
        /// </summary>
        public bool BlockAllLowerPriority { get; }

        /// <summary>
        /// 上下文在栈中的索引。
        /// </summary>
        public int StackIndex { get; }

        private static string[] Copy(string[] source)
        {
            if (source == null || source.Length == 0)
                return new string[0];

            var copy = new string[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                copy[i] = source[i] ?? string.Empty;
            }

            return copy;
        }
    }
}
