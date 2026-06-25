using System;

namespace YokiFrame
{
    /// <summary>
    /// 自定义对象工厂：通过用户提供的工厂委托创建对象。
    /// </summary>
    public class CustomObjectFactory<T> : IObjectFactory<T>
    {
        protected Func<T> mFactoryMethod;

        public CustomObjectFactory(Func<T> factoryMethod) => mFactoryMethod = factoryMethod;
        public T Create() => mFactoryMethod.Invoke();
    }
}
