#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    // PoolKit 和 SingletonKit 文档
    public partial class DocumentationToolPage
    {
        private DocModule CreatePoolKitDoc()
        {
            return new DocModule
            {
                Name = "PoolKit",
                Icon = "♻️",
                Category = "CORE KIT",
                Description = "对象池系统，减少频繁创建销毁对象带来的 GC 压力。提供泛型对象池、安全对象池和容器池。",
                Sections = new List<DocSection>
                {
                    new()
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
}"
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
                    },
                    new()
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
pool.Recycle(bullet);"
                            }
                        }
                    },
                    new()
                    {
                        Title = "容器池",
                        Description = "List、Dictionary、HashSet 等容器的复用池，避免频繁分配。使用 Unity 内置的 Pool API。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "使用容器池",
                                Code = @"using UnityEngine.Pool;

// 方式1：使用 Pool 静态类（自动归还）
Pool.List<int>(list =>
{
    list.Add(1);
    list.Add(2);
    list.Add(3);
    // 使用 list...
    // 作用域结束后自动归还
});

Pool.Dictionary<int, string>(dict =>
{
    dict[1] = ""one"";
    dict[2] = ""two"";
    // 使用 dict...
});

// 方式2：手动管理
var list = ListPool<int>.Get();
list.Add(1);
list.Add(2);
// 使用完毕归还（会自动 Clear）
ListPool<int>.Release(list);

var dict = DictionaryPool<int, string>.Get();
dict[1] = ""one"";
DictionaryPool<int, string>.Release(dict);",
                                Explanation = "容器池避免了频繁 new List/Dictionary 带来的 GC 压力。"
                            }
                        }
                    }
                }
            };
        }
        
        private DocModule CreateSingletonKitDoc()
        {
            return new DocModule
            {
                Name = "SingletonKit",
                Icon = "🎯",
                Category = "CORE KIT",
                Description = "单例模式工具，提供普通单例和 MonoBehaviour 单例两种实现。推荐优先使用普通单例，避免依赖 Unity 生命周期。",
                Sections = new List<DocSection>
                {
                    new()
                    {
                        Title = "普通单例（推荐）",
                        Description = "不依赖 Unity 的纯 C# 单例实现，由 SingletonKit<T> 统一管理。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "实现单例",
                                Code = @"public class GameManager : Singleton<GameManager>
{
    public int Score { get; private set; }
    public GameState State { get; private set; }
    
    // 单例初始化回调
    public override void OnSingletonInit()
    {
        Score = 0;
        State = GameState.Menu;
    }
    
    public void AddScore(int value)
    {
        Score += value;
    }
    
    public void ChangeState(GameState newState)
    {
        State = newState;
    }
}

// 使用
GameManager.Instance.AddScore(100);
var state = GameManager.Instance.State;

// 释放单例（可选）
GameManager.Dispose();",
                                Explanation = "推荐使用普通单例，避免依赖 MonoBehaviour 生命周期。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "MonoBehaviour 单例",
                        Description = "需要挂载到 GameObject 的单例，仅在必须使用 Unity 生命周期时使用。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "实现 Mono 单例",
                                Code = @"public class AudioManager : MonoSingleton<AudioManager>
{
    private AudioSource mBgmSource;
    
    public override void OnSingletonInit()
    {
        // 初始化逻辑
        DontDestroyOnLoad(gameObject);
        mBgmSource = gameObject.AddComponent<AudioSource>();
    }
    
    public void PlayBGM(AudioClip clip)
    {
        mBgmSource.clip = clip;
        mBgmSource.loop = true;
        mBgmSource.Play();
    }
    
    protected override void OnDestroy()
    {
        base.OnDestroy(); // 调用基类清理单例引用
    }
}

// 使用
AudioManager.Instance.PlayBGM(bgmClip);",
                                Explanation = "MonoSingleton 会自动创建 GameObject，但应尽量避免使用。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "MonoSingletonPath 特性",
                        Description = "使用 MonoSingletonPath 特性指定 MonoSingleton 在场景中的路径。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "指定单例路径",
                                Code = @"[MonoSingletonPath(""[Managers]/AudioManager"")]
public class AudioManager : MonoSingleton<AudioManager>
{
    // ...
}

// 访问时会自动创建层级结构：
// [Managers] (GameObject)
//   └── AudioManager (GameObject with AudioManager component)"
                            }
                        }
                    }
                }
            };
        }
    }
}
#endif
