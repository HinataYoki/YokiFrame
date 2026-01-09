namespace YokiFrame
{
    /// <summary>
    /// Buff 配置数据结构
    /// </summary>
    public struct BuffData
    {
        /// <summary>
        /// Buff ID（配置表 ID）
        /// </summary>
        public int BuffId;
        
        /// <summary>
        /// 持续时间（秒），-1 表示永久
        /// </summary>
        public float Duration;
        
        /// <summary>
        /// 最大堆叠数
        /// </summary>
        public int MaxStack;
        
        /// <summary>
        /// 堆叠模式
        /// </summary>
        public StackMode StackMode;
        
        /// <summary>
        /// 周期触发间隔（秒），0 表示不触发
        /// </summary>
        public float TickInterval;
        
        /// <summary>
        /// Buff 标签数组
        /// </summary>
        public int[] Tags;
        
        /// <summary>
        /// 排斥标签数组（添加此 Buff 时会移除带有这些标签的 Buff）
        /// </summary>
        public int[] ExclusionTags;

        /// <summary>
        /// 创建一个基础的 BuffData
        /// </summary>
        public static BuffData Create(int buffId, float duration = -1f, int maxStack = 1, 
            StackMode stackMode = StackMode.Refresh, float tickInterval = 0f)
        {
            return new BuffData
            {
                BuffId = buffId,
                Duration = duration,
                MaxStack = maxStack,
                StackMode = stackMode,
                TickInterval = tickInterval,
                Tags = null,
                ExclusionTags = null
            };
        }

        /// <summary>
        /// 设置标签
        /// </summary>
        public BuffData WithTags(params int[] tags)
        {
            Tags = tags;
            return this;
        }

        /// <summary>
        /// 设置排斥标签
        /// </summary>
        public BuffData WithExclusionTags(params int[] exclusionTags)
        {
            ExclusionTags = exclusionTags;
            return this;
        }

        /// <summary>
        /// 检查是否包含指定标签
        /// </summary>
        public bool HasTag(int tag)
        {
            if (Tags == null || Tags.Length == 0) return false;
            for (int i = 0; i < Tags.Length; i++)
            {
                if (Tags[i] == tag) return true;
            }
            return false;
        }

        /// <summary>
        /// 检查是否排斥指定标签
        /// </summary>
        public bool ExcludesTag(int tag)
        {
            if (ExclusionTags == null || ExclusionTags.Length == 0) return false;
            for (int i = 0; i < ExclusionTags.Length; i++)
            {
                if (ExclusionTags[i] == tag) return true;
            }
            return false;
        }

        /// <summary>
        /// 是否为永久 Buff
        /// </summary>
        public bool IsPermanent => Duration < 0;
    }
}
