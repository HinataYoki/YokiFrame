using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// Buff 系统静态入口类
    /// </summary>
    public static class BuffKit
    {
        // BuffData 注册表
        private static readonly Dictionary<int, BuffData> sBuffDataDict = new();
        
        // 数据提供者
        private static IBuffDataProvider sDataProvider;
        
        // BuffContainer 对象池
        private static readonly SimplePoolKit<BuffContainer> sContainerPool = 
            new(() => new BuffContainer(), null, 4);
        
        // BuffInstance 对象池
        private static readonly SimplePoolKit<BuffInstance> sInstancePool = 
            new(() => new BuffInstance(), null, 32);

#if UNITY_EDITOR
        // 活跃容器列表（仅编辑器模式）
        private static readonly List<BuffContainer> sActiveContainers = new(16);
        
        /// <summary>
        /// 获取所有活跃的 BuffContainer（仅编辑器模式）
        /// </summary>
        public static IReadOnlyList<BuffContainer> ActiveContainers => sActiveContainers;
#endif

        /// <summary>
        /// 默认最大堆叠数
        /// </summary>
        public static int DefaultMaxStack { get; set; } = 99;

        /// <summary>
        /// 默认持续时间（-1 表示永久）
        /// </summary>
        public static float DefaultDuration { get; set; } = -1f;

        #region 数据注册

        /// <summary>
        /// 注册 BuffData
        /// </summary>
        public static void RegisterBuffData(BuffData data)
        {
            if (sBuffDataDict.ContainsKey(data.BuffId))
            {
                // 覆盖已有数据
            }
            sBuffDataDict[data.BuffId] = data;
        }

        /// <summary>
        /// 批量注册 BuffData
        /// </summary>
        public static void RegisterBuffData(IEnumerable<BuffData> dataList)
        {
            foreach (var data in dataList)
            {
                RegisterBuffData(data);
            }
        }

        /// <summary>
        /// 获取 BuffData
        /// </summary>
        public static BuffData GetBuffData(int buffId)
        {
            // 先从注册表查找
            if (sBuffDataDict.TryGetValue(buffId, out var data))
            {
                return data;
            }
            
            // 再从数据提供者查找
            if (sDataProvider != null)
            {
                return sDataProvider.GetBuffData(buffId);
            }
            
            // 返回默认值
            return default;
        }

        /// <summary>
        /// 设置数据提供者
        /// </summary>
        public static void SetDataProvider(IBuffDataProvider provider)
        {
            sDataProvider = provider;
        }

        /// <summary>
        /// 清除所有注册的 BuffData
        /// </summary>
        public static void ClearRegisteredData()
        {
            sBuffDataDict.Clear();
        }

        #endregion

        #region 容器工厂

        /// <summary>
        /// 创建 BuffContainer
        /// </summary>
        public static BuffContainer CreateContainer()
        {
            var container = sContainerPool.Allocate();
            container.IsRecycled = false;
            
#if UNITY_EDITOR
            sActiveContainers.Add(container);
#endif
            
            return container;
        }

        /// <summary>
        /// 回收 BuffContainer
        /// </summary>
        public static void RecycleContainer(BuffContainer container)
        {
            if (container == null || container.IsRecycled) return;
            
#if UNITY_EDITOR
            sActiveContainers.Remove(container);
#endif
            
            container.OnRecycled();
            container.IsRecycled = true;
            sContainerPool.Recycle(container);
        }

        #endregion

        #region 实例池（内部）

        /// <summary>
        /// 分配 BuffInstance（内部使用）
        /// </summary>
        internal static BuffInstance AllocateInstance()
        {
            var instance = sInstancePool.Allocate();
            instance.IsRecycled = false;
            return instance;
        }

        /// <summary>
        /// 回收 BuffInstance（内部使用）
        /// </summary>
        internal static void RecycleInstance(BuffInstance instance)
        {
            if (instance == null || instance.IsRecycled) return;
            
            instance.OnRecycled();
            instance.IsRecycled = true;
            sInstancePool.Recycle(instance);
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 重置 BuffKit 状态（用于测试）
        /// </summary>
        public static void Reset()
        {
            sBuffDataDict.Clear();
            sDataProvider = null;
            
#if UNITY_EDITOR
            // 清理所有活跃容器
            for (int i = sActiveContainers.Count - 1; i >= 0; i--)
            {
                var container = sActiveContainers[i];
                container.OnRecycled();
                container.IsRecycled = true;
            }
            sActiveContainers.Clear();
#endif
        }

        #endregion
    }
}
