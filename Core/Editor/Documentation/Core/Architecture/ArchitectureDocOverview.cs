#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Architecture 模块的概览章节。
    /// </summary>
    internal static class ArchitectureDocOverview
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "概览",
                Description = "Architecture 是 YokiFrame 的基础。它负责管理服务与模型、协调它们的生命周期，并通过清晰的注册与获取规则维持框架的模块化。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "核心契约",
                        Code = @"// IArchitecture
public interface IArchitecture : ICanInit
{
    static IArchitecture Interface { get; }
    void Register<T>(T service) where T : class, IService, new();
    T GetService<T>(bool force = false) where T : class, IService, new();
    IEnumerable<IService> GetAllServices();
}

// IService
public interface IService : ICanInit
{
    IArchitecture Architecture { get; }
    void SetArchitecture(IArchitecture architecture);
}

// IModel
public interface IModel : IService, ISerializable { }

// ICanInit
public interface ICanInit : IDisposable
{
    bool Initialized { get; }
    void Init();
}",
                        Explanation = "Architecture 通过少量稳定契约完成服务注册、获取与生命周期管理。"
                    }
                }
            };
        }
    }
}
#endif
