#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// LocalizationKit 最佳实践文档
    /// </summary>
    internal static class LocalizationKitDocBestPractice
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "最佳实践",
                Description = "推荐的使用方式。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "定义文本 ID 常量",
                        Code = @"// 推荐：使用静态类定义文本 ID
public static class TextId
{
    // UI 文本
    public const int CONFIRM = 1001;
    public const int CANCEL = 1002;
    public const int TITLE = 1003;
    
    // 游戏文本
    public const int ITEM_NAME = 2001;
    public const int SKILL_DESC = 2002;
    
    // 系统消息
    public const int ERROR_NETWORK = 3001;
    public const int ERROR_SAVE = 3002;
}

// 使用
string text = LocalizationKit.Get(TextId.CONFIRM);",
                        Explanation = "使用 int 常量而非字符串，避免魔法值，便于重构和查找引用。"
                    },
                    new()
                    {
                        Title = "初始化流程",
                        Code = @"// 游戏启动时初始化
public class GameInitializer
{
    public void Initialize()
    {
        // 1. 设置数据提供者
        var provider = new JsonLocalizationProvider();
        provider.LoadFromResources();
        LocalizationKit.SetProvider(provider);

        // 2. 设置默认语言
        LocalizationKit.SetDefaultLanguage(LanguageId.ChineseSimplified);

        // 3. 从存档加载语言偏好
        var saveData = SaveKit.Load(0);
        if (saveData != null)
        {
            LocalizationKitSaveIntegration.LoadLanguagePreference(saveData);
        }

        // 4. 监听语言切换，保存偏好
        LocalizationKit.OnLanguageChanged += _ =>
        {
            var data = SaveKit.Load(0) ?? SaveKit.CreateSaveData();
            LocalizationKitSaveIntegration.SaveLanguagePreference(data);
            SaveKit.Save(0, data);
        };
    }
}"
                    },
                    new()
                    {
                        Title = "Binder 生命周期管理",
                        Code = @"// 推荐：在 MonoBehaviour 中管理 Binder 生命周期
public class LocalizedUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI mTitleText;
    [SerializeField] private TextMeshProUGUI mDescText;
    [SerializeField] private Image mIconImage;

    private LocalizedTextBinder mTitleBinder;
    private LocalizedTextBinder mDescBinder;
    private ILocalizationBinder mIconBinder;

    private void Start()
    {
        // 创建绑定器
        mTitleBinder = mTitleText.BindLocalization(TextId.TITLE);
        mDescBinder = mDescText.BindLocalization(TextId.DESC, playerName, score);

        // 图片绑定（需用户自行实现扩展方法）
        mIconBinder = mIconImage.BindLocalizedSprite(2001);
    }

    private void OnDestroy()
    {
        // 释放绑定器（必须！）
        mTitleBinder?.Dispose();
        mDescBinder?.Dispose();
        mIconBinder?.Dispose();
    }

    // 更新参数
    public void UpdateScore(int newScore)
    {
        mDescBinder?.UpdateArgs(playerName, newScore);
    }
}",
                        Explanation = "Binder 必须在 OnDestroy 中调用 Dispose() 释放，避免内存泄漏和无效引用。"
                    },
                    new()
                    {
                        Title = "避免常见错误",
                        Code = @"// ❌ 错误：未释放 Binder
private void Start()
{
    tmpText.BindLocalization(1001); // Binder 丢失引用但未释放
}

// ✅ 正确：保存引用并在销毁时释放
private LocalizedTextBinder mBinder;
private void Start()
{
    mBinder = tmpText.BindLocalization(1001);
}
private void OnDestroy()
{
    mBinder?.Dispose();
}

// ❌ 错误：Unity 对象判空使用 ?.
if (tmpText?.gameObject != null) { }

// ✅ 正确：Unity 对象判空使用 == default
if (tmpText != default) { }

// ❌ 错误：重复创建 Binder
private void Update()
{
    tmpText.BindLocalization(1001); // 每帧创建新 Binder，内存泄漏
}

// ✅ 正确：只创建一次
private LocalizedTextBinder mBinder;
private void Start()
{
    mBinder = tmpText.BindLocalization(1001);
}",
                        Explanation = "常见错误包括：未释放 Binder、错误的判空方式、重复创建 Binder。"
                    }
                }
            };
        }
    }
}
#endif
