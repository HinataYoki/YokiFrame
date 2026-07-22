using System.Text.Json;
using System.Text.Json.Serialization;
using YokiFrame.Tooling.Application.Models.LocalizationKit;
using YokiFrame.Tooling.Application.Services.Settings;

namespace YokiFrame.Tooling.Application.Services.LocalizationKit;

/// <summary>通过统一 owned-document Gateway 保存 LocalizationKit 的 Workbench-only Luban 工作目录。</summary>
public sealed class LocalizationKitSettingsService
{
    private readonly YokiFrameProjectSettingsStore? mSettingsStore;

    /// <summary>创建按调用方项目根解析 Store 的 LocalizationKit 配置服务。</summary>
    public LocalizationKitSettingsService()
    {
    }

    /// <summary>创建复用指定项目配置 Store 的 LocalizationKit 配置服务。</summary>
    /// <param name="settingsStore">项目级统一配置 Store。</param>
    public LocalizationKitSettingsService(YokiFrameProjectSettingsStore settingsStore)
    {
        mSettingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
    }

    /// <summary>读取项目级 Luban 工作目录；文件缺失或损坏时回落自动发现配置。</summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <returns>可直接投影到 Workbench 的设置。</returns>
    public LocalizationKitWorkbenchSettings Load(string projectRoot)
    {
        YokiFrameProjectOwnedDocumentSnapshot snapshot = GetStore(projectRoot)
            .ReadOwnedDocument(YokiFrameProjectSettingsTarget.LocalizationKitDraft);
        if (!snapshot.Exists || string.IsNullOrWhiteSpace(snapshot.Content))
        {
            return new LocalizationKitWorkbenchSettings();
        }

        try
        {
            return JsonSerializer.Deserialize(
                       snapshot.Content,
                       LocalizationKitSettingsJsonContext.Default.LocalizationKitWorkbenchSettings)
                   ?? new LocalizationKitWorkbenchSettings();
        }
        catch (JsonException)
        {
            return new LocalizationKitWorkbenchSettings();
        }
    }

    /// <summary>原子保存当前项目的 Luban 工作目录，不写入 Runtime Settings。</summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <param name="settings">待提交的 Workbench-only 设置。</param>
    public void Save(string projectRoot, LocalizationKitWorkbenchSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string json = JsonSerializer.Serialize(
            settings,
            LocalizationKitSettingsJsonContext.Default.LocalizationKitWorkbenchSettings);
        YokiFrameProjectOwnedDocumentWriteResult result = GetStore(projectRoot)
            .WriteOwnedDocumentAsync(
                YokiFrameProjectSettingsTarget.LocalizationKitDraft,
                json,
                YokiFrameProjectSettingsWriteMode.MergeLatest,
                cancellationToken: CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        if (!result.Saved)
        {
            throw new IOException("LocalizationKit Workbench 配置在提交时发生冲突。");
        }
    }

    /// <summary>获取绑定当前项目根的 Store，并拒绝把注入 Store 用于其它项目。</summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <returns>当前项目唯一可用的配置 Store。</returns>
    private YokiFrameProjectSettingsStore GetStore(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            throw new ArgumentException("项目根不能为空。", nameof(projectRoot));
        }

        string normalizedRoot = Path.GetFullPath(projectRoot);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (mSettingsStore != null)
        {
            if (!string.Equals(mSettingsStore.ProjectRoot, normalizedRoot, comparison))
            {
                throw new InvalidOperationException("LocalizationKit 配置 Store 绑定到其它项目。");
            }

            return mSettingsStore;
        }

        return new YokiFrameProjectSettingsStore(normalizedRoot);
    }
}

/// <summary>为 LocalizationKit Workbench 配置提供 Native AOT 可用的 JSON 元数据。</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(LocalizationKitWorkbenchSettings))]
internal sealed partial class LocalizationKitSettingsJsonContext : JsonSerializerContext
{
}
