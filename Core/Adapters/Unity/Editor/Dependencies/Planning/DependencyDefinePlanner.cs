#if UNITY_EDITOR

using System;
using System.Collections.Generic;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 依据单次 inventory 快照纯计算 YokiFrame 依赖宏，不访问任何 Unity 全局状态。
    /// </summary>
    internal sealed class DependencyDefinePlanner
    {
        /// <summary>
        /// 创建无状态依赖宏规划器，允许测试与刷新协调器直接复用。
        /// </summary>
        public DependencyDefinePlanner()
        {
        }

        /// <summary>
        /// 保留非受管宏、清理失效及废弃宏，并生成稳定去重排序的目标计划。
        /// </summary>
        /// <param name="currentSymbols">当前构建目标的编译宏。</param>
        /// <param name="snapshot">本次刷新统一采集的依赖快照。</param>
        /// <returns>包含目标集合、增删差异和写入判定的计划。</returns>
        public DependencyDefinePlan CreatePlan(
            string[] currentSymbols,
            DependencyInventorySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var currentSequence = NormalizeCurrentSequence(currentSymbols);
            HashSet<string> currentSet = new(currentSequence, StringComparer.Ordinal);
            HashSet<string> desiredSet = new(StringComparer.Ordinal);
            AddExternalSymbols(currentSet, desiredSet);
            AddDetectedDependencySymbols(snapshot, desiredSet);

            var desiredSymbols = ToSortedArray(desiredSet);
            var addedSymbols = CollectDifference(desiredSet, currentSet);
            var removedSymbols = CollectDifference(currentSet, desiredSet);
            var changed = !SequenceEquals(currentSequence, desiredSymbols);
            return new DependencyDefinePlan(desiredSymbols, addedSymbols, removedSymbols, changed);
        }

        /// <summary>
        /// 规范化当前宏序列，移除空值但保留顺序与重复项以检测非规范状态。
        /// </summary>
        /// <param name="symbols">PlayerSettings 返回的宏数组。</param>
        /// <returns>用于稳定比较的宏序列。</returns>
        private static string[] NormalizeCurrentSequence(string[] symbols)
        {
            if (symbols == null || symbols.Length == 0)
            {
                return Array.Empty<string>();
            }

            List<string> result = new(symbols.Length);
            for (var index = 0; index < symbols.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(symbols[index]))
                {
                    result.Add(symbols[index].Trim());
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// 将所有不属于依赖服务所有权的用户或第三方宏复制到目标集合。
        /// </summary>
        /// <param name="currentSymbols">当前去重宏集合。</param>
        /// <param name="desiredSymbols">待生成的目标宏集合。</param>
        private static void AddExternalSymbols(
            HashSet<string> currentSymbols,
            HashSet<string> desiredSymbols)
        {
            foreach (var symbol in currentSymbols)
            {
                if (!DependencyDefineCatalog.IsManagedSymbol(symbol))
                {
                    desiredSymbols.Add(symbol);
                }
            }
        }

        /// <summary>
        /// 把当前快照确实命中的七组可选依赖宏加入目标集合。
        /// </summary>
        /// <param name="snapshot">本次刷新统一采集的依赖快照。</param>
        /// <param name="desiredSymbols">待生成的目标宏集合。</param>
        private static void AddDetectedDependencySymbols(
            DependencyInventorySnapshot snapshot,
            HashSet<string> desiredSymbols)
        {
            var definitions = DependencyDefineCatalog.Definitions;
            for (var index = 0; index < definitions.Length; index++)
            {
                if (definitions[index].IsDetected(snapshot))
                {
                    desiredSymbols.Add(definitions[index].DefineSymbol);
                }
            }
        }

        /// <summary>
        /// 计算来源集合相对排除集合的稳定有序差异。
        /// </summary>
        /// <param name="source">差异来源集合。</param>
        /// <param name="excludes">需要排除的集合。</param>
        /// <returns>按 Ordinal 排序的差异数组。</returns>
        private static string[] CollectDifference(
            HashSet<string> source,
            HashSet<string> excludes)
        {
            List<string> result = new();
            foreach (var symbol in source)
            {
                if (!excludes.Contains(symbol))
                {
                    result.Add(symbol);
                }
            }

            result.Sort(StringComparer.Ordinal);
            return result.ToArray();
        }

        /// <summary>
        /// 将宏集合转换为可重复写入 PlayerSettings 的稳定有序数组。
        /// </summary>
        /// <param name="symbols">待排序的宏集合。</param>
        /// <returns>按 Ordinal 排序的宏数组。</returns>
        private static string[] ToSortedArray(HashSet<string> symbols)
        {
            var result = new string[symbols.Count];
            symbols.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        /// <summary>
        /// 比较当前序列与规范目标序列，确保重复项或顺序漂移也会被一次写回收口。
        /// </summary>
        /// <param name="current">当前宏序列。</param>
        /// <param name="desired">规范目标序列。</param>
        /// <returns>两组序列逐项完全相同时返回 true。</returns>
        private static bool SequenceEquals(string[] current, string[] desired)
        {
            if (current.Length != desired.Length)
            {
                return false;
            }

            for (var index = 0; index < current.Length; index++)
            {
                if (!string.Equals(current[index], desired[index], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

#endif
