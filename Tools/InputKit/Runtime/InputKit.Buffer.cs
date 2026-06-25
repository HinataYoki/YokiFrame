namespace YokiFrame
{
    /// <summary>
    /// InputKit 的短窗口输入缓冲能力。
    /// </summary>
    public static partial class InputKit
    {
        private const float DEFAULT_BUFFER_VALUE = 1f;

        private static InputBuffer sInputBuffer;
        private static float sBufferWindowMs = DEFAULT_BUFFER_WINDOW_MS;

        /// <summary>
        /// 输入缓冲有效窗口，单位毫秒。
        /// </summary>
        public static float BufferWindow
        {
            get { return sBufferWindowMs; }
            set
            {
                sBufferWindowMs = value < 0f ? 0f : value;
                if (sInputBuffer != null)
                    sInputBuffer.WindowMs = sBufferWindowMs;
            }
        }

        /// <summary>
        /// 设置输入缓冲有效窗口，单位毫秒。
        /// </summary>
        public static void SetBufferWindow(float milliseconds)
        {
            BufferWindow = milliseconds;
        }

        /// <summary>
        /// 缓存一次输入动作。
        /// </summary>
        public static void BufferInput(string actionName, float value = DEFAULT_BUFFER_VALUE)
        {
            EnsureBufferInitialized();
            sInputBuffer.Add(actionName, sCurrentTime, value);
        }

        /// <summary>
        /// 判断指定动作是否存在可消费的缓冲输入。
        /// </summary>
        public static bool HasBufferedInput(string actionName) =>
            sInputBuffer != null && sInputBuffer.Has(actionName, sCurrentTime);

        /// <summary>
        /// 消费指定动作最近一次有效缓冲输入。
        /// </summary>
        public static bool ConsumeBufferedInput(string actionName) =>
            sInputBuffer != null && sInputBuffer.Consume(actionName, sCurrentTime);

        /// <summary>
        /// 查看指定动作最近一次有效缓冲输入。
        /// </summary>
        public static bool PeekBufferedInput(string actionName, out float timestamp, out float value)
        {
            if (sInputBuffer == null)
            {
                timestamp = 0f;
                value = 0f;
                return false;
            }

            return sInputBuffer.Peek(actionName, sCurrentTime, out timestamp, out value);
        }

        /// <summary>
        /// 清空全部输入缓冲。
        /// </summary>
        public static void ClearBuffer()
        {
            if (sInputBuffer != null)
                sInputBuffer.Clear();
        }

        /// <summary>
        /// 清空指定动作的输入缓冲。
        /// </summary>
        public static void ClearBuffer(string actionName)
        {
            if (sInputBuffer != null)
                sInputBuffer.Clear(actionName);
        }

        /// <summary>
        /// 清理已过期的输入缓冲。
        /// </summary>
        public static void CleanupBuffer()
        {
            if (sInputBuffer != null)
                sInputBuffer.Cleanup(sCurrentTime);
        }

        private static void EnsureBufferInitialized()
        {
            if (sInputBuffer != null)
                return;

            sInputBuffer = new();
            sInputBuffer.WindowMs = sBufferWindowMs;
        }
    }
}
