using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// Buff 实例，表示一个正在生效的 Buff，包含运行时状态
    /// </summary>
    public class BuffInstance : IPoolable
    {
        /// <summary>
        /// Buff ID
        /// </summary>
        public int BuffId { get; internal set; }
        
        /// <summary>
        /// Buff 定义引用
        /// </summary>
        public IBuff Buff { get; internal set; }
        
        /// <summary>
        /// 剩余持续时间（秒），负数表示永久
        /// </summary>
        public float RemainingDuration { get; internal set; }
        
        /// <summary>
        /// 已经过的 Tick 时间（秒）
        /// </summary>
        public float ElapsedTickTime { get; internal set; }
        
        /// <summary>
        /// 当前堆叠层数
        /// </summary>
        public int StackCount { get; internal set; }
        
        /// <summary>
        /// 是否为永久 Buff
        /// </summary>
        public bool IsPermanent => RemainingDuration < 0;

        /// <summary>
        /// 原始持续时间（用于刷新）
        /// </summary>
        internal float OriginalDuration { get; set; }

        // 自定义数据存储（使用 int key 避免字符串）
        private Dictionary<int, object> mCustomData;

        #region 对象池接口

        public bool IsRecycled { get; set; }

        public void OnRecycled()
        {
            BuffId = 0;
            Buff = null;
            RemainingDuration = 0;
            ElapsedTickTime = 0;
            StackCount = 0;
            OriginalDuration = 0;
            mCustomData?.Clear();
        }

        #endregion

        #region 自定义数据

        /// <summary>
        /// 设置自定义数据
        /// </summary>
        public void SetData<T>(int key, T value)
        {
            mCustomData ??= new Dictionary<int, object>();
            mCustomData[key] = value;
        }

        /// <summary>
        /// 获取自定义数据
        /// </summary>
        public T GetData<T>(int key)
        {
            if (mCustomData != null && mCustomData.TryGetValue(key, out var value))
            {
                return (T)value;
            }
            return default;
        }

        /// <summary>
        /// 尝试获取自定义数据
        /// </summary>
        public bool TryGetData<T>(int key, out T value)
        {
            if (mCustomData != null && mCustomData.TryGetValue(key, out var obj))
            {
                value = (T)obj;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// 检查是否存在自定义数据
        /// </summary>
        public bool HasData(int key)
        {
            return mCustomData != null && mCustomData.ContainsKey(key);
        }

        /// <summary>
        /// 移除自定义数据
        /// </summary>
        public bool RemoveData(int key)
        {
            return mCustomData != null && mCustomData.Remove(key);
        }

        #endregion

        /// <summary>
        /// 初始化 BuffInstance
        /// </summary>
        internal void Initialize(int buffId, IBuff buff, float duration, int stackCount = 1)
        {
            BuffId = buffId;
            Buff = buff;
            RemainingDuration = duration;
            OriginalDuration = duration;
            ElapsedTickTime = 0;
            StackCount = stackCount;
            IsRecycled = false;
        }

        /// <summary>
        /// 刷新持续时间
        /// </summary>
        internal void RefreshDuration()
        {
            if (!IsPermanent)
            {
                RemainingDuration = OriginalDuration;
            }
        }
    }
}
