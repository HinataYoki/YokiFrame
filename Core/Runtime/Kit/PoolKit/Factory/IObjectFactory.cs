namespace YokiFrame
{
    /// <summary>
    /// 对象工厂接口
    /// </summary>
    public interface IObjectFactory<T>
    {
        /// <summary>
        /// 创建对象
        /// </summary>
        T Create();
    }
}