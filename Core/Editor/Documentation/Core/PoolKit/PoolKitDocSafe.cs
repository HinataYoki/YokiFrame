#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// PoolKit SafePoolKit 文档
    /// </summary>
    internal static class PoolKitDocSafe
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "SafePoolKit（推荐）",
                Description = "线程安全的泛型对象池，支持 IPoolable 接口实现自动重置。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "实现 IPoolable 接口",
                        Code = @"public class Bullet : IPoolable
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float Damage;
    public bool IsRecycled { get; set; }
    
    public void OnRecycled()
    {
        // 回收时自动调用，重置状态
        Position = Vector3.zero;
        Velocity = Vector3.zero;
        Damage = 0;
    }
}",
                        Explanation = "实现 IPoolable 接口，回收时自动调用 OnRecycled 重置状态。"
                    },
                    new()
                    {
                        Title = "使用 SafePoolKit",
                        Code = @"// 从池中分配对象
var bullet = SafePoolKit<Bullet>.Instance.Allocate();
bullet.Position = spawnPoint;
bullet.Velocity = direction * speed;
bullet.Damage = 10f;

// 使用完毕后回收（会自动调用 OnRecycled）
SafePoolKit<Bullet>.Instance.Recycle(bullet);

// 查看当前池中对象数量
int poolCount = SafePoolKit<Bullet>.Instance.CurCount;",
                        Explanation = "SafePoolKit 是单例模式，全局共享同一个池。"
                    }
                }
            };
        }
    }
}
#endif
