namespace YokiFrame
{
    /// <summary>
    /// 对象池事件类型。
    /// </summary>
    public enum PoolEventType
    {
        /// <summary>对象被借出。</summary>
        Spawn,
        /// <summary>对象被归还。</summary>
        Return,
        /// <summary>对象被强制归还。</summary>
        Forced
    }

    /// <summary>
    /// 对象池事件记录（仅编辑器诊断数据）。
    /// </summary>
    public class PoolEvent
    {
        /// <summary>事件类型。</summary>
        public PoolEventType EventType;

        /// <summary>事件时间戳。</summary>
        public float Timestamp;

        /// <summary>对象池名称。</summary>
        public string PoolName;

        /// <summary>对象标识。</summary>
        public string ObjectName;

        /// <summary>调用来源（短方法名）。</summary>
        public string Source;

        /// <summary>调用位置文件。</summary>
        public string SourceFile;

        /// <summary>调用位置行号。</summary>
        public int SourceLine;

        /// <summary>完整堆栈。</summary>
        public string StackTrace;

        /// <summary>对象引用（可能为 null）。</summary>
        public object ObjRef;
    }
}
