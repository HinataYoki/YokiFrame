# LocalizationKit 本地化

## 适用场景

LocalizationKit 为游戏运行时提供语言选择、文本查询、复数文本、参数格式化、语言元数据和语言切换 Binder。文本数据来源由 `ILocalizationProvider` 注入，运行时业务只依赖 `LocalizationKit` 门面，不直接依赖 JSON、配置表或宿主资源系统。

适合以下场景：

- UI、提示、任务和剧情文本需要按当前语言读取。
- 文本需要索引参数、命名参数或简单自定义标签处理。
- 文本包含英语等有复数变化的语言，需要按数量选择分类。
- 语言切换后需要刷新已经绑定到文本编号的显示对象。
- 项目希望用 JSON 或委托表查询接入本地化数据，同时保持业务层与数据格式解耦。

LocalizationKit Runtime 不负责翻译服务和宿主资源加载。Provider 必须由项目启动代码或其它组合根显式安装；Workbench/CLI 只用于查看和维护项目源文件。

## 使用前提

LocalizationKit 可用于 Unity 与 Godot .NET Runtime。使用文本查询前必须先安装并注册一个 Provider，例如 JSON Provider 或 TableKit Provider；Workbench/CLI 只提供项目源文件搜索、缺失检查和预览。

YokiFrame 不会自动安装 LocalizationKit Provider。使用文本查询前，必须先调用 `LocalizationKit.SetProvider`；未配置 Provider 时查询会返回缺失文本标记。

## 快速上手

### JSON 数据

```csharp
using System;
using System.Collections.Generic;
using YokiFrame;

var provider = new JsonLocalizationProvider();
bool loaded = provider.TryLoadFromJson(jsonText, out string error);
if (!loaded)
{
    throw new InvalidOperationException(error);
}

LocalizationKit.SetProvider(provider);
LocalizationKit.SetDefaultLanguage(LanguageId.English);
if (!LocalizationKit.SetLanguage(LanguageId.ChineseSimplified))
{
    throw new InvalidOperationException("目标语言不在 Provider 支持列表中。");
}

string title = LocalizationKit.Get(1001);
string hp = LocalizationKit.Get(1002, currentHp, maxHp);
string message = LocalizationKit.Get(1003, new Dictionary<string, object>
{
    { "name", playerName },
    { "count", coinCount }
});
string apples = LocalizationKit.GetPlural(2001, appleCount);
```

### 语言切换与 Binder

```csharp
LocalizationKit.OnLanguageChanged += HandleLanguageChanged;
LocalizationKit.RegisterBinder(scoreLabel);

LocalizationKit.SetLanguage(LanguageId.English);

LocalizationKit.UnregisterBinder(scoreLabel);
LocalizationKit.OnLanguageChanged -= HandleLanguageChanged;
```

`ILocalizationBinder.IsValid` 为 `false` 的对象不会刷新。Binder 刷新期间可以注销自身；门面使用快照遍历，不会因集合修改而失效。

## 核心 API

### 文本查询与 fallback

#### 普通文本

```csharp
string current = LocalizationKit.Get(1001);
string english = LocalizationKit.Get(LanguageId.English, 1001);
string indexed = LocalizationKit.Get(1002, currentHp, maxHp);
string named = LocalizationKit.Get(1003, new Dictionary<string, object>
{
    { "name", playerName },
    { "count", coinCount }
});
```

`Get(int)` 和参数格式化重载使用当前语言；`Get(LanguageId, int)` 只对指定语言查询，但仍会在缺失时回退到默认语言。普通文本的格式模板支持 `{0}`、`{0:F1}` 和 `{name}` 形式。缺少参数时保留原占位符。

#### 复数文本

```csharp
string itemCount = LocalizationKit.GetPlural(2001, count);
string itemWithName = LocalizationKit.GetPlural(2002, count, itemName);
```

`GetPlural` 只使用当前语言，先通过 `PluralRuleFactory` 选择 `PluralCategory`，再把 `count` 放在格式化参数第一个位置。复数模板通常使用 `{0}`，额外参数从 `{1}` 开始。当前语言没有文本时，fallback 会按默认语言和同一个数量重新计算复数分类。

找不到普通文本时返回 `[Missing:<textId>]`；找不到复数文本时返回 `[Missing:<textId>:<category>]`。这些标记是稳定的运行时结果，调用方可以在 UI 或日志中识别。

#### 缓存

门面缓存普通文本和复数文本。以下操作会清理全部或相关缓存：

- `SetProvider`：清理旧 Provider 缓存，并在 Provider 实例变化时刷新 Binder。
- `SetDefaultLanguage`：清理 fallback 相关缓存并刷新有效 Binder。
- `SetLanguage`：清理全部缓存并刷新有效 Binder。
- `ClearCache`：显式清理全部缓存。
- `UnloadLanguage`：移除指定语言的文本和复数缓存。
- `Reset`：重置全部 Runtime 状态，适合宿主重新初始化。

### `LocalizationKit` API

#### 状态、Provider 和 Formatter

| API | 说明 |
|---|---|
| `event Action<LanguageId> OnLanguageChanged` | 语言实际成功切换后触发；设置同一语言时不触发 |
| `void SetProvider(ILocalizationProvider localizationProvider)` | 设置 Provider，不能为 `null`；会清理缓存 |
| `ILocalizationProvider GetProvider()` | 获取当前 Provider，未配置时返回 `null` |
| `void SetFormatter(ITextFormatter textFormatter)` | 设置 Formatter，不能为 `null` |
| `ITextFormatter GetFormatter()` | 获取当前 Formatter |
| `void SetDefaultLanguage(LanguageId languageId)` | 设置缺失文本的 fallback 语言、清理缓存并刷新有效 Binder |
| `LanguageId GetDefaultLanguage()` | 获取 fallback 语言 |

`SetProvider` 替换不同实例和 `SetDefaultLanguage` 实际改变时都会刷新 Binder，但不会触发 `OnLanguageChanged`。Provider 的支持语言和数据有效性由 Provider 自己定义。

#### 语言状态

| API | 说明 |
|---|---|
| `bool SetLanguage(LanguageId languageId)` | 切换当前语言；有 Provider 时目标必须在支持列表中 |
| `LanguageId GetCurrentLanguage()` | 获取当前语言 |
| `IReadOnlyList<LanguageId> GetAvailableLanguages()` | 获取 Provider 支持的只读语言列表；无 Provider 时为空列表 |
| `LanguageInfo GetLanguageInfo(LanguageId languageId)` | 获取语言显示元数据；无 Provider 时返回 `LanguageInfo.Empty` |
| `bool IsLanguageLoaded(LanguageId languageId)` | 查询语言是否已加载 |
| `void PreloadLanguage(LanguageId languageId)` | 请求 Provider 预加载语言；无 Provider 时忽略 |
| `void UnloadLanguage(LanguageId languageId)` | 请求 Provider 卸载语言并清理该语言缓存 |
| `void ClearCache()` | 清理普通文本和复数文本缓存 |
| `void Reset()` | 重置 Provider、Formatter、语言、缓存、Binder 和事件，适合宿主重新初始化 |

有 Provider 时，`SetLanguage` 对未支持语言返回 `false` 并保持当前语言；切换成功或目标已是当前语言时返回 `true`。无 Provider 时不执行支持列表校验。

#### 文本查询

| API | 说明 |
|---|---|
| `string Get(int textId)` | 按当前语言读取普通文本 |
| `string Get(LanguageId languageId, int textId)` | 按指定语言读取普通文本，并允许 fallback |
| `string Get(int textId, params object[] args)` | 按索引参数格式化当前语言文本 |
| `string Get(int textId, IReadOnlyDictionary<string, object> args)` | 按命名参数格式化当前语言文本 |
| `string GetPlural(int textId, int count)` | 按当前语言复数规则选择分类，并把数量作为第一个参数 |
| `string GetPlural(int textId, int count, params object[] extraArgs)` | 选择复数分类，并把数量和额外参数一起格式化 |

#### Binder

| API | 说明 |
|---|---|
| `void RegisterBinder(ILocalizationBinder binder)` | 注册语言切换时需要刷新的对象；传入 `null` 时忽略 |
| `void UnregisterBinder(ILocalizationBinder binder)` | 注销 Binder；传入 `null` 时忽略 |
| `int GetBinderCount()` | 获取当前注册数量 |

Binder 由调用方负责在对象生命周期结束时注销。`LocalizationKit` 不负责查找 UI 控件，也不提供 Unity `Text`、TMP 或 Godot 控件的默认实现。

### 公共契约

#### `ILocalizationProvider`

```csharp
IReadOnlyList<LanguageId> GetSupportedLanguages();
bool TryGetText(LanguageId languageId, int textId, out string text);
bool TryGetPluralText(
    LanguageId languageId,
    int textId,
    PluralCategory category,
    out string text);
LanguageInfo GetLanguageInfo(LanguageId languageId);
void PreloadLanguage(LanguageId languageId);
void UnloadLanguage(LanguageId languageId);
bool IsLanguageLoaded(LanguageId languageId);
```

Provider 应返回只读语言列表。`TryGetPluralText` 可以在具体分类不存在时回退 `Other`；门面还会在 fallback 语言上重新计算分类。

#### `ILocalizationBinder`

```csharp
int TextId { get; }
bool IsValid { get; }
void Refresh();
```

`TextId` 用于描述 Binder 绑定的文本编号，`IsValid` 用于阻止已销毁或已脱离宿主的对象刷新，`Refresh` 由语言切换或 Provider 替换触发。

#### `ITextFormatter`

```csharp
string Format(string template, ReadOnlySpan<object> args);
string Format(string template, IReadOnlyDictionary<string, object> namedArgs);
string ProcessTags(string text);
```

#### `IPluralRule`

```csharp
LanguageId LanguageId { get; }
PluralCategory GetCategory(int count);
PluralCategory GetCategory(double count);
```

### `LanguageId` 与 `PluralCategory`

#### `LanguageId`

内置语言标识为：`ChineseSimplified`、`ChineseTraditional`、`English`、`Japanese`、`Korean`、`French`、`German`、`Spanish`、`Portuguese`、`Russian`、`Arabic`、`Thai`、`Vietnamese`、`Indonesian`。

JSON 可以使用枚举名称或有效数字形式。Provider 的实际支持列表仍以 `GetSupportedLanguages()` 为准。

#### `PluralCategory`

可用分类为 `Zero`、`One`、`Two`、`Few`、`Many`、`Other`。没有注册规则的语言使用默认 invariant 规则，返回 `Other`。

#### `LanguageInfo`

```csharp
new LanguageInfo(
    LanguageId id,
    int displayNameTextId,
    int nativeNameTextId,
    int iconSpriteId)
```

公开字段为 `Id`、`DisplayNameTextId`、`NativeNameTextId`、`IconSpriteId`；`IsValid` 在至少一个显示资源编号不为零时为 `true`；`LanguageInfo.Empty` 表示未配置元数据；`ToString()` 返回包含语言 Id 和显示名称编号的诊断文本。

### `JsonLocalizationProvider`

#### JSON 结构

```json
{
  "formatVersion": 1,
  "languages": [
    {
      "id": "English",
      "displayNameTextId": 10,
      "nativeNameTextId": 11,
      "iconSpriteId": 12
    },
    { "id": "ChineseSimplified", "displayNameTextId": 20 }
  ],
  "texts": [
    {
      "id": 1001,
      "values": {
        "English": "Start",
        "ChineseSimplified": "开始"
      }
    },
    {
      "id": 2001,
      "plural": {
        "English": {
          "One": "{0} apple",
          "Other": "{0} apples"
        }
      }
    }
  ]
}
```

`formatVersion` 缺省时按 `1` 处理。`languages` 和 `texts` 是必需数组；每个语言、文本 ID 和 JSON 对象键必须唯一。文本条目的 `values` 和 `plural` 可选，但其中引用的语言必须先在 `languages` 声明。复数分类可以使用名称或有效数字形式。

#### API

| API | 说明 |
|---|---|
| `new JsonLocalizationProvider()` | 创建空 Provider |
| `string LastLoadError` | 最近一次加载错误；成功或 `Clear` 后为空 |
| `void LoadFromJson(string json)` | 尝试加载 JSON；失败时保留之前的完整快照，并通过 `LastLoadError` 暴露错误 |
| `bool TryLoadFromJson(string json, out string error)` | 验证并原子替换快照，失败返回 `false` |
| `void AddText(LanguageId languageId, int textId, string text)` | 手动添加普通文本，适合自定义导入器 |
| `void AddPluralText(LanguageId languageId, int textId, PluralCategory category, string text)` | 手动添加复数文本 |
| `void SetLanguageInfo(LanguageInfo info)` | 写入语言显示元数据，并自动登记语言 |
| `IEnumerable<int> GetAllTextIds()` | 获取当前普通和复数文本编号的去重集合；不保证顺序 |
| `void Clear()` | 清空文本、复数、语言元数据、支持语言和加载状态 |

加载成功后 JSON 中声明的语言全部标记为已加载。`PreloadLanguage`/`UnloadLanguage` 仅改变 Provider 的加载状态；查询未加载语言时返回失败。解析失败不会留下半份新数据。

### `TableLocalizationProvider`

Table Provider 不绑定 TableKit 或 Luban 生成类型，只定义委托边界：

```csharp
var provider = new TableLocalizationProvider(
    supportedLanguages: new[] { LanguageId.English },
    textGetter: (language, textId) => projectTable.GetText((int)language, textId),
    pluralTextGetter: (language, textId, category) =>
        projectTable.GetPlural((int)language, textId, category),
    languageInfoGetter: language => projectTable.GetLanguageInfo((int)language),
    errorHandler: exception => LogKit.Exception(exception));
```

构造函数：

```csharp
TableLocalizationProvider(
    IEnumerable<LanguageId> supportedLanguages,
    Func<LanguageId, int, string> textGetter,
    Func<LanguageId, int, PluralCategory, string> pluralTextGetter = null,
    Func<LanguageId, LanguageInfo> languageInfoGetter = null,
    Action<Exception> errorHandler = null)
```

其中 `supportedLanguages` 和 `textGetter` 不能为 `null`；重复语言会去重并保留首次出现顺序。`pluralTextGetter` 为空时，复数查询回退到普通文本委托；分类查询得到 `null` 时会再尝试 `Other`。`languageInfoGetter` 为空时，已支持语言返回带语言 Id 的空元数据；`errorHandler` 接收委托异常，异常不会继续抛出到查询调用方。

其余 API 与 `ILocalizationProvider` 一致：`GetSupportedLanguages`、`TryGetText`、`TryGetPluralText`、`GetLanguageInfo`、`PreloadLanguage`、`UnloadLanguage`、`IsLanguageLoaded`。初始支持语言均标记为已加载。

使用 Luban 或 TableKit 时，把生成表的查询委托传给 `TableKitLocalizationProvider` 即可；生成的表类型由项目自己维护，不需要让 LocalizationKit 直接依赖 Luban。

## 生命周期与错误边界

- 项目必须在查询前显式安装 Provider；`LocalizationKit` 不创建默认宿主资源后端
- 语言切换会通知已注册 Binder；业务 owner 必须在对象销毁时注销 Binder
- Provider 解析、重载或格式化失败不能覆盖当前可用快照；调用方应处理缺失文本标记
- 自定义复数规则和 Formatter 注册是进程内可变状态，会话结束时应恢复预期配置

## 格式化与复数

### `DefaultTextFormatter`

```csharp
var formatter = new DefaultTextFormatter();
formatter.RegisterTagHandler("b", value => value.ToUpperInvariant());

string indexed = formatter.Format("HP {0}/{1}", new object[] { 3, 5 });
string named = formatter.Format(
    "{name} has {count:F1}",
    new Dictionary<string, object>
    {
        { "name", "Alice" },
        { "count", 3.5f }
    });
string tagged = formatter.ProcessTags("A<b:ok>C");
```

| API | 说明 |
|---|---|
| `void RegisterTagHandler(string tagName, Func<string, string> handler)` | 注册或替换 `<tag:argument>` 处理器；空名称或空回调忽略 |
| `void UnregisterTagHandler(string tagName)` | 移除处理器；空名称忽略 |
| `string Format(string template, ReadOnlySpan<object> args)` | 处理 `{0}`、`{0:F1}` 等索引占位符 |
| `string Format(string template, IReadOnlyDictionary<string, object> namedArgs)` | 处理 `{name}`、`{count:F1}` 等命名占位符 |
| `string ProcessTags(string text)` | 处理 `<tag:argument>` 标签 |

索引和命名格式化支持 `{{`/`}}` 转义；参数缺失或占位符无法解析时保留原文。带格式说明的值使用 `CultureInfo.CurrentCulture`。未知标签保持原标签文本；没有闭合 `>` 的文本保持原文。标签回调在内部锁释放后执行，不应依赖 Formatter 的锁状态。

### `PluralRuleFactory`

| API | 说明 |
|---|---|
| `IPluralRule GetRule(LanguageId languageId)` | 获取语言规则；未注册时返回默认 invariant 规则 |
| `void RegisterRule(IPluralRule rule)` | 注册或替换规则；`rule` 不能为 `null` |
| `PluralCategory GetCategory(LanguageId languageId, int count)` | 按整数数量计算分类 |
| `PluralCategory GetCategory(LanguageId languageId, double count)` | 按浮点数量计算分类 |

内置注册规则为简体中文、繁体中文、英语、日语和韩语。其它内置 `LanguageId` 当前通过默认 invariant 规则返回 `Other`。

### 内置规则

- `EnglishPluralRule.Instance` 是英语规则单例：整数数量恰好为 `1` 返回 `One`，其它返回 `Other`；浮点数量与 `1` 的差值不超过 `1e-9` 时返回 `One`。
- `InvariantPluralRule` 的构造函数接受 `LanguageId`，整数和浮点数量都返回 `Other`。内置静态实例包括 `ChineseSimplified`、`ChineseTraditional`、`Japanese` 和 `Korean`。

自定义规则实现 `IPluralRule` 后调用 `PluralRuleFactory.RegisterRule`。注册是进程内全局行为，切换语言方案时请显式恢复或替换规则。

### `LocalizationSaveData`

`LocalizationSaveData` 是 `[Serializable]` 的纯数据对象，不负责文件读写；具体序列化由 SaveKit 或项目自己的序列化器负责。

| API | 说明 |
|---|---|
| `const int CurrentVersion` | 当前值为 `1` |
| `new LocalizationSaveData()` | 创建简体中文、当前版本的默认数据 |
| `new LocalizationSaveData(LanguageId language, int version)` | 创建指定语言和版本的数据 |
| `int CurrentLanguageId` | 语言的整数序列化字段 |
| `int Version` | 数据版本字段 |
| `LanguageId Language { get; set; }` | 读写语言枚举映射 |
| `static LocalizationSaveData CreateDefault()` | 创建默认保存数据 |
| `static LocalizationSaveData FromCurrentSettings()` | 从 `LocalizationKit.GetCurrentLanguage()` 创建数据 |
| `bool Apply()` | 应用保存的语言；Provider 不支持时返回 `false` |

## 在工具中查看

Workbench 可以浏览 JSON 或 Luban 表格中的文本、语言和缺失项；页面不会修改 Runtime 状态。使用 Luban 表格时，先准备 `luban.conf` 和 schema，再从 LocalizationKit 页面选择工作目录。

## 限制与相关资料

| 问题 | 处理 |
|---|---|
| `Get` 返回 `[Missing:...]` | 确认已调用 `SetProvider`、目标语言已加载、文本编号存在；再检查默认语言 fallback |
| `SetLanguage` 返回 `false` | Provider 已配置但目标语言不在 `GetAvailableLanguages()` 中 |
| JSON 加载失败 | 使用 `TryLoadFromJson` 查看 `error` 或读取 `LastLoadError`；失败不会覆盖旧快照 |
| 复数分类不符合预期 | 检查 `PluralRuleFactory.GetRule` 是否已注册目标语言规则以及 Provider 是否提供对应分类 |
| 语言切换后 UI 不更新 | UI 对象必须实现 `ILocalizationBinder`，注册后还要在销毁时注销 |
| 想直接从 Unity Text/TMP 取文本 | 当前没有内置宿主 Binder；由项目 Adapter 或 UI 层实现 `ILocalizationBinder` |
| 想直接加载 Addressables/YooAsset 语言包 | 当前没有 LocalizationKit 资源 Adapter；先实现 `ILocalizationProvider`，再由项目显式注入 |
| 想在 Workbench 查看本地化状态 | 打开 LocalizationKit 页面；未注册 Luban schema 时使用 standalone JSON，已注册时使用生成后的表格数据 |
