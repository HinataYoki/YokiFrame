#if UNITY_EDITOR
namespace YokiFrame
{
    /// <summary>
    /// 池事件类型
    /// </summary>
    public enum PoolEventType
    {
        /// <summary>
        /// 借出
        /// </summary>
        Spawn,
        
        /// <summary>
        /// 归还
        /// </summary>
        Return,
        
        /// <summary>
        /// 强制归还
        /// </summary>
        Forced
    }

    /// <summary>
    /// 池事件记录
    /// </summary>
    public class PoolEvent
    {
        /// <summary>
        /// 事件类型
        /// </summary>
        public PoolEventType EventType;
        
        /// <summary>
        /// 事件时间戳（Time.realtimeSinceStartup）
        /// </summary>
        public float Timestamp;
        
        /// <summary>
        /// 池名称
        /// </summary>
        public string PoolName;
        
        /// <summary>
        /// 对象标识
        /// </summary>
        public string ObjectName;
        
        /// <summary>
        /// 调用来源（简短方法名）
        /// </summary>
        public string Source;
        
        /// <summary>
        /// 完整堆栈追踪
        /// </summary>
        public string StackTrace;
        
        /// <summary>
        /// 对象引用（可能为 null）
        /// </summary>
        public object ObjRef;
    }
}
#endif
