#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// PoolKit 自定义对象池文档
    /// </summary>
    internal static class PoolKitDocCustom
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "自定义对象池",
                Description = "继承 PoolKit<T> 创建自定义对象池，可以设置工厂方法和最大容量。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "创建自定义池",
                        Code = @"public class BulletPool : PoolKit<Bullet>
{
    public BulletPool(int initialCapacity = 32) : base(initialCapacity)
    {
        // 设置工厂方法
        SetFactoryMethod(() => new Bullet());
    }
    
    public override bool Recycle(Bullet obj)
    {
        if (obj == null) return false;
        obj.OnRecycled();
        mCacheStack.Push(obj);
        return true;
    }
}

// 使用
var pool = new BulletPool(64);
var bullet = pool.Allocate();
pool.Recycle(bullet);",
                        Explanation = "自定义池可以控制初始容量和回收逻辑。"
                    }
                }
            };
        }
    }
}
#endif
