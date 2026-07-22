#if UNITY_EDITOR

namespace YokiFrame.Unity
{
    /// <summary>
    /// 表示一次依赖宏计算的稳定目标集合与增删差异。
    /// </summary>
    internal sealed class DependencyDefinePlan
    {
        /// <summary>
        /// 创建可供 PlayerSettings 写入和诊断使用的宏变更计划。
        /// </summary>
        /// <param name="desiredSymbols">去重并按序排列后的目标宏。</param>
        /// <param name="addedSymbols">相对当前状态新增的宏。</param>
        /// <param name="removedSymbols">相对当前状态移除的宏。</param>
        /// <param name="changed">是否需要写回 PlayerSettings。</param>
        internal DependencyDefinePlan(
            string[] desiredSymbols,
            string[] addedSymbols,
            string[] removedSymbols,
            bool changed)
        {
            DesiredSymbols = desiredSymbols;
            AddedSymbols = addedSymbols;
            RemovedSymbols = removedSymbols;
            Changed = changed;
        }

        /// <summary>
        /// 获取去重并稳定排序后的完整目标宏。
        /// </summary>
        public string[] DesiredSymbols { get; }

        /// <summary>
        /// 获取相对当前状态新增的宏。
        /// </summary>
        public string[] AddedSymbols { get; }

        /// <summary>
        /// 获取相对当前状态移除的宏。
        /// </summary>
        public string[] RemovedSymbols { get; }

        /// <summary>
        /// 获取是否需要把目标宏写回 PlayerSettings。
        /// </summary>
        public bool Changed { get; }
    }
}

#endif
