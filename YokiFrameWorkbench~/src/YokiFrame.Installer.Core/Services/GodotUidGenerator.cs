using System.Text;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 使用旧版一致的 FNV-1a 64 与 Godot 资源 UID 字母表生成确定性 UID。
/// </summary>
public sealed class GodotUidGenerator
{
    private const string GODOT_UID_ALPHABET = "abcdefghijklmnopqrstuvwxy012345678";
    private const ulong GODOT_UID_ID_MASK = 0x7FFF_FFFF_FFFF_FFFF;
    private const ulong FNV1A_OFFSET_BASIS = 14_695_981_039_346_656_037;
    private const ulong FNV1A_PRIME = 1_099_511_628_211;

    /// <summary>
    /// 按资源 res 路径生成不含换行的确定性 uid:// 文本，路径大小写不影响结果。
    /// </summary>
    /// <param name="resourcePath">Godot res:// 资源路径。</param>
    /// <returns>使用 Godot 字母表编码的 UID 文本。</returns>
    public string Generate(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            throw new ArgumentException("Godot resource path must not be empty.", nameof(resourcePath));
        }

        var normalizedPath = resourcePath.ToLowerInvariant();
        var id = ComputeFnv1A64(normalizedPath) & GODOT_UID_ID_MASK;
        if (id == 0)
        {
            id = 1;
        }

        return "uid://" + EncodeId(id);
    }

    /// <summary>
    /// 验证 UID 文本前缀、非空正文和 Godot a-y、0-8 字母表；允许首尾空白和换行。
    /// </summary>
    /// <param name="value">待验证 UID 文本。</param>
    /// <returns>可由 Godot 识别时返回 true。</returns>
    public bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (!trimmed.StartsWith("uid://", StringComparison.Ordinal) || trimmed.Length == "uid://".Length)
        {
            return false;
        }

        return trimmed["uid://".Length..].All(static character =>
            character is >= 'a' and <= 'y' or >= '0' and <= '8');
    }

    /// <summary>
    /// 对 UTF-8 路径执行无符号 64 位 FNV-1a，乘法溢出按算法要求环绕。
    /// </summary>
    /// <param name="value">已规范化的小写资源路径。</param>
    /// <returns>完整 64 位 FNV-1a 哈希。</returns>
    private static ulong ComputeFnv1A64(string value)
    {
        var hash = FNV1A_OFFSET_BASIS;
        foreach (var valueByte in Encoding.UTF8.GetBytes(value))
        {
            hash ^= valueByte;
            hash = unchecked(hash * FNV1A_PRIME);
        }

        return hash;
    }

    /// <summary>
    /// 将非零 63 位资源 ID 转换为 Godot 自定义 34 进制文本。
    /// </summary>
    /// <param name="id">非零资源 ID。</param>
    /// <returns>不含 uid:// 前缀的 Godot UID 正文。</returns>
    private static string EncodeId(ulong id)
    {
        StringBuilder reversed = new();
        do
        {
            var index = (int)(id % (ulong)GODOT_UID_ALPHABET.Length);
            reversed.Append(GODOT_UID_ALPHABET[index]);
            id /= (ulong)GODOT_UID_ALPHABET.Length;
        }
        while (id != 0);

        var characters = reversed.ToString().ToCharArray();
        Array.Reverse(characters);
        return new string(characters);
    }
}
