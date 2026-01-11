#if YOKIFRAME_INPUTSYSTEM_SUPPORT
namespace YokiFrame
{
    /// <summary>
    /// 缓冲输入数据结构
    /// </summary>
    public struct BufferedInput
    {
        /// <summary>Action 名称</summary>
        public string ActionName;
        
        /// <summary>输入时间戳（Time.unscaledTime）</summary>
        public float Timestamp;
        
        /// <summary>输入值</summary>
        public float Value;
        
        /// <summary>是否已被消费</summary>
        public bool IsConsumed;

        /// <summary>
        /// 创建缓冲输入
        /// </summary>
        public static BufferedInput Create(string actionName, float timestamp, float value = 1f)
        {
            return new BufferedInput
            {
                ActionName = actionName,
                Timestamp = timestamp,
                Value = value,
                IsConsumed = false
            };
        }

        /// <summary>
        /// 标记为已消费
        /// </summary>
        public void Consume()
        {
            IsConsumed = true;
        }

        /// <summary>
        /// 检查是否在有效窗口内
        /// </summary>
        public bool IsValid(float currentTime, float windowMs)
        {
            if (IsConsumed) return false;
            float elapsed = (currentTime - Timestamp) * 1000f;
            return elapsed <= windowMs;
        }
    }
}

#endif