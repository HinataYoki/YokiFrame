using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// Buff 容器，管理单个实体身上的所有 Buff
    /// </summary>
    public class BuffContainer : IPoolable, IDisposable
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

        #region 添加

        /// <summary>
        /// 通过 BuffId 添加 Buff（从注册的 BuffData 查找）
        /// </summary>
        public bool Add(int buffId)
        {
            ThrowIfDisposed();
            
            var data = BuffKit.GetBuffData(buffId);
            if (data.BuffId == 0 && buffId != 0)
            {
                // BuffData 未注册
                return false;
            }
            
            return AddInternal(data, null);
        }

        /// <summary>
        /// 通过 BuffId 添加指定层数的 Buff
        /// </summary>
        public bool Add(int buffId, int stackCount)
        {
            ThrowIfDisposed();
            
            var data = BuffKit.GetBuffData(buffId);
            if (data.BuffId == 0 && buffId != 0)
            {
                return false;
            }
            
            // 对于 Stack 模式，多次添加
            if (data.StackMode == StackMode.Stack)
            {
                bool anyAdded = false;
                for (int i = 0; i < stackCount; i++)
                {
                    if (AddInternal(data, null))
                    {
                        anyAdded = true;
                    }
                }
                return anyAdded;
            }
            
            return AddInternal(data, null);
        }

        /// <summary>
        /// 通过 BuffData 添加 Buff
        /// </summary>
        public bool Add(BuffData data)
        {
            ThrowIfDisposed();
            return AddInternal(data, null);
        }

        /// <summary>
        /// 通过 IBuff 实例添加 Buff
        /// </summary>
        public bool Add(IBuff buff)
        {
            ThrowIfDisposed();
            
            if (buff == null) return false;
            
            var data = new BuffData
            {
                BuffId = buff.BuffId,
                Duration = buff.Duration,
                MaxStack = buff.MaxStack,
                StackMode = buff.StackMode,
                TickInterval = buff.TickInterval,
                Tags = buff.Tags,
                ExclusionTags = buff.ExclusionTags
            };
            
            return AddInternal(data, buff);
        }

        private bool AddInternal(BuffData data, IBuff buff)
        {
            // 检查免疫
            if (data.Tags != null)
            {
                for (int i = 0; i < data.Tags.Length; i++)
                {
                    if (mImmuneTags.Contains(data.Tags[i]))
                    {
                        return false;
                    }
                }
            }
            
            // 处理排斥标签
            if (data.ExclusionTags != null && data.ExclusionTags.Length > 0)
            {
                for (int i = 0; i < data.ExclusionTags.Length; i++)
                {
                    RemoveByTag(data.ExclusionTags[i], BuffRemoveReason.Excluded);
                }
            }
            
            // 根据堆叠模式处理
            switch (data.StackMode)
            {
                case StackMode.Independent:
                    return AddNewInstance(data, buff);
                    
                case StackMode.Refresh:
                    return AddOrRefresh(data, buff);
                    
                case StackMode.Stack:
                    return AddOrStack(data, buff);
                    
                default:
                    return AddNewInstance(data, buff);
            }
        }

        private bool AddNewInstance(BuffData data, IBuff buff)
        {
            var instance = CreateInstance(data, buff);
            AddToCollections(instance);
            InvokeOnAdd(instance);
            return true;
        }

        private bool AddOrRefresh(BuffData data, IBuff buff)
        {
            if (mBuffsByIdDict.TryGetValue(data.BuffId, out var list) && list.Count > 0)
            {
                // 刷新现有实例
                var instance = list[0];
                instance.RefreshDuration();
                return true;
            }
            
            // 创建新实例
            return AddNewInstance(data, buff);
        }

        private bool AddOrStack(BuffData data, IBuff buff)
        {
            if (mBuffsByIdDict.TryGetValue(data.BuffId, out var list) && list.Count > 0)
            {
                var instance = list[0];
                
                if (instance.StackCount < data.MaxStack)
                {
                    // 增加堆叠
                    int oldStack = instance.StackCount;
                    instance.StackCount++;
                    instance.RefreshDuration();
                    InvokeOnStackChanged(instance, oldStack, instance.StackCount);
                    return true;
                }
                else
                {
                    // 已达最大堆叠，只刷新时间
                    instance.RefreshDuration();
                    return true;
                }
            }
            
            // 创建新实例
            return AddNewInstance(data, buff);
        }

        private BuffInstance CreateInstance(BuffData data, IBuff buff)
        {
            var instance = BuffKit.AllocateInstance();
            instance.Initialize(data.BuffId, buff, data.Duration, 1);
            return instance;
        }

        private void AddToCollections(BuffInstance instance)
        {
            if (!mBuffsByIdDict.TryGetValue(instance.BuffId, out var list))
            {
                list = new List<BuffInstance>();
                mBuffsByIdDict[instance.BuffId] = list;
            }
            list.Add(instance);
            mAllBuffsList.Add(instance);
        }

        private void InvokeOnAdd(BuffInstance instance)
        {
            instance.Buff?.OnAdd(this, instance);
            
            var effects = instance.Buff?.Effects;
            if (effects != null)
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    effects[i].OnApply(this, instance);
                }
            }
            
            // 触发事件
            EventKit.Type.Send(new BuffAddedEvent { Container = this, Instance = instance });
        }

        private void InvokeOnStackChanged(BuffInstance instance, int oldStack, int newStack)
        {
            instance.Buff?.OnStackChanged(this, instance, oldStack, newStack);
            
            var effects = instance.Buff?.Effects;
            if (effects != null)
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    effects[i].OnStackChanged(this, instance, oldStack, newStack);
                }
            }
            
            // 触发事件
            EventKit.Type.Send(new BuffStackChangedEvent 
            { 
                Container = this, 
                Instance = instance, 
                OldStack = oldStack, 
                NewStack = newStack 
            });
        }

        #endregion

        #region 移除

        /// <summary>
        /// 移除指定 BuffId 的所有实例
        /// </summary>
        public bool Remove(int buffId)
        {
            ThrowIfDisposed();
            
            if (!mBuffsByIdDict.TryGetValue(buffId, out var list) || list.Count == 0)
            {
                return false;
            }
            
            // 复制列表避免遍历时修改
            mTempResultList.Clear();
            mTempResultList.AddRange(list);
            
            for (int i = 0; i < mTempResultList.Count; i++)
            {
                RemoveInstanceInternal(mTempResultList[i], BuffRemoveReason.Manual);
            }
            
            return true;
        }

        /// <summary>
        /// 移除指定的 Buff 实例
        /// </summary>
        public bool RemoveInstance(BuffInstance instance)
        {
            ThrowIfDisposed();
            
            if (instance == null || !mAllBuffsList.Contains(instance))
            {
                return false;
            }
            
            RemoveInstanceInternal(instance, BuffRemoveReason.Manual);
            return true;
        }

        /// <summary>
        /// 移除所有带有指定标签的 Buff
        /// </summary>
        public int RemoveByTag(int tag)
        {
            return RemoveByTag(tag, BuffRemoveReason.Manual);
        }

        private int RemoveByTag(int tag, BuffRemoveReason reason)
        {
            ThrowIfDisposed();
            
            mTempResultList.Clear();
            
            for (int i = 0; i < mAllBuffsList.Count; i++)
            {
                var instance = mAllBuffsList[i];
                var tags = instance.Buff?.Tags;
                if (tags != null)
                {
                    for (int j = 0; j < tags.Length; j++)
                    {
                        if (tags[j] == tag)
                        {
                            mTempResultList.Add(instance);
                            break;
                        }
                    }
                }
            }
            
            int count = mTempResultList.Count;
            for (int i = 0; i < count; i++)
            {
                RemoveInstanceInternal(mTempResultList[i], reason);
            }
            
            return count;
        }

        /// <summary>
        /// 清除所有 Buff
        /// </summary>
        public void Clear()
        {
            ThrowIfDisposed();
            ClearInternal(triggerCallbacks: true);
        }

        private void ClearInternal(bool triggerCallbacks)
        {
            if (triggerCallbacks)
            {
                // 复制列表避免遍历时修改
                mTempResultList.Clear();
                mTempResultList.AddRange(mAllBuffsList);
                
                for (int i = 0; i < mTempResultList.Count; i++)
                {
                    RemoveInstanceInternal(mTempResultList[i], BuffRemoveReason.Cleared);
                }
            }
            else
            {
                // 直接清理，不触发回调
                for (int i = 0; i < mAllBuffsList.Count; i++)
                {
                    BuffKit.RecycleInstance(mAllBuffsList[i]);
                }
                mAllBuffsList.Clear();
                mBuffsByIdDict.Clear();
            }
        }

        private void RemoveInstanceInternal(BuffInstance instance, BuffRemoveReason reason)
        {
            // 从集合中移除
            mAllBuffsList.Remove(instance);
            
            if (mBuffsByIdDict.TryGetValue(instance.BuffId, out var list))
            {
                list.Remove(instance);
                if (list.Count == 0)
                {
                    mBuffsByIdDict.Remove(instance.BuffId);
                }
            }
            
            // 触发回调
            InvokeOnRemove(instance, reason);
            
            // 回收实例
            BuffKit.RecycleInstance(instance);
        }

        private void InvokeOnRemove(BuffInstance instance, BuffRemoveReason reason)
        {
            instance.Buff?.OnRemove(this, instance);
            
            var effects = instance.Buff?.Effects;
            if (effects != null)
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    effects[i].OnRemove(this, instance);
                }
            }
            
            // 触发事件
            EventKit.Type.Send(new BuffRemovedEvent 
            { 
                Container = this, 
                Instance = instance, 
                Reason = reason 
            });
        }

        #endregion

        #region 查询

        /// <summary>
        /// 检查是否存在指定 BuffId 的 Buff
        /// </summary>
        public bool Has(int buffId)
        {
            ThrowIfDisposed();
            return mBuffsByIdDict.TryGetValue(buffId, out var list) && list.Count > 0;
        }

        /// <summary>
        /// 获取指定 BuffId 的第一个实例
        /// </summary>
        public BuffInstance Get(int buffId)
        {
            ThrowIfDisposed();
            
            if (mBuffsByIdDict.TryGetValue(buffId, out var list) && list.Count > 0)
            {
                return list[0];
            }
            return null;
        }

        /// <summary>
        /// 获取指定 BuffId 的所有实例
        /// </summary>
        public void GetAll(int buffId, List<BuffInstance> results)
        {
            ThrowIfDisposed();
            
            results.Clear();
            if (mBuffsByIdDict.TryGetValue(buffId, out var list))
            {
                results.AddRange(list);
            }
        }

        /// <summary>
        /// 获取所有带有指定标签的 Buff 实例
        /// </summary>
        public void GetByTag(int tag, List<BuffInstance> results)
        {
            ThrowIfDisposed();
            
            results.Clear();
            for (int i = 0; i < mAllBuffsList.Count; i++)
            {
                var instance = mAllBuffsList[i];
                var tags = instance.Buff?.Tags;
                if (tags != null)
                {
                    for (int j = 0; j < tags.Length; j++)
                    {
                        if (tags[j] == tag)
                        {
                            results.Add(instance);
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 获取指定 BuffId 的总堆叠数
        /// </summary>
        public int GetStackCount(int buffId)
        {
            ThrowIfDisposed();
            
            if (!mBuffsByIdDict.TryGetValue(buffId, out var list))
            {
                return 0;
            }
            
            int total = 0;
            for (int i = 0; i < list.Count; i++)
            {
                total += list[i].StackCount;
            }
            return total;
        }

        #endregion

        #region 免疫

        /// <summary>
        /// 添加免疫标签
        /// </summary>
        public void AddImmunity(int tag)
        {
            ThrowIfDisposed();
            mImmuneTags.Add(tag);
        }

        /// <summary>
        /// 移除免疫标签
        /// </summary>
        public void RemoveImmunity(int tag)
        {
            ThrowIfDisposed();
            mImmuneTags.Remove(tag);
        }

        /// <summary>
        /// 检查是否免疫指定标签
        /// </summary>
        public bool IsImmune(int tag)
        {
            ThrowIfDisposed();
            return mImmuneTags.Contains(tag);
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

        #region 序列化

        /// <summary>
        /// 导出为可序列化的数据
        /// </summary>
        public BuffContainerSaveData ToSaveData()
        {
            ThrowIfDisposed();
            
            var instances = new BuffInstanceSaveData[mAllBuffsList.Count];
            for (int i = 0; i < mAllBuffsList.Count; i++)
            {
                var instance = mAllBuffsList[i];
                instances[i] = new BuffInstanceSaveData
                {
                    BuffId = instance.BuffId,
                    RemainingDuration = instance.RemainingDuration,
                    StackCount = instance.StackCount,
                    ElapsedTickTime = instance.ElapsedTickTime
                };
            }
            
            var immuneTags = new int[mImmuneTags.Count];
            mImmuneTags.CopyTo(immuneTags);
            
            return new BuffContainerSaveData
            {
                Instances = instances,
                ImmuneTags = immuneTags
            };
        }

        /// <summary>
        /// 从序列化数据恢复
        /// </summary>
        public void FromSaveData(BuffContainerSaveData data)
        {
            ThrowIfDisposed();
            
            // 清除现有数据
            ClearInternal(triggerCallbacks: false);
            
            // 恢复免疫标签
            if (data.ImmuneTags != null)
            {
                for (int i = 0; i < data.ImmuneTags.Length; i++)
                {
                    mImmuneTags.Add(data.ImmuneTags[i]);
                }
            }
            
            // 恢复 Buff 实例
            if (data.Instances != null)
            {
                for (int i = 0; i < data.Instances.Length; i++)
                {
                    var saveData = data.Instances[i];
                    var buffData = BuffKit.GetBuffData(saveData.BuffId);
                    
                    if (buffData.BuffId == 0 && saveData.BuffId != 0)
                    {
                        // BuffData 未注册，跳过
                        continue;
                    }
                    
                    var instance = BuffKit.AllocateInstance();
                    instance.Initialize(saveData.BuffId, null, saveData.RemainingDuration, saveData.StackCount);
                    instance.ElapsedTickTime = saveData.ElapsedTickTime;
                    instance.OriginalDuration = buffData.Duration;
                    
                    AddToCollections(instance);
                }
            }
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
