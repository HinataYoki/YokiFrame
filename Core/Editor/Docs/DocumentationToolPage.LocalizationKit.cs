#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    public partial class DocumentationToolPage
    {
        private DocModule CreateLocalizationKitDoc()
        {
            return new DocModule
            {
                Name = "LocalizationKit",
                Icon = "🌐",
                Category = "TOOLS",
                Description = "本地化系统工具，提供多语言文本管理、参数化文本、复数形式、UI 绑定、异步加载等功能。支持 JSON 和 TableKit 数据源。",
                Sections = new List<DocSection>
                {
                    new()
                    {
                        Title = "快速开始",
                        Description = "LocalizationKit 提供简洁的本地化 API。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "初始化",
                                Code = @"// 创建 JSON 数据提供者
var provider = new JsonLocalizationProvider();
provider.LoadFromResources(); // 从 Resources/Localization 加载

// 设置提供者
LocalizationKit.SetProvider(provider);

// 设置默认语言（用于 fallback）
LocalizationKit.SetDefaultLanguage(LanguageId.ChineseSimplified);",
                                Explanation = "初始化时设置数据提供者，支持 JSON 文件或 TableKit 配置表。"
                            },
                            new()
                            {
                                Title = "获取文本",
                                Code = @"// 使用 int ID 获取文本（推荐）
string text = LocalizationKit.Get(TextId.CONFIRM); // ""确认""

// 带参数的文本
string welcome = LocalizationKit.Get(TextId.WELCOME, ""玩家名"");
// 模板: ""欢迎，{0}！"" -> ""欢迎，玩家名！""

// 命名参数
var args = new Dictionary<string, object>
{
    { ""name"", ""Alice"" },
    { ""count"", 100 }
};
string msg = LocalizationKit.Get(TextId.REWARD_MSG, args);
// 模板: ""{name} 获得了 {count} 金币"" -> ""Alice 获得了 100 金币""",
                                Explanation = "使用 int ID 而非字符串作为 key，避免魔法字符串。推荐定义 TextId 常量类或枚举。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "语言切换",
                        Description = "运行时切换语言，自动刷新所有绑定的 UI。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "切换语言",
                                Code = @"// 切换到英文
bool success = LocalizationKit.SetLanguage(LanguageId.English);

// 获取当前语言
LanguageId current = LocalizationKit.GetCurrentLanguage();

// 获取支持的语言列表
var languages = LocalizationKit.GetAvailableLanguages();

// 监听语言切换事件
LocalizationKit.OnLanguageChanged += newLang =>
{
    Debug.Log($""语言已切换到: {newLang}"");
};",
                                Explanation = "切换语言时会自动清除缓存并通知所有绑定器刷新。"
                            },
                            new()
                            {
                                Title = "语言信息",
                                Code = @"// 获取语言详细信息
var info = LocalizationKit.GetLanguageInfo(LanguageId.English);
// info.DisplayNameTextId -> 显示名称的文本ID
// info.NativeNameTextId -> 原生名称的文本ID
// info.IconSpriteId -> 图标资源ID

// 检查语言是否已加载
if (LocalizationKit.IsLanguageLoaded(LanguageId.Japanese))
{
    // 日语数据已加载
}"
                            }
                        }
                    },
                    new()
                    {
                        Title = "复数形式",
                        Description = "根据数量自动选择正确的复数形式。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "复数文本",
                                Code = @"// 英文复数
// One: ""1 item""
// Other: ""{0} items""
string text = LocalizationKit.GetPlural(TextId.ITEM_COUNT, 1);  // ""1 item""
string text2 = LocalizationKit.GetPlural(TextId.ITEM_COUNT, 5); // ""5 items""

// 中文（无复数变化）
// Other: ""{0} 个物品""
string zhText = LocalizationKit.GetPlural(TextId.ITEM_COUNT, 1);  // ""1 个物品""
string zhText2 = LocalizationKit.GetPlural(TextId.ITEM_COUNT, 5); // ""5 个物品""

// 带额外参数
string msg = LocalizationKit.GetPlural(TextId.REWARD, count, ""金币"");
// 模板: ""{0} 个{1}"" -> ""5 个金币""",
                                Explanation = "遵循 ICU 复数规则，支持 Zero/One/Two/Few/Many/Other 六种类别。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "UI 绑定",
                        Description = "自动响应语言切换的 UI 文本绑定。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "绑定文本组件",
                                Code = @"// 绑定 TextMeshProUGUI
var binder = tmpText.BindLocalization(TextId.TITLE);

// 绑定带参数的文本
var binder2 = tmpText.BindLocalization(TextId.WELCOME, ""玩家名"");

// 绑定 Legacy Text
var binder3 = legacyText.BindLocalization(TextId.CONFIRM);

// 更新参数
binder2.UpdateArgs(""新玩家名"");

// 手动刷新
binder.Refresh();

// 释放绑定（重要！）
binder.Dispose();",
                                Explanation = "绑定器会自动注册到 LocalizationKit，语言切换时自动刷新。使用完毕后必须调用 Dispose() 释放。"
                            },
                            new()
                            {
                                Title = "手动管理绑定器",
                                Code = @"// 创建绑定器
var binder = new LocalizedTextBinder(TextId.TITLE, tmpText);

// 使用命名参数
var args = new Dictionary<string, object> { { ""name"", ""Test"" } };
var binder2 = new LocalizedTextBinder(TextId.MSG, tmpText, args);

// 获取绑定器数量
int count = LocalizationKit.GetBinderCount();"
                            }
                        }
                    },
                    new()
                    {
                        Title = "异步加载",
                        Description = "使用 UniTask 异步加载语言数据。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "异步操作",
                                Code = @"// 异步加载语言
await LocalizationKitAsync.LoadLanguageAsync(
    LanguageId.Japanese,
    progress: new Progress<float>(p => Debug.Log($""加载进度: {p:P0}"")),
    cancellationToken: destroyCancellationToken
);

// 异步切换语言（包含加载）
bool success = await LocalizationKitAsync.SetLanguageAsync(
    LanguageId.Japanese,
    cancellationToken: destroyCancellationToken
);

// 异步获取文本
string text = await LocalizationKitAsync.GetAsync(TextId.TITLE);

// 异步卸载语言
await LocalizationKitAsync.UnloadLanguageAsync(LanguageId.Japanese);",
                                Explanation = "需要定义 YOKIFRAME_UNITASK_SUPPORT 宏。支持取消令牌和进度回调。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "数据提供者",
                        Description = "支持多种数据源。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "JSON 提供者",
                                Code = @"// 从 Resources 加载
var provider = new JsonLocalizationProvider(
    pathPattern: ""Localization/localization"",
    useResources: true
);
provider.LoadFromResources();

// 从 JSON 字符串加载
var json = @""{
    """"languages"""": [
        { """"id"""": 0, """"displayNameTextId"""": 1 },
        { """"id"""": 2, """"displayNameTextId"""": 2 }
    ],
    """"texts"""": [
        { """"id"""": 1001, """"values"""": [""""确认"""", """"Confirm""""] }
    ]
}"";
provider.LoadFromJson(json);

// 手动添加文本（用于测试）
provider.AddText(LanguageId.English, 1001, ""Hello"");
provider.AddPluralText(LanguageId.English, 1002, PluralCategory.One, ""1 item"");
provider.AddPluralText(LanguageId.English, 1002, PluralCategory.Other, ""{0} items"");"
                            },
                            new()
                            {
                                Title = "TableKit 提供者",
                                Code = @"// 使用 TableKit 配置表（需要 YOKIFRAME_LUBAN_SUPPORT）
var provider = new TableKitLocalizationProvider();
LocalizationKit.SetProvider(provider);

// TableKit 提供者会自动从 Luban 生成的配置表读取数据",
                                Explanation = "需要先通过 TableKit 生成本地化配置表代码。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "文本格式化",
                        Description = "支持占位符、格式说明符和自定义标签。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "格式化功能",
                                Code = @"// 索引占位符
// 模板: ""你好，{0}！你有 {1} 条消息。""
LocalizationKit.Get(TextId.MSG, ""Alice"", 5);

// 格式说明符
// 模板: ""价格: {0:F2} 元""
LocalizationKit.Get(TextId.PRICE, 19.99f); // ""价格: 19.99 元""

// 转义大括号
// 模板: ""{{0}} 表示占位符""
// 结果: ""{0} 表示占位符""

// 自定义标签处理
var formatter = LocalizationKit.GetFormatter() as DefaultTextFormatter;
formatter.RegisterTagHandler(""item"", param => $""[物品:{param}]"");
// 模板: ""你获得了 <item:1001>""
// 结果: ""你获得了 [物品:1001]""",
                                Explanation = "支持 Unity 原生富文本标签（如 <color>、<b>）和自定义标签。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "SaveKit 集成",
                        Description = "持久化语言偏好设置。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "保存/加载语言设置",
                                Code = @"// 保存当前语言设置
var saveData = SaveKit.CreateSaveData();
LocalizationKitSaveIntegration.SaveLanguagePreference(saveData);
SaveKit.Save(slotId, saveData);

// 加载语言设置
var loadedData = SaveKit.Load(slotId);
if (LocalizationKitSaveIntegration.HasLanguagePreference(loadedData))
{
    LocalizationKitSaveIntegration.LoadLanguagePreference(loadedData);
}

// 清除语言设置
LocalizationKitSaveIntegration.ClearLanguagePreference(saveData);",
                                Explanation = "语言偏好会保存到 SaveData 中，下次启动时自动恢复。"
                            }
                        }
                    },
                    new()
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
                            }
                        }
                    }
                }
            };
        }
    }
}
#endif
