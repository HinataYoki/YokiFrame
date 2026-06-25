namespace YokiFrame
{
    /// <summary>
    /// 默认对象工厂：通过 new() 创建对象。
    /// </summary>
    public class DefaultObjectFactory<T> : IObjectFactory<T> where T : new()
    {
        public T Create() => new();
    }
}
