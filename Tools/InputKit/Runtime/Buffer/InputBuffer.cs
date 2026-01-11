#if YOKIFRAME_INPUTSYSTEM_SUPPORT
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 输入缓冲核心实现（环形缓冲区，零 GC）
    /// </summary>
    public sealed class InputBuffer
    {
        private const int DEFAULT_CAPACITY = 32;
        
        private readonly BufferedInput[] mBuffer;
        private readonly int mCapacity;
        private int mHead;
        private int mCount;
        private float mWindowMs = 150f;

        /// <summary>缓冲窗口时长（毫秒）</summary>
        public float WindowMs
        {
            get => mWindowMs;
            set => mWindowMs = Mathf.Max(0f, value);
        }

        /// <summary>当前缓冲数量</summary>
        public int Count => mCount;

        public InputBuffer(int capacity = DEFAULT_CAPACITY)
        {
            mCapacity = capacity;
            mBuffer = new BufferedInput[capacity];
            mHead = 0;
            mCount = 0;
        }

        /// <summary>
        /// 添加输入到缓冲
        /// </summary>
        public void Add(string actionName, float value = 1f)
        {
            var input = BufferedInput.Create(actionName, Time.unscaledTime, value);
            
            int index = (mHead + mCount) % mCapacity;
            mBuffer[index] = input;
            
            if (mCount < mCapacity)
            {
                mCount++;
            }
            else
            {
                // 缓冲区满，覆盖最旧的
                mHead = (mHead + 1) % mCapacity;
            }
        }

        /// <summary>
        /// 查询缓冲中是否有指定输入（未消费且在窗口内）
        /// </summary>
        public bool Has(string actionName)
        {
            float currentTime = Time.unscaledTime;
            
            for (int i = 0; i < mCount; i++)
            {
                int index = (mHead + i) % mCapacity;
                ref var input = ref mBuffer[index];
                
                if (input.ActionName == actionName && input.IsValid(currentTime, mWindowMs))
                {
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// 消费缓冲中的输入（返回是否成功）
        /// </summary>
        public bool Consume(string actionName)
        {
            float currentTime = Time.unscaledTime;
            
            // 从最新的开始查找
            for (int i = mCount - 1; i >= 0; i--)
            {
                int index = (mHead + i) % mCapacity;
                ref var input = ref mBuffer[index];
                
                if (input.ActionName == actionName && input.IsValid(currentTime, mWindowMs))
                {
                    input.Consume();
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// 查看缓冲中的输入（不消费）
        /// </summary>
        public bool Peek(string actionName, out float timestamp, out float value)
        {
            float currentTime = Time.unscaledTime;
            
            // 从最新的开始查找
            for (int i = mCount - 1; i >= 0; i--)
            {
                int index = (mHead + i) % mCapacity;
                ref var input = ref mBuffer[index];
                
                if (input.ActionName == actionName && input.IsValid(currentTime, mWindowMs))
                {
                    timestamp = input.Timestamp;
                    value = input.Value;
                    return true;
                }
            }
            
            timestamp = 0f;
            value = 0f;
            return false;
        }

        /// <summary>
        /// 清空所有缓冲
        /// </summary>
        public void Clear()
        {
            mHead = 0;
            mCount = 0;
        }

        /// <summary>
        /// 清空指定 Action 的缓冲
        /// </summary>
        public void Clear(string actionName)
        {
            for (int i = 0; i < mCount; i++)
            {
                int index = (mHead + i) % mCapacity;
                ref var input = ref mBuffer[index];
                
                if (input.ActionName == actionName)
                {
                    input.Consume(); // 标记为已消费
                }
            }
        }

        /// <summary>
        /// 清理过期的缓冲项
        /// </summary>
        public void Cleanup()
        {
            float currentTime = Time.unscaledTime;
            
            while (mCount > 0)
            {
                ref var oldest = ref mBuffer[mHead];
                if (!oldest.IsValid(currentTime, mWindowMs))
                {
                    mHead = (mHead + 1) % mCapacity;
                    mCount--;
                }
                else
                {
                    break;
                }
            }
        }
    }
}

#endif