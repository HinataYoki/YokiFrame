#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// PoolKit SimplePoolKit 文档
    /// </summary>
    internal static class PoolKitDocSimple
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "SimplePoolKit 简易对象池",
                Description = "轻量级对象池，适合简单场景，支持自定义创建和重置方法。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基础用法",
                        Code = @"// 创建简易对象池
var bulletPool = new SimplePoolKit<Bullet>(
    factoryMethod: () => new Bullet(),  // 创建方法
    resetMethod: b => b.Reset(),        // 重置方法（可选）
    initCount: 10                       // 初始数量（可选）
);

// 分配对象
var bullet = bulletPool.Allocate();
bullet.Fire(direction);

// 回收对象（自动调用 resetMethod）
bulletPool.Recycle(bullet);",
                        Explanation = "SimplePoolKit 适合不需要单例管理的局部对象池场景。"
                    },
                    new()
                    {
                        Title = "与 SafePoolKit 对比",
                        Code = @"// SimplePoolKit - 局部使用，手动管理
var localPool = new SimplePoolKit<MyClass>(() => new MyClass());

// SafePoolKit - 全局单例，自动管理
// 需要实现 IPoolable 接口
public class MyPoolable : IPoolable
{
    public bool IsRecycled { get; set; }
    public void OnRecycled() { /* 重置状态 */ }
}

// 使用单例访问
var obj = SafePoolKit<MyPoolable>.Instance.Allocate();
SafePoolKit<MyPoolable>.Instance.Recycle(obj);",
                        Explanation = "SimplePoolKit 更灵活，适合局部使用；SafePoolKit 更安全，防止重复回收，适合全局单例场景。"
                    }
                }
            };
        }
    }
}
#endif
