#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// PoolKit monitor documentation.
    /// </summary>
    internal static class PoolKitDocMonitor
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "Pool Monitor",
                Description = "Runtime object-pool monitor with pool health visualization, active-object tracking, and event diagnostics.",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "Enable Tracking",
                        Code = @"PoolDebugger.EnableTracking = true;
PoolDebugger.EnableStackTrace = true;",
                        Explanation = "Tracking is editor-oriented and should be enabled only when the diagnostics are needed."
                    },
                    new()
                    {
                        Title = "Read Pool Debug Info",
                        Code = @"var pools = new List<PoolDebugInfo>();
PoolDebugger.GetAllPools(pools);

foreach (var pool in pools)
{
    Debug.Log($""Pool: {pool.Name}"");
    Debug.Log($""Usage: {pool.UsageRate:P0}"");
    Debug.Log($""Health: {pool.HealthStatus}"");
}",
                        Explanation = "PoolDebugInfo exposes the runtime snapshot used by the monitor page."
                    },
                    new()
                    {
                        Title = "Health Model",
                        Code = @"// Healthy: usage below 50%
// Normal:  usage between 50% and 80%
// Busy:    usage above 80%
//
// usageRate = activeCount / totalCount",
                        Explanation = "Pool health is derived from usage rate so hot pools are easy to spot quickly."
                    },
                    new()
                    {
                        Title = "Reactive Architecture",
                        Code = @"// PoolDebugger
//   -> EditorDataBridge.NotifyDataChanged()
//   -> PoolKitToolPage subscriptions
//   -> ViewModel / page state
//   -> UI refresh
//
// Shared channels:
// DataChannels.POOL_LIST_CHANGED
// DataChannels.POOL_ACTIVE_CHANGED
// DataChannels.POOL_EVENT_LOGGED",
                        Explanation = "PoolKit uses shared editor channels for structural data refresh and still keeps timer-based refresh where elapsed time is continuously changing."
                    },
                    new()
                    {
                        Title = "Custom Editor Subscription",
                        Code = @"#if UNITY_EDITOR
using YokiFrame.EditorTools;

var subscription = EditorDataBridge.Subscribe<PoolDebugInfo>(
    DataChannels.POOL_LIST_CHANGED,
    pool =>
    {
        if (pool.HealthStatus == PoolHealthStatus.Busy)
        {
            Debug.LogWarning($""Pool {pool.Name} is busy."");
        }
    });

subscription.Dispose();
#endif",
                        Explanation = "Custom editor tooling can react to the shared PoolKit monitor channels."
                    }
                }
            };
        }
    }
}
#endif
