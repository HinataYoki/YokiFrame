using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// BuffContainer - 添加 Buff 逻辑
    /// </summary>
    public partial class BuffContainer
    {
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

            // 优先从注册表查询 BuffData（保留 StrengthComparer 等配置）
            var data = BuffKit.GetBuffData(buff.BuffId);

            // 未注册时构造临时 BuffData
            if (data.BuffId == 0 && buff.BuffId != 0)
            {
                data = new BuffData
                {
                    BuffId = buff.BuffId,
                    Duration = buff.Duration,
                    MaxStack = buff.MaxStack,
                    StackMode = buff.StackMode,
                    TickInterval = buff.TickInterval,
                    Tags = buff.Tags,
                    ExclusionTags = buff.ExclusionTags
                };
            }

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

                case StackMode.StrongestWins:
                    return AddOrStrongest(data, buff);

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

        private bool AddOrStrongest(BuffData data, IBuff buff)
        {
            // 没有现存实例 → 直接新建
            if (!mBuffsByIdDict.TryGetValue(data.BuffId, out var list) || list.Count == 0)
            {
                return AddNewInstance(data, buff);
            }

            var existing = list[0];
            var comparer = data.StrengthComparer;

            // 未提供比较器 → 回退为 Refresh
            if (comparer == null)
            {
                existing.RefreshDuration();
                return true;
            }

            // 构造临时 candidate 用于比较（不加入集合/不触发回调）
            var candidate = BuffKit.AllocateInstance();
            candidate.Initialize(data.BuffId, buff, data.Duration, 1);

            var result = comparer(candidate, existing);

            if (result > 0)
            {
                // 新更强：移除旧（Excluded），启用 candidate
                RemoveInstanceInternal(existing, BuffRemoveReason.Excluded);
                AddToCollections(candidate);
                InvokeOnAdd(candidate);
                return true;
            }

            if (result == 0)
            {
                // 强度相同：刷新旧实例，丢弃 candidate
                existing.RefreshDuration();
                BuffKit.RecycleInstance(candidate);
                return true;
            }

            // 旧更强：丢弃 candidate
            BuffKit.RecycleInstance(candidate);
            return false;
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
    }
}
