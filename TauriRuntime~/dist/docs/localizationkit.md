# LocalizationKit 本地化

## 配置 Provider

```csharp
using YokiFrame;

LocalizationKit.SetProvider(new ProjectLocalizationProvider());
LocalizationKit.SetDefaultLanguage(LanguageId.English);
LocalizationKit.SetLanguage(LanguageId.ChineseSimplified);
```

Unity、Godot、CSV、JSON、Luban 表或远端配置差异都放进 `ILocalizationProvider`，业务代码仍调用 `LocalizationKit`。

## 读取文本

```csharp
string title = LocalizationKit.Get(1001);
string hp = LocalizationKit.Get(1002, currentHp, maxHp);

string message = LocalizationKit.Get(2001, new Dictionary<string, object>
{
    { "name", playerName },
    { "count", coinCount }
});
```

复数：

```csharp
string apples = LocalizationKit.GetPlural(3001, appleCount);
```

不要在业务里按语言写分支，复数规则交给 `IPluralRule`。

## 查询和预加载

```csharp
var current = LocalizationKit.GetCurrentLanguage();
var languages = LocalizationKit.GetAvailableLanguages();

LocalizationKit.PreloadLanguage(LanguageId.English);
LocalizationKit.UnloadLanguage(LanguageId.Japanese);
```

## UI Binder

```csharp
LocalizationKit.RegisterBinder(labelBinder);
LocalizationKit.SetLanguage(LanguageId.English);
LocalizationKit.UnregisterBinder(labelBinder);
```

语言切换会清空缓存，并刷新 `IsValid == true` 的 Binder。无效 Binder 会被跳过。

## 工作台诊断

LocalizationKit 页面用于查看当前语言、默认语言、语言列表、Provider、Formatter、缓存和 Binder。

| 在工作台里看什么 | 用途 |
|---|---|
| 当前语言 / 默认语言 | 确认运行时语言是否符合预期。 |
| 语言列表 | 检查目标语言是否加载。 |
| Provider | 判断文本来源是 CSV、JSON、Luban 表还是项目后端。 |
| Cache | 查看文本缓存数量和命中情况。 |
| Binder | 确认 UI 文本是否绑定并随语言刷新。 |

文本不更新时，先看当前语言，再看目标 key 是否由 Provider 提供，最后看 UI Binder 是否仍存活。切换语言会改变运行状态，只在明确需要时操作。

## 常见坑

| 问题 | 处理方式 |
|---|---|
| 页面没有语言 | 确认启动时设置了 Provider，且 Provider 返回支持语言。 |
| 文本显示 `[Missing:id]` | 当前语言和默认语言都没有该文本。 |
| UI 不刷新 | 确认 Binder 已注册、`IsValid` 为 true。 |
| 切语言失败 | 检查目标 `LanguageId` 是否定义且 Provider 支持。 |
| Unity/Godot 来源不同 | 差异放 Provider，不新建平行 Kit。 |
