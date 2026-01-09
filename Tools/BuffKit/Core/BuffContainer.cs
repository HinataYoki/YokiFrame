using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// Buff 容器，管理单个实体身上的所有 Buff
    /// </summary>
    public partial class BuffContainer : IPoolable, IDisposable
    {
        // 按 BuffId 分组存储的 Buff 实例
        private readonly Dictionary<int, List<BuffInstance>> mBuffsByIdDict = new();
        
        // 所有 Buff 实例的平铺列表（用于遍历）
        private readonly List<BuffInstance> mAllBuffsList = new();
        
        // 免疫标签集合
        private readonly HashSet<int> mImmuneTags = new();
        
        // 待移除的 Buff 列表（避免遍历时修改集合）
        private readonly List<BuffInstance> mPendingRemoveList = new();
        
        // 缓存的结果列表（避免频繁分配）
        private readonly List<BuffInstance> mTempResultList = new();

        /// <summary>
        /// 当前 Buff 实例总数
        /// </summary>
        public int Count => mAllBuffsList.Count;

        /// <summary>
        /// 容器是否已被释放
        /// </summary>
        public bool IsDisposed { get; private set; }

        #region 对象池接口

        public bool IsRecycled { get; set; }

        public void OnRecycled()
        {
            ClearInternal(triggerCallbacks: false);
            mImmuneTags.Clear();
            IsDisposed = false;
        }

        #endregion

        #region 释放接口

        public void Dispose()
        {
            if (IsDisposed) return;
            
            Clear();
            IsDisposed = true;
            
            // 返回对象池
            BuffKit.RecycleContainer(this);
        }

        #endregion

        #region 更新

        /// <summary>
        /// 更新所有 Buff 的时间
        /// </summary>
        public void Update(float deltaTime)
        {
            ThrowIfDisposed();
            
            mPendingRemoveList.Clear();
            
            for (int i = 0; i < mAllBuffsList.Count; i++)
            {
                var instance = mAllBuffsList[i];
                
                // 更新持续时间
                if (!instance.IsPermanent)
                {
                    instance.RemainingDuration -= deltaTime;
                    
                    if (instance.RemainingDuration <= 0)
                    {
                        // 触发到期回调
                        instance.Buff?.OnExpire(this, instance);
                        mPendingRemoveList.Add(instance);
                        continue;
                    }
                }
                
                // 更新 Tick
                var tickInterval = instance.Buff?.TickInterval ?? 0;
                if (tickInterval > 0)
                {
                    instance.ElapsedTickTime += deltaTime;
                    
                    while (instance.ElapsedTickTime >= tickInterval)
                    {
                        instance.ElapsedTickTime -= tickInterval;
                        instance.Buff?.OnTick(this, instance);
                    }
                }
            }
            
            // 移除到期的 Buff
            for (int i = 0; i < mPendingRemoveList.Count; i++)
            {
                RemoveInstanceInternal(mPendingRemoveList[i], BuffRemoveReason.Expired);
            }
        }

        #endregion

        #region 修改器

        /// <summary>
        /// 计算属性修改后的值
        /// </summary>
        public float GetModifiedValue(int attributeId, float baseValue)
        {
            ThrowIfDisposed();
            
            float additiveSum = 0f;
            float multiplicativeSum = 0f;
            float? overrideValue = null;
            int overridePriority = int.MinValue;
            
            // 遍历所有 Buff，收集修改器
            for (int i = 0; i < mAllBuffsList.Count; i++)
            {
                var instance = mAllBuffsList[i];
                var modifiers = instance.Buff?.Modifiers;
                if (modifiers == null) continue;
                
                int stackMultiplier = instance.StackCount;
                
                for (int j = 0; j < modifiers.Count; j++)
                {
                    var modifier = modifiers[j];
                    if (modifier.AttributeId != attributeId) continue;
                    
                    switch (modifier.Type)
                    {
                        case ModifierType.Additive:
                            additiveSum += modifier.Value * stackMultiplier;
                            break;
                            
                        case ModifierType.Multiplicative:
                            multiplicativeSum += modifier.Value * stackMultiplier;
                            break;
                            
                        case ModifierType.Override:
                            if (modifier.Priority > overridePriority)
                            {
                                overridePriority = modifier.Priority;
                                overrideValue = modifier.Value;
                            }
                            break;
                    }
                }
            }
            
            // 计算最终值：(baseValue + additive) * (1 + multiplicative)
            float result = (baseValue + additiveSum) * (1f + multiplicativeSum);
            
            // Override 优先级最高
            if (overrideValue.HasValue)
            {
                result = overrideValue.Value;
            }
            
            return result;
        }

        #endregion

        private void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(BuffContainer));
            }
        }
    }
}
