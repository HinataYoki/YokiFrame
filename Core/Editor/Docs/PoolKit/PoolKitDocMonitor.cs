#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// PoolKit 监控面板文档
    /// </summary>
    internal static class PoolKitDocMonitor
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "监控面板（PoolKit Monitor）",
                Description = "运行时对象池监控工具，提供池状态可视化、活跃对象追踪、代码定位等调试功能。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "启用追踪功能",
                        Code = @"// 启用对象追踪（默认开启）
PoolDebugger.EnableTracking = true;

// 启用堆栈追踪（性能开销较大，按需开启）
PoolDebugger.EnableStackTrace = true;",
                        Explanation = "追踪功能仅在编辑器模式下生效，不影响发布版本性能。"
                    },
                    new()
                    {
                        Title = "获取池调试信息",
                        Code = @"// 获取所有池的调试信息
var pools = new List<PoolDebugInfo>();
PoolDebugger.GetAllPools(pools);

foreach (var pool in pools)
{
    Debug.Log($""池: {pool.Name}"");
    Debug.Log($""  使用率: {pool.UsageRate:P0}"");
    Debug.Log($""  健康状态: {pool.HealthStatus}"");
    Debug.Log($""  使用中: {pool.ActiveCount}"");
    Debug.Log($""  池内: {pool.InactiveCount}"");
    Debug.Log($""  容量: {(pool.MaxCacheCount < 0 ? ""无限制"" : pool.MaxCacheCount.ToString())}"");
    Debug.Log($""  峰值: {pool.PeakCount}"");
    
    // 遍历活跃对象
    foreach (var obj in pool.ActiveObjects)
    {
        Debug.Log($""  活跃对象: {obj.Obj}"");
        if (!string.IsNullOrEmpty(obj.StackTrace))
        {
            Debug.Log($""    来源: {obj.StackTrace}"");
        }
    }
}",
                        Explanation = "PoolDebugInfo 包含池的完整运行时状态，可用于自定义监控逻辑。"
                    },
                    new()
                    {
                        Title = "健康状态说明",
                        Code = @"// PoolHealthStatus 枚举值说明
// Healthy  - 使用率 < 50%，池状态健康（蓝色）
// Normal   - 使用率 50% - 80%，正常运行（灰色）
// Busy     - 使用率 > 80%，池较繁忙（橙色）

// 使用率计算公式
float usageRate = (float)activeCount / totalCount;

// 数字显示格式
// 使用 X / 池内 Y / 容量 Z
// - 使用：当前借出的对象数量
// - 池内：池中待机的对象数量
// - 容量：池的最大缓存容量（∞ 表示无限制）",
                        Explanation = "健康状态根据使用率自动计算，通过颜色快速识别需要优化的池。"
                    },
                    new()
                    {
                        Title = "代码定位功能",
                        Code = @"// 点击活跃对象卡片上的""代码""按钮，可跳转到对象借出的代码位置
// 支持两种堆栈格式：
// 1. Unity 格式：at ClassName.Method () [0x00000] in /path/file.cs:123
// 2. Mono 格式：ClassName.Method () [0x00000] in F:\path\file.cs:123

// 堆栈追踪需要启用
PoolDebugger.EnableStackTrace = true;

// 堆栈会自动过滤 Unity 引擎和框架代码，定位到项目代码位置",
                        Explanation = "代码定位功能帮助快速找到对象借出的代码位置，便于排查对象未归还的问题。"
                    },
                    new()
                    {
                        Title = "打开监控面板",
                        Code = @"// 方式 1：通过菜单
// Tools > YokiFrame > YokiFrame Tools > PoolKit

// 方式 2：快捷键
// Ctrl+Shift+Y 打开 YokiFrame Tools，切换到 PoolKit 标签页",
                        Explanation = "监控面板集成在 YokiFrame Tools 主工具窗口中。"
                    },
                    new()
                    {
                        Title = "响应式架构说明",
                        Code = @"// PoolKit 监控面板采用响应式架构，数据变化自动触发 UI 更新
// 核心组件：
// - PoolKitViewModel: 响应式数据模型
// - EditorDataBridge: 编辑器数据通道
// - ReactiveProperty<T>: 响应式属性
// - ReactiveCollection<T>: 响应式集合

// 数据流：
// PoolDebugger (运行时) 
//   → EditorDataBridge.NotifyDataChanged() 
//   → PoolKitToolPage 订阅回调
//   → ViewModel 更新
//   → UI 自动刷新

// 通道定义（DataChannels.cs）：
// CHANNEL_POOL_LIST_CHANGED   - 池列表变化
// CHANNEL_POOL_ACTIVE_CHANGED - 活跃对象变化
// CHANNEL_POOL_EVENT_LOGGED   - 事件日志追加",
                        Explanation = "响应式架构替代了传统的 OnUpdate 轮询模式，仅在数据变化时触发 UI 更新，显著降低 CPU 占用。"
                    },
                    new()
                    {
                        Title = "自定义监控扩展",
                        Code = @"// 订阅池数据变化（编辑器代码）
#if UNITY_EDITOR
using YokiFrame.EditorTools;

// 订阅池列表变化
var subscription = EditorDataBridge.Subscribe<List<PoolDebugInfo>>(
    DataChannels.CHANNEL_POOL_LIST_CHANGED,
    pools => 
    {
        foreach (var pool in pools)
        {
            if (pool.HealthStatus == PoolHealthStatus.Busy)
            {
                Debug.LogWarning($""池 {pool.Name} 使用率过高！"");
            }
        }
    });

// 取消订阅
subscription.Dispose();
#endif",
                        Explanation = "可通过 EditorDataBridge 订阅池数据变化，实现自定义监控逻辑。"
                    }
                }
            };
        }
    }
}
#endif
