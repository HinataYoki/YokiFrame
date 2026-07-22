namespace YokiFrame.Packaging.Models;

/// <summary>
/// 描述项目级 Runtime 缓存当前指针；真实 Runtime 目录始终由 sourceFingerprint 推导。
/// </summary>
public sealed class RuntimeCachePointer
{
    /// <summary>
    /// 创建当前 Runtime 指针。
    /// </summary>
    /// <param name="layoutVersion">缓存布局版本。</param>
    /// <param name="sourceFingerprint">当前有效 Workbench 源码指纹。</param>
    /// <param name="updatedAtUtc">指针最后确认时间。</param>
    public RuntimeCachePointer(int layoutVersion, string sourceFingerprint, DateTimeOffset updatedAtUtc)
    {
        LayoutVersion = layoutVersion;
        SourceFingerprint = sourceFingerprint;
        UpdatedAtUtc = updatedAtUtc;
    }

    /// <summary>获取缓存布局版本。</summary>
    public int LayoutVersion { get; }

    /// <summary>获取当前有效 Workbench 源码指纹。</summary>
    public string SourceFingerprint { get; }

    /// <summary>获取指针最后确认时间。</summary>
    public DateTimeOffset UpdatedAtUtc { get; }
}
