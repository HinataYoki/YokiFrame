namespace YokiFrame
{
    /// <summary>
    /// BuffContainer - 移除 Buff 逻辑
    /// </summary>
    public partial class BuffContainer
    {
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
    }
}
