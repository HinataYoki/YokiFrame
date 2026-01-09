#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Architecture 使用架构文档
    /// </summary>
    internal static class ArchitectureDocUsage
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "使用架构",
                Description = "通过 Architecture.Interface 访问架构实例，获取服务进行业务操作。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "获取服务",
                        Code = @"// 获取服务实例
var playerService = GameArchitecture.Interface.GetService<PlayerService>();
playerService.AddExp(100);

// 未注册的服务返回 null
var service = GameArchitecture.Interface.GetService<SomeService>();
if (service == null)
{
    Debug.LogWarning(""服务未注册"");
}

// force 参数：未注册时自动创建并注册
var autoService = GameArchitecture.Interface.GetService<SomeService>(force: true);

// 获取所有服务（用于调试或批量操作）
foreach (var svc in GameArchitecture.Interface.GetAllServices())
{
    Debug.Log(svc.GetType().Name);
}",
                        Explanation = "GetService 返回已注册的服务实例，未注册时返回 null。"
                    }
                }
            };
        }
    }
}
#endif
