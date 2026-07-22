#if UNITY_EDITOR

using System;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 编排单次 inventory、宏读取、纯规划和必要写入，隔离 Unity 全局 API。
    /// </summary>
    internal sealed class DependencyDefineRefreshCoordinator
    {
        private readonly Func<DependencyInventorySnapshot> mSnapshotProvider;
        private readonly Func<string[]> mReadSymbols;
        private readonly Action<string[]> mWriteSymbols;
        private readonly Func<string> mBuildTargetProvider;
        private readonly DependencyDefinePlanner mPlanner = new();

        /// <summary>
        /// 创建刷新协调器，并要求三个边界回调都明确提供。
        /// </summary>
        /// <param name="snapshotProvider">每次刷新只调用一次的 inventory 采集器。</param>
        /// <param name="readSymbols">当前构建目标宏读取器。</param>
        /// <param name="writeSymbols">目标宏写入器。</param>
        public DependencyDefineRefreshCoordinator(
            Func<DependencyInventorySnapshot> snapshotProvider,
            Func<string[]> readSymbols,
            Action<string[]> writeSymbols)
            : this(snapshotProvider, readSymbols, writeSymbols, () => string.Empty)
        {
        }

        /// <summary>
        /// 创建带构建目标诊断的刷新协调器，确保 Console 能定位宏写入所属平台。
        /// </summary>
        /// <param name="snapshotProvider">每次刷新只调用一次的 inventory 采集器。</param>
        /// <param name="readSymbols">当前构建目标宏读取器。</param>
        /// <param name="writeSymbols">目标宏写入器。</param>
        /// <param name="buildTargetProvider">当前 Unity 构建目标读取器。</param>
        public DependencyDefineRefreshCoordinator(
            Func<DependencyInventorySnapshot> snapshotProvider,
            Func<string[]> readSymbols,
            Action<string[]> writeSymbols,
            Func<string> buildTargetProvider)
        {
            mSnapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
            mReadSymbols = readSymbols ?? throw new ArgumentNullException(nameof(readSymbols));
            mWriteSymbols = writeSymbols ?? throw new ArgumentNullException(nameof(writeSymbols));
            mBuildTargetProvider = buildTargetProvider ?? throw new ArgumentNullException(nameof(buildTargetProvider));
        }

        /// <summary>
        /// 执行一次刷新；inventory 失败时保证不读取也不写入 PlayerSettings。
        /// </summary>
        /// <returns>包含成功、写入状态和诊断信息的结果。</returns>
        public DependencyDefineRefreshResult Refresh()
        {
            var buildTarget = mBuildTargetProvider();
            DependencyInventorySnapshot snapshot;
            try
            {
                snapshot = mSnapshotProvider();
                if (snapshot == null)
                {
                    throw new InvalidOperationException("依赖 inventory 未返回有效快照。");
                }
            }
            catch (Exception exception)
            {
                return new DependencyDefineRefreshResult(
                    false,
                    false,
                    "依赖 inventory 采集失败: " + exception.Message,
                    buildTarget,
                    null,
                    null);
            }

            try
            {
                var plan = mPlanner.CreatePlan(mReadSymbols(), snapshot);
                if (plan.Changed)
                {
                    mWriteSymbols(plan.DesiredSymbols);
                }

                return new DependencyDefineRefreshResult(
                    true,
                    plan.Changed,
                    string.Empty,
                    buildTarget,
                    snapshot,
                    plan);
            }
            catch (Exception exception)
            {
                return new DependencyDefineRefreshResult(
                    false,
                    false,
                    "依赖宏读取或写入失败: " + exception.Message,
                    buildTarget,
                    snapshot,
                    null);
            }
        }
    }
}

#endif
