namespace YokiFrame.Tooling.Application.Packages;

/// <summary>
/// 描述从 YokiFrame package.json 读取的用户可见包版本与仓库主页。
/// </summary>
public sealed class YokiFramePackageMetadata
{
    /// <summary>
    /// 创建经过验证的 YokiFrame 包元数据。
    /// </summary>
    /// <param name="version">非空包版本。</param>
    /// <param name="repositoryUri">可由系统浏览器打开的 HTTPS 仓库主页。</param>
    public YokiFramePackageMetadata(string version, Uri repositoryUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(repositoryUri);
        if (!repositoryUri.IsAbsoluteUri
            || !string.Equals(repositoryUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("YokiFrame 仓库主页必须是绝对 HTTPS 地址。", nameof(repositoryUri));
        }

        Version = version;
        RepositoryUri = repositoryUri;
    }

    /// <summary>
    /// 获取 package.json 中的当前包版本。
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// 获取从 repository.url 规范化得到的仓库主页。
    /// </summary>
    public Uri RepositoryUri { get; }
}
