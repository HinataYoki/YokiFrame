#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// PoolKit 监控调试文档
    /// </summary>
    internal static class PoolKitDocMonitor
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "对象池监控",
                Description = "通过 PoolDebugger 在编辑器中监控对象池的运行状态。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "启用调试追踪",
                        Code = @"PoolDebugger.EnableTracking = true;
PoolDebugger.EnableStackTrace = true;",
                        Explanation = "调试追踪仅在需要诊断时开启，避免性能开销。"
                    },
                    new()
                    {
                        Title = "读取池调试信息",
                        Code = @"var pools = new List<PoolDebugInfo>();
PoolDebugger.GetAllPools(pools);

foreach (var pool in pools)
{
    Debug.Log($""池: {pool.Name}"");
    Debug.Log($""使用率: {pool.UsageRate:P0}"");
    Debug.Log($""健康状态: {pool.HealthStatus}"");
}",
                        Explanation = "PoolDebugInfo 提供运行时的池快照，包含活跃数、总量、峰值、使用率和健康状态。"
                    }
                }
            };
        }
    }
}
#endif
