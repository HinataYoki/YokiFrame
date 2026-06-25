# LocalizationKit 本地化

LocalizationKit 是跨引擎本地化运行时门面。业务代码在 `YokiFrame` 命名空间中调用 `LocalizationKit` 静态入口，文本来源、语言预加载、Unity/Godot 资源差异都放在 `ILocalizationProvider` 或 Adapter 后端里，不把 `Resources`、Godot `ResourceLoader` 或编辑器工具 API 写进 Tools 层。

## 核心类型

| 类型 | 作用 |
|------|------|
| `LocalizationKit` | 静态统一入口，负责语言切换、文本读取、复数文本、缓存、Binder 刷新和诊断快照。 |
| `LanguageId` | 跨引擎语言枚举，例如 `ChineseSimplified`、`English`、`Japanese`。 |
| `LanguageInfo` | 语言显示信息，包含显示名文本 ID、本地名文本 ID 和图标 ID。 |
| `ILocalizationProvider` | 文本与语言数据后端接口。Unity、Godot 或项目可分别提供资源表、JSON、CSV、表格等后端。 |
| `ITextFormatter` | 文本格式化接口，默认支持索引参数和命名参数。 |
| `IPluralRule` | 复数规则接口，由 `PluralRuleFactory` 根据语言选择。 |
| `ILocalizationBinder` | UI 文本绑定接口，语言切换时由 LocalizationKit 刷新有效 Binder。 |

## 语言查询 API

| 方法 | 说明 |
|------|------|
| `GetCurrentLanguage()` | 获取当前语言。 |
| `GetDefaultLanguage()` | 获取默认语言。 |
| `GetAvailableLanguages()` | 获取可用语言列表。 |
| `GetLanguageInfo(languageId)` | 获取语言显示信息。 |
| `IsLanguageLoaded(languageId)` | 检查语言是否已加载。 |

```csharp
var current = LocalizationKit.GetCurrentLanguage();
var available = LocalizationKit.GetAvailableLanguages();
foreach (var lang in available)
{
    var info = LocalizationKit.GetLanguageInfo(lang);
    Debug.Log($"{lang}: {info}");
}
```

## 静态入口

安装 provider 后，Unity 和 Godot 业务侧仍然使用同一套调用风格：

```csharp
using YokiFrame;

LocalizationKit.SetProvider(new ProjectLocalizationProvider());
LocalizationKit.SetDefaultLanguage(LanguageId.English);
LocalizationKit.SetLanguage(LanguageId.ChineseSimplified);

string title = LocalizationKit.Get(1001);
string hp = LocalizationKit.Get(1002, currentHp, maxHp);
```

命名参数适合 UI 模板或策划表文本：

```csharp
string message = LocalizationKit.Get(2001, new Dictionary<string, object>
{
    { "name", playerName },
    { "count", coinCount }
});
```

复数文本通过统一入口读取，不在业务里写语言分支：

```csharp
string apples = LocalizationKit.GetPlural(3001, appleCount);
string formatted = LocalizationKit.GetPlural(3001, appleCount, extraArg1, extraArg2);
```

## Provider 和跨引擎边界

`ILocalizationProvider` 是迁移 1.x 本地化资源的主要扩展点：

```csharp
public sealed class ProjectLocalizationProvider : ILocalizationProvider
{
    // 从项目自己的表格、资源包或远端配置读取文本。
}
```

Unity 可以在 Adapter 或项目启动代码中把 ScriptableObject、Resources、Addressables、YooAsset、CSV/JSON 表包装成 provider。Godot 可以把 `res://`、`user://` 或 Godot Resource 包装成 provider。业务代码不要创建 `UnityLocalizationKit` 或 `GodotLocalizationKit` 这种平行入口。

## 缓存管理

| 方法 | 说明 |
|------|------|
| `ClearCache()` | 清空文本缓存和复数缓存。 |

## Binder 与刷新

UI 文本绑定对象实现 `ILocalizationBinder` 后注册到 LocalizationKit：

```csharp
LocalizationKit.RegisterBinder(labelBinder);
LocalizationKit.SetLanguage(LanguageId.English);
```

语言切换会清空文本缓存、复数缓存，并刷新 `IsValid == true` 的 Binder。无效 Binder 会被跳过，避免 UI 生命周期结束后仍被访问。

### Binder 管理 API

| 方法 | 说明 |
|------|------|
| `RegisterBinder(binder)` | 注册 Binder。 |
| `UnregisterBinder(binder)` | 注销 Binder。 |
| `GetBinderCount()` | 获取已注册 Binder 数量。 |

## 语言预加载

```csharp
LocalizationKit.PreloadLanguage(LanguageId.English);
LocalizationKit.UnloadLanguage(LanguageId.Japanese);
```

预加载可以提前加载语言数据，减少切换时的延迟。卸载可以释放不需要的语言数据。

## 重置

```csharp
LocalizationKit.Reset();
```

`Reset()` 会清空 Provider、Formatter、缓存、Binder 和语言状态。


## Tauri 工作台

LocalizationKit 页面读取顺序为：

1. `read_telemetry("LocalizationKit", "state")`
2. `read_snapshot("LocalizationKit", "state")`
3. `send_command("LocalizationKit", "get_workbench_snapshot")`

Unity `KitStateSnapshotPublisher` 和 Godot `GodotKitStateSnapshotPublisher` 都通过可选 handler 发布 `LocalizationKit/state`。Tauri 工作台展示 provider、formatter、缓存、Binder 和语言列表；用户点击切换语言时才发送 `set_language` 命令，避免用命令桥高频轮询运行时状态。

## AI 查询建议

AI 默认先读：

```text
.yokiframe/engines/<engineId>/snapshots/LocalizationKit/state.json
```

snapshot 缺失、过期或需要请求-响应语义时，再发送 `LocalizationKit/get_workbench_snapshot`、`LocalizationKit/stats` 或 `LocalizationKit/list_languages`。只有用户明确要求切换语言时，才发送 `LocalizationKit/set_language`。

## 常见问题

| 问题 | 处理方式 |
|------|----------|
| Tauri 页面没有语言 | 确认启动时调用了 `LocalizationKit.SetProvider()`，并且 provider 返回了支持语言列表。 |
| 切换语言失败 | 检查 provider 是否支持目标语言，以及 payload 是否使用已定义的 `LanguageId`。 |
| 文本显示 `[Missing:id]` | 当前语言和默认语言都没有对应文本，检查表格 ID、默认语言和 provider 加载状态。 |
| UI 没有刷新 | 确认 UI Binder 已注册、`IsValid` 为 true，并且语言切换走 `LocalizationKit.SetLanguage()`。 |
| Unity/Godot 文本来源不同 | 差异应放在 `ILocalizationProvider` 或 Adapter 后端，业务仍使用 `LocalizationKit.Get()`。 |
