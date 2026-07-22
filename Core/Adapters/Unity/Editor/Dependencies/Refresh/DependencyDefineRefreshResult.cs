#if UNITY_EDITOR

using System;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 表示一次依赖宏刷新是否成功、是否写入以及可诊断错误。
    /// </summary>
    internal sealed class DependencyDefineRefreshResult
    {
        /// <summary>
        /// 创建刷新结果；失败结果必须携带错误信息，成功结果使用空字符串。
        /// </summary>
        /// <param name="succeeded">刷新流程是否完整成功。</param>
        /// <param name="changed">是否已写入新的宏集合。</param>
        /// <param name="errorMessage">失败诊断信息。</param>
        /// <param name="buildTarget">本轮刷新对应的 Unity 构建目标。</param>
        /// <param name="snapshot">本轮唯一 inventory 快照；采集失败时为空。</param>
        /// <param name="plan">本轮宏规划；读取或写入失败时为空。</param>
        internal DependencyDefineRefreshResult(
            bool succeeded,
            bool changed,
            string errorMessage,
            string buildTarget,
            DependencyInventorySnapshot snapshot,
            DependencyDefinePlan plan)
        {
            Succeeded = succeeded;
            Changed = changed;
            ErrorMessage = errorMessage;
            BuildTarget = buildTarget ?? string.Empty;
            Snapshot = snapshot;
            Plan = plan;
        }

        /// <summary>
        /// 获取刷新流程是否完整成功。
        /// </summary>
        public bool Succeeded { get; }

        /// <summary>
        /// 获取本次刷新是否实际写入宏集合。
        /// </summary>
        public bool Changed { get; }

        /// <summary>
        /// 获取失败诊断；成功时为空字符串。
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// 获取本轮刷新对应的 Unity 构建目标。
        /// </summary>
        public string BuildTarget { get; }

        /// <summary>
        /// 获取本轮唯一 inventory 快照；采集失败时为空。
        /// </summary>
        public DependencyInventorySnapshot Snapshot { get; }

        /// <summary>
        /// 获取本轮宏规划；读取或写入失败时为空。
        /// </summary>
        public DependencyDefinePlan Plan { get; }

        /// <summary>
        /// 获取 inventory 中已隔离的单文件诊断；采集失败时返回空数组。
        /// </summary>
        public string[] InventoryDiagnostics => Snapshot == null ? Array.Empty<string>() : Snapshot.Diagnostics;
    }
}

#endif
