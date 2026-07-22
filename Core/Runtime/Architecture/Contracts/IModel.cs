using System.Runtime.Serialization;

namespace YokiFrame
{
    /// <summary>
    /// 定义 Architecture 中的数据模型服务契约，保留旧版序列化约定。
    /// </summary>
    public interface IModel : IService, ISerializable
    {
    }
}
