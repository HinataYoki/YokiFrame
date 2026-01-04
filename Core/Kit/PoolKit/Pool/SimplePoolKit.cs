using System;

namespace YokiFrame
{
    /// <summary>
    /// 简易对象池
    /// </summary>
    public class SimplePoolKit<T> : PoolKit<T>
    {
        private readonly Action<T> mResetMethod;

        public SimplePoolKit(Func<T> factoryMethod, Action<T> resetMethod = null, int initCount = 0) 
            : base(initCount > 0 ? initCount : 16)
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