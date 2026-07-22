using System.Security.Cryptography;
using System.Text;

namespace YokiFrame.Tooling.Application.ProjectModel;

/// <summary>
/// 计算只随项目事实变化的 Project Model input hash，不把生成时间或绝对路径纳入指纹。
/// </summary>
internal static class ProjectModelInputHash
{
    private const string GENERATOR_VERSION = "yokiframe-project-model-v1";

    /// <summary>
    /// 根据规范化事实和源文件 hash 计算稳定 SHA-256。
    /// </summary>
    /// <param name="snapshot">本次扫描得到的项目事实。</param>
    /// <returns>小写十六进制 SHA-256。</returns>
    public static string Compute(ProjectModelSourceSnapshot snapshot)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, "generator", GENERATOR_VERSION);
        Append(hash, "project.kind", snapshot.ProjectKind);
        Append(hash, "project.engineVersion", snapshot.EngineVersion);
        Append(hash, "package.name", snapshot.PackageName);
        Append(hash, "package.version", snapshot.PackageVersion);
        Append(hash, "package.root", snapshot.PackageRelativeRoot);
        Append(hash, "package.source", snapshot.PackageSource);
        // Harness 是由 Project Model 生成的 bootstrap 投影，不属于输入事实；把它纳入 hash 会让首轮生成后再次 refresh 非幂等。
        foreach (var source in snapshot.SourceFiles.OrderBy(static item => item.RelativePath, StringComparer.Ordinal))
        {
            Append(hash, "source", source.RelativePath + "=" + source.Sha256);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    /// <summary>使用带长度的 UTF-8 key/value 帧加入 hash，避免拼接歧义。</summary>
    private static void Append(IncrementalHash hash, string key, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(key + "\0" + value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}
