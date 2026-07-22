using System.Runtime.Serialization;

namespace YokiFrame
{
    /// <summary>
    /// Architecture 数据模型基类，复用服务生命周期并保留序列化契约。
    /// </summary>
    public abstract class AbstractModel : AbstractService, IModel
    {
        /// <summary>
        /// 写入模型序列化数据；具体模型按自身存档字段实现。
        /// </summary>
        /// <param name="info">序列化信息容器。</param>
        /// <param name="context">序列化上下文。</param>
        public abstract void GetObjectData(SerializationInfo info, StreamingContext context);
    }
}
