using System.Text.Json;
using System.Text.Json.Serialization;
using YokiFrame.Tooling.Application.Models.TableKit;
using YokiFrame.Tooling.Application.Services.Settings;

namespace YokiFrame.Tooling.Application.Services.TableKit;

/// <summary>通过统一 owned-document Gateway 保存 TableKit Workbench-only 配置，不把编辑器字段写入 Runtime Settings。</summary>
public sealed class TableKitSettingsService
{
    private readonly YokiFrameProjectSettingsStore? mSettingsStore;

    /// <summary>创建按调用方项目根解析 Store 的 TableKit 配置服务。</summary>
    public TableKitSettingsService()
    {
    }

    /// <summary>创建复用指定项目配置 Store 的 TableKit 配置服务。</summary>
    /// <param name="settingsStore">项目级统一配置 Store。</param>
    public TableKitSettingsService(YokiFrameProjectSettingsStore settingsStore)
    {
        mSettingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
    }

    /// <summary>读取项目级 TableKit 配置；文件不存在或 JSON 损坏时返回默认配置。</summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <param name="defaults">页面默认配置。</param>
    /// <returns>绑定当前项目根的配置。</returns>
    public TableKitOptions Load(string projectRoot, TableKitOptions defaults)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        YokiFrameProjectOwnedDocumentSnapshot snapshot = GetStore(projectRoot)
            .ReadOwnedDocument(YokiFrameProjectSettingsTarget.TableKitDraft);
        if (!snapshot.Exists || string.IsNullOrWhiteSpace(snapshot.Content)) return defaults;

        try
        {
            TableKitOptions? loaded = JsonSerializer.Deserialize(
                snapshot.Content,
                TableKitSettingsJsonContext.Default.TableKitOptions);
            if (loaded == null) return defaults;
            return loaded with { ProjectRoot = defaults.ProjectRoot };
        }
        catch (JsonException)
        {
            return defaults;
        }
    }

    /// <summary>通过统一 Gateway 提交 Workbench-only 草稿，锁、revision、flush 和原子替换由 Store 负责。</summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <param name="options">待保存配置。</param>
    public void Save(string projectRoot, TableKitOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        string json = JsonSerializer.Serialize(
            options with { ProjectRoot = Path.GetFullPath(projectRoot) },
            TableKitSettingsJsonContext.Default.TableKitOptions);
        YokiFrameProjectOwnedDocumentWriteResult result = GetStore(projectRoot)
            .WriteOwnedDocumentAsync(
                YokiFrameProjectSettingsTarget.TableKitDraft,
                json,
                YokiFrameProjectSettingsWriteMode.MergeLatest,
                cancellationToken: CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        if (!result.Saved)
        {
            throw new IOException("TableKit Workbench settings changed while the draft was being committed.");
        }
    }

    /// <summary>获取绑定当前项目根的统一配置 Store，并拒绝跨项目复用。</summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <returns>项目配置 Store。</returns>
    private YokiFrameProjectSettingsStore GetStore(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            throw new ArgumentException("Project root is required.", nameof(projectRoot));
        }

        string normalizedRoot = Path.GetFullPath(projectRoot);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (mSettingsStore != null)
        {
            if (!string.Equals(mSettingsStore.ProjectRoot, normalizedRoot, comparison))
            {
                throw new InvalidOperationException("TableKit settings store is bound to a different project root.");
            }

            return mSettingsStore;
        }

        return new YokiFrameProjectSettingsStore(normalizedRoot);
    }
}

/// <summary>为 TableKit 项目配置提供 Native AOT 可用的 JSON 元数据。</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(TableKitOptions))]
internal sealed partial class TableKitSettingsJsonContext : JsonSerializerContext
{
}
