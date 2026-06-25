using System;

namespace YokiFrame
{
    /// <summary>
    /// 固定容量的输入缓冲环形队列。
    /// </summary>
    public sealed class InputBuffer
    {
        private const int DEFAULT_CAPACITY = 32;
        private const float DEFAULT_WINDOW_MS = 150f;

        private readonly BufferedInput[] mBuffer;
        private readonly int mCapacity;
        private int mHead;
        private int mCount;
        private float mWindowMs = DEFAULT_WINDOW_MS;

        /// <summary>
        /// 创建输入缓冲。
        /// </summary>
        public InputBuffer(int capacity = DEFAULT_CAPACITY)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            mCapacity = capacity;
            mBuffer = new BufferedInput[capacity];
        }

        /// <summary>
        /// 输入缓冲有效窗口，单位毫秒。
        /// </summary>
        public float WindowMs
        {
            get { return mWindowMs; }
            set { mWindowMs = value < 0f ? 0f : value; }
        }

        /// <summary>
        /// 当前缓冲内的记录数量。
        /// </summary>
        public int Count
        {
            get { return mCount; }
        }

        /// <summary>
        /// 添加一条输入缓冲记录。
        /// </summary>
        public void Add(string actionName, float timestamp, float value)
        {
            if (string.IsNullOrEmpty(actionName))
                return;

            var index = (mHead + mCount) % mCapacity;
            mBuffer[index] = BufferedInput.Create(actionName, timestamp, value);

            if (mCount < mCapacity)
            {
                mCount++;
                return;
            }

            mHead = (mHead + 1) % mCapacity;
        }

        /// <summary>
        /// 判断指定动作是否存在有效缓冲记录。
        /// </summary>
        public bool Has(string actionName, float currentTime)
        {
            float timestamp;
            float value;
            return Peek(actionName, currentTime, out timestamp, out value);
        }

        /// <summary>
        /// 消费指定动作最近一次有效缓冲记录。
        /// </summary>
        public bool Consume(string actionName, float currentTime)
        {
            for (var i = mCount - 1; i >= 0; i--)
            {
                var index = (mHead + i) % mCapacity;
                if (mBuffer[index].ActionName == actionName && mBuffer[index].IsValid(currentTime, mWindowMs))
                {
                    mBuffer[index].Consume();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 查看指定动作最近一次有效缓冲记录。
        /// </summary>
        public bool Peek(string actionName, float currentTime, out float timestamp, out float value)
        {
            for (var i = mCount - 1; i >= 0; i--)
            {
                var index = (mHead + i) % mCapacity;
                if (mBuffer[index].ActionName == actionName && mBuffer[index].IsValid(currentTime, mWindowMs))
                {
                    timestamp = mBuffer[index].Timestamp;
                    value = mBuffer[index].Value;
                    return true;
                }
            }

            timestamp = 0f;
            value = 0f;
            return false;
        }

        /// <summary>
        /// 清空全部缓冲记录。
        /// </summary>
        public void Clear()
        {
            mHead = 0;
            mCount = 0;
        }

        /// <summary>
        /// 清空指定动作的缓冲记录。
        /// </summary>
        public void Clear(string actionName)
        {
            for (var i = 0; i < mCount; i++)
            {
                var index = (mHead + i) % mCapacity;
                if (mBuffer[index].ActionName == actionName)
                    mBuffer[index].Consume();
            }
        }

        /// <summary>
        /// 移除已经过期的缓冲记录。
        /// </summary>
        public void Cleanup(float currentTime)
        {
            while (mCount > 0)
            {
                if (mBuffer[mHead].IsValid(currentTime, mWindowMs))
                    return;

                mHead = (mHead + 1) % mCapacity;
                mCount--;
            }
        }
    }
}
