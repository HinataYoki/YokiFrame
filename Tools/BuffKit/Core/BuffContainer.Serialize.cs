namespace YokiFrame
{
    /// <summary>
    /// BuffContainer - 序列化逻辑
    /// </summary>
    public partial class BuffContainer
    {
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
    }
}
