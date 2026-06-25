namespace YokiFrame
{
    /// <summary>
    /// 表示一个输入动作的完整帧状态。
    /// </summary>
    public readonly struct InputActionState
    {
        /// <summary>
        /// 创建输入动作状态。
        /// </summary>
        public InputActionState(
            string actionName,
            bool isPressed,
            bool wasPressedThisFrame,
            bool wasReleasedThisFrame,
            float value,
            float lastChangedAt)
        {
            ActionName = actionName;
            IsPressed = isPressed;
            WasPressedThisFrame = wasPressedThisFrame;
            WasReleasedThisFrame = wasReleasedThisFrame;
            Value = value;
            LastChangedAt = lastChangedAt;
        }

        /// <summary>输入动作名称。</summary>
        public string ActionName { get; }

        /// <summary>当前帧是否处于按下状态。</summary>
        public bool IsPressed { get; }

        /// <summary>是否在当前帧按下。</summary>
        public bool WasPressedThisFrame { get; }

        /// <summary>是否在当前帧释放。</summary>
        public bool WasReleasedThisFrame { get; }

        /// <summary>输入动作的模拟值。</summary>
        public float Value { get; }

        /// <summary>最近一次状态变化时间。</summary>
        public float LastChangedAt { get; }

        internal InputActionState ClearFrameEdges()
        {
            return new InputActionState(ActionName, IsPressed, false, false, Value, LastChangedAt);
        }
    }
}
