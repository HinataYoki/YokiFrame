namespace YokiFrame
{
    /// <summary>
    /// 对象工厂接口。
    /// </summary>
    public interface IObjectFactory<T>
    {
        /// <summary>创建新的对象实例。</summary>
        T Create();
    }
}
