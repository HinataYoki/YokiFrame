using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// BuffContainer - 查询和免疫逻辑
    /// </summary>
    public partial class BuffContainer
    {
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
    }
}
