using System;

namespace YokiFrame
{
    /// <summary>
    /// 简易对象池
    /// </summary>
    public class SimpleObjectPool<T> : Pool<T>
    {
        private readonly Action<T> mResetMethod;

        public SimpleObjectPool(Func<T> factoryMethod, Action<T> resetMethod = null, int initCount = 0)
        {
            mFactory = new CustomObjectFactory<T>(factoryMethod);
            mResetMethod = resetMethod;

            for (var i = 0; i < initCount; i++)
            {
                mCacheStack.Push(mFactory.Create());
            }
        }

        public override bool Recycle(T obj)
        {
            mResetMethod?.Invoke(obj);
            mCacheStack.Push(obj);
            return true;
        }
    }
}