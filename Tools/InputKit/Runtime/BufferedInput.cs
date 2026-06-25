namespace YokiFrame
{
    /// <summary>
    /// 表示一条可在短时间窗口内消费的输入缓存。
    /// </summary>
    public struct BufferedInput
    {
        /// <summary>输入动作名称。</summary>
        public string ActionName;

        /// <summary>输入发生时的时间戳。</summary>
        public float Timestamp;

        /// <summary>输入值。</summary>
        public float Value;

        /// <summary>是否已经被消费。</summary>
        public bool IsConsumed;

        /// <summary>
        /// 创建一条新的输入缓存记录。
        /// </summary>
        public static BufferedInput Create(string actionName, float timestamp, float value)
        {
            return new()
            {
                ActionName = actionName,
                Timestamp = timestamp,
                Value = value,
                IsConsumed = false
            };
        }

        /// <summary>
        /// 标记该输入已被消费。
        /// </summary>
        public void Consume()
        {
            IsConsumed = true;
        }

        /// <summary>
        /// 判断该输入在当前时间窗口内是否仍可用。
        /// </summary>
        public bool IsValid(float currentTime, float windowMs)
        {
            if (IsConsumed)
                return false;

            var elapsedMs = (currentTime - Timestamp) * 1000f;
            return elapsedMs <= windowMs;
        }
    }
}
