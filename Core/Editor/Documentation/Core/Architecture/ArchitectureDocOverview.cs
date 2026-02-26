#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Architecture 概述文档
    /// </summary>
    internal static class ArchitectureDocOverview
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "概述",
                Description = "Architecture 是整个框架的基础，负责管理所有服务（Service）和数据模型（Model）的生命周期。服务通过依赖注入实现松耦合调用。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "核心接口",
                        Code = @"// IArchitecture - 架构接口
public interface IArchitecture : ICanInit
{
    static IArchitecture Interface { get; }
    void Register<T>(T service) where T : class, IService, new();
    T GetService<T>(bool force = false) where T : class, IService, new();
    IEnumerable<IService> GetAllServices();
}

// IService - 服务接口
public interface IService : ICanInit
{
    IArchitecture Architecture { get; }
    void SetArchitecture(IArchitecture architecture);
}

// IModel - 数据模型接口（支持序列化）
public interface IModel : IService, ISerializable { }

// ICanInit - 初始化生命周期接口
public interface ICanInit : IDisposable
{
    bool Initialized { get; }
    void Init();
}",
                        Explanation = "Architecture 提供服务注册、获取和生命周期管理。"
                    }
                }
            };
        }
    }
}
#endif
