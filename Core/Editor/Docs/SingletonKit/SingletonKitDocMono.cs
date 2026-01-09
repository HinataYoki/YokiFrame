#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SingletonKit MonoBehaviour 单例文档
    /// </summary>
    internal static class SingletonKitDocMono
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
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
                    },
                    new()
                    {
                        Title = "MonoSingletonPath 特性",
                        Code = @"[MonoSingletonPath(""[Managers]/AudioManager"")]
public class AudioManager : MonoSingleton<AudioManager>
{
    // ...
}

// 访问时会自动创建层级结构：
// [Managers] (GameObject)
//   └── AudioManager (GameObject with AudioManager component)",
                        Explanation = "使用 MonoSingletonPath 特性指定单例在场景中的路径。"
                    }
                }
            };
        }
    }
}
#endif
