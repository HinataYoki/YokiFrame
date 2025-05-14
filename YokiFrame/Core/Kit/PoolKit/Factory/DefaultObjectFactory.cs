namespace YokiFrame
{
    /// <summary>
    /// 默认对象工厂：相关对象是通过New 出来的
    /// </summary>
    public class DefaultObjectFactory<T> : IObjectFactory<T> where T : new()
    {
        public T Create() => new();
    }
}