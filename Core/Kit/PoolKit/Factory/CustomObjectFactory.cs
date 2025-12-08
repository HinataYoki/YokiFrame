using System;

namespace YokiFrame
{
    /// <summary>
    /// 自定义对象工厂：相关对象是 自己定义 
    /// </summary>
    public class CustomObjectFactory<T> : IObjectFactory<T>
    {
        protected Func<T> mFactoryMethod;
        public CustomObjectFactory(Func<T> factoryMethod) => mFactoryMethod = factoryMethod;
        public T Create() => mFactoryMethod.Invoke();
    }
}