#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// PoolKit 工厂模式文档
    /// </summary>
    internal static class PoolKitDocFactory
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "对象工厂",
                Description = "对象池的工厂模式，支持自定义对象创建逻辑。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "工厂接口",
                        Code = @"// IObjectFactory 接口定义
public interface IObjectFactory<T>
{
    T Create();
}

// 默认工厂（使用 Activator.CreateInstance）
public class DefaultObjectFactory<T> : IObjectFactory<T> where T : new()
{
    public T Create() => new T();
}

// 自定义工厂（使用委托）
public class CustomObjectFactory<T> : IObjectFactory<T>
{
    private readonly Func<T> mFactoryMethod;
    
    public CustomObjectFactory(Func<T> factoryMethod)
    {
        mFactoryMethod = factoryMethod;
    }
    
    public T Create() => mFactoryMethod();
}",
                        Explanation = "工厂模式将对象创建逻辑与对象池解耦。"
                    },
                    new()
                    {
                        Title = "设置工厂",
                        Code = @"// 方式1：设置工厂实例
var pool = SafePoolKit<Enemy>.Instance;
pool.SetObjectFactory(new EnemyFactory());

// 方式2：设置工厂方法（更简洁）
pool.SetFactoryMethod(() => new Enemy(config));

// 自定义工厂示例
public class EnemyFactory : IObjectFactory<Enemy>
{
    private readonly EnemyConfig mConfig;
    
    public EnemyFactory(EnemyConfig config)
    {
        mConfig = config;
    }
    
    public Enemy Create()
    {
        var enemy = new Enemy();
        enemy.Initialize(mConfig);
        return enemy;
    }
}",
                        Explanation = "工厂方法适合需要依赖注入或复杂初始化的场景。"
                    },
                    new()
                    {
                        Title = "池配置",
                        Code = @"// SafePoolKit 配置
var pool = SafePoolKit<Bullet>.Instance;

// 设置最大缓存数量（超出时自动销毁）
pool.MaxCacheCount = 100;

// 初始化池（预热）
pool.Init(
    initCount: 20,      // 初始创建数量
    maxCount: 50,       // 最大缓存数量
    factoryMethod: () => new Bullet()
);

// 查询当前池状态
Debug.Log($""当前缓存: {pool.CurCount}"");",
                        Explanation = "合理配置池大小可以平衡内存占用和分配性能。"
                    }
                }
            };
        }
    }
}
#endif
