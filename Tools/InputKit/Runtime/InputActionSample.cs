namespace YokiFrame
{
    /// <summary>
    /// 表示一次输入动作采样。
    /// </summary>
    public readonly struct InputActionSample
    {
        /// <summary>
        /// 创建输入动作采样。
        /// </summary>
        public InputActionSample(string actionName, bool isPressed, float value)
        {
            ActionName = actionName;
            IsPressed = isPressed;
            Value = value;
        }

        /// <summary>输入动作名称。</summary>
        public string ActionName { get; }

        /// <summary>当前帧是否处于按下状态。</summary>
        public bool IsPressed { get; }

        /// <summary>输入动作的模拟值。</summary>
        public float Value { get; }
    }
}
