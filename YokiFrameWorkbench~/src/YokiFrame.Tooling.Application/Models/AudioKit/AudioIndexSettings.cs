namespace YokiFrame.Tooling.Application.Models.AudioKit;

/// <summary>定义一个项目独立保存的 AudioKit 稳定索引生成配置。</summary>
/// <param name="ScanFolder">项目内音频扫描目录。</param>
/// <param name="OutputPath">项目内 C# 索引输出路径。</param>
/// <param name="ManifestPath">项目内稳定 ID 分配账本路径。</param>
/// <param name="NamespaceName">生成代码使用的命名空间。</param>
/// <param name="ClassName">生成常量容器类名。</param>
/// <param name="StartId">新音频第一次分配时使用的起始 ID。</param>
public sealed record AudioIndexSettings(
    string ScanFolder,
    string OutputPath,
    string ManifestPath,
    string NamespaceName,
    string ClassName,
    int StartId)
{
    /// <summary>创建新项目使用的稳定默认配置。</summary>
    /// <returns>以 Assets/Art/Audio 和 GameAudio 为基线的默认配置。</returns>
    public static AudioIndexSettings CreateDefault()
    {
        return new AudioIndexSettings(
            "Assets/Art/Audio",
            "Assets/Scripts/Generated/AudioIds.cs",
            "Assets/Settings/YokiFrame/audio-index.json",
            "GameAudio",
            "AudioIds",
            1001);
    }
}
