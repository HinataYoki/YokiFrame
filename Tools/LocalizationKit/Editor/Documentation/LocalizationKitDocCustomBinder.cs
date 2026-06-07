#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// LocalizationKit 自定义 Binder 文档
    /// </summary>
    internal static class LocalizationKitDocCustomBinder
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "自定义 Binder",
                Description = "基于依赖倒置原则，支持扩展任意组件类型的本地化绑定（文本/图片/音频/视频等）。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "核心概念",
                        Code = @"// ILocalizationBinder 接口
public interface ILocalizationBinder
{
    int TextId { get; }        // 资源ID
    bool IsValid { get; }      // 绑定器是否有效
    void Refresh();            // 语言切换时回调
}

// 工作流程：
// 1. 创建时调用 LocalizationKit.RegisterBinder(this) 自注册
// 2. 语言切换时 LocalizationKit 自动调用 Refresh()
// 3. 销毁时调用 LocalizationKit.UnregisterBinder(this) 注销",
                        Explanation = "所有 Binder 必须实现 ILocalizationBinder 接口，LocalizationKit 通过接口管理所有绑定器。"
                    },
                    new()
                    {
                        Title = "方式1：泛型 LocalizedBinder（推荐）",
                        Code = @"// 扩展自定义文本组件
public class CustomTextComponent : MonoBehaviour
{
    public string Content;
}

public static class CustomTextExtensions
{
    public static LocalizedBinder<CustomTextComponent> BindLocalization(
        this CustomTextComponent customText, int textId)
    {
        return new LocalizedBinder<CustomTextComponent>(
            resourceId: textId,
            component: customText,
            resourceGetter: LocalizationKit.Get,
            setter: (comp, text) => comp.Content = text,
            validityChecker: comp => comp != default
        );
    }
}

// 使用
var binder = myCustomText.BindLocalization(textId: 1001);",
                        Explanation = "泛型 LocalizedBinder<T> 是推荐方式，只需提供组件、资源获取函数和赋值逻辑即可。"
                    },
                    new()
                    {
                        Title = "方式2：实现 ILocalizationBinder（高级）",
                        Code = @"// 自定义图片绑定器（支持资源缓存）
public class CustomImageBinder : ILocalizationBinder, IDisposable
{
    private readonly int mSpriteId;
    private readonly Image mImage;
    private Sprite mCachedSprite;
    private bool mIsDisposed;

    public int TextId => mSpriteId;
    public bool IsValid => !mIsDisposed && mImage != default;

    public CustomImageBinder(int spriteId, Image image)
    {
        mSpriteId = spriteId;
        mImage = image;
        LocalizationKit.RegisterBinder(this);
        Refresh();
    }

    public void Refresh()
    {
        if (!IsValid) return;

        // 清理旧资源
        if (mCachedSprite != null)
            Resources.UnloadAsset(mCachedSprite);

        // 加载新资源
        var lang = LocalizationKit.GetCurrentLanguage();
        var path = $""Localization/{lang}/Sprites/{mSpriteId}"";
        mCachedSprite = Resources.Load<Sprite>(path);
        mImage.sprite = mCachedSprite;
    }

    public void Dispose()
    {
        if (mIsDisposed) return;
        mIsDisposed = true;
        LocalizationKit.UnregisterBinder(this);
        if (mCachedSprite != null)
            Resources.UnloadAsset(mCachedSprite);
    }
}",
                        Explanation = "直接实现接口适用于需要复杂逻辑的场景（如资源缓存、异步加载）。"
                    },
                    new()
                    {
                        Title = "扩展图片组件",
                        Code = @"// 使用泛型 Binder 扩展 Image 组件
public static class ImageLocalizationExtensions
{
    public static LocalizedBinder<Image> BindLocalizedSprite(
        this Image image, int spriteId)
    {
        return new LocalizedBinder<Image>(
            resourceId: spriteId,
            component: image,
            resourceGetter: id =>
            {
                var lang = LocalizationKit.GetCurrentLanguage();
                var path = $""Localization/{lang}/Sprites/{id}"";
                return path;
            },
            setter: (img, path) =>
            {
                // 使用 YooAsset 或 Resources 加载
                var sprite = Resources.Load<Sprite>(path);
                img.sprite = sprite;
            },
            validityChecker: img => img != default
        );
    }
}

// 使用
var binder = myImage.BindLocalizedSprite(spriteId: 2001);",
                        Explanation = "图片本地化需要根据语言加载不同的 Sprite 资源，结合项目的资源管理方案（YooAsset/Addressables）。"
                    },
                    new()
                    {
                        Title = "扩展音频组件",
                        Code = @"// 扩展 AudioSource 组件
public static class AudioLocalizationExtensions
{
    public static LocalizedBinder<AudioSource> BindLocalizedAudio(
        this AudioSource audioSource, int audioId)
    {
        return new LocalizedBinder<AudioSource>(
            resourceId: audioId,
            component: audioSource,
            resourceGetter: id =>
            {
                var lang = LocalizationKit.GetCurrentLanguage();
                return $""Audio/{lang}/{id}"";
            },
            setter: (audio, path) =>
            {
                var clip = Resources.Load<AudioClip>(path);
                audio.clip = clip;
            },
            validityChecker: audio => audio != default
        );
    }
}

// 使用
var binder = myAudioSource.BindLocalizedAudio(audioId: 3001);",
                        Explanation = "音频本地化适用于多语言配音、方言等场景。"
                    },
                    new()
                    {
                        Title = "完整使用示例",
                        Code = @"public class LocalizedUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI mTitleText;
    [SerializeField] private Image mFlagImage;
    [SerializeField] private AudioSource mBgmSource;

    private LocalizedTextBinder mTitleBinder;
    private ILocalizationBinder mFlagBinder;
    private ILocalizationBinder mBgmBinder;

    private void Start()
    {
        // 绑定文本
        mTitleBinder = mTitleText.BindLocalization(1001);

        // 绑定图片（需实现扩展方法）
        mFlagBinder = mFlagImage.BindLocalizedSprite(2001);

        // 绑定音频（需实现扩展方法）
        mBgmBinder = mBgmSource.BindLocalizedAudio(3001);
    }

    private void OnDestroy()
    {
        // 释放绑定器（必须！）
        mTitleBinder?.Dispose();
        (mFlagBinder as IDisposable)?.Dispose();
        (mBgmBinder as IDisposable)?.Dispose();
    }
}",
                        Explanation = "混合使用多种 Binder，语言切换时所有组件自动更新。务必在 OnDestroy 中释放所有绑定器。"
                    },
                    new()
                    {
                        Title = "注意事项",
                        Code = @"// ✅ 正确：Unity 对象判空
if (component != default) { }
validityChecker: comp => comp != default

// ❌ 错误：不要用 ?. 判空 Unity 对象
if (component?.gameObject != null) { } // 错误

// ✅ 正确：必须在 OnDestroy 释放
private void OnDestroy()
{
    mBinder?.Dispose();
}

// ❌ 错误：忘记释放导致内存泄漏
private void Start()
{
    tmpText.BindLocalization(1001); // 丢失引用，无法释放
}

// ✅ 正确：同步加载小资源
setter: (img, path) =>
{
    img.sprite = Resources.Load<Sprite>(path);
}

// ⚠️ 大资源需自行实现异步 Binder
// 在 Refresh 中启动 UniTask 异步加载",
                        Explanation = "Unity 对象判空使用 == default，必须释放 Binder，小资源同步加载，大资源异步加载。"
                    }
                }
            };
        }
    }
}
#endif
