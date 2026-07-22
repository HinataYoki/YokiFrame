using System.Text.Json;
using YokiFrame.RuntimeCache;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 验证 Godot 安装前的项目 Runtime 缓存由当前待投影源码生成，并包含目标宿主 profile 的 GUI 与 CLI。
/// </summary>
public sealed class RuntimeCacheBindingValidator
{
    private const int LAYOUT_VERSION = 1;
    private const string RUNTIME_MANIFEST_FILE_NAME = "tool-manifest.json";

    /// <summary>
    /// 验证目标项目 current.json、fingerprint 目录和目标 profile 入口；任一不匹配都要求先从源码包执行 bootstrap。
    /// </summary>
    /// <param name="projectRoot">目标 Godot 项目根。</param>
    /// <param name="sourcePackageRoot">将被投影的完整 YokiFrame 源码包根。</param>
    /// <param name="runtimeProfile">当前宿主对应的 Runtime profile。</param>
    public void Validate(string projectRoot, string sourcePackageRoot, string runtimeProfile)
    {
        if (string.IsNullOrWhiteSpace(runtimeProfile))
        {
            throw new ArgumentException("Runtime profile is required.", nameof(runtimeProfile));
        }

        var sourceFingerprint = YokiFrameWorkbenchSourceFingerprint.Compute(sourcePackageRoot);
        var pointerPath = YokiFrameWorkbenchRuntimeCacheLayout.GetCurrentFilePath(projectRoot);
        if (!File.Exists(pointerPath))
        {
            throw CreateBootstrapRequiredException("Runtime 缓存指针不存在: " + pointerPath);
        }

        using JsonDocument pointer = ReadJson(pointerPath, "Runtime 缓存指针无效: ");
        var pointerRoot = pointer.RootElement;
        if (ReadInt32(pointerRoot, "layoutVersion") != LAYOUT_VERSION
            || !string.Equals(ReadString(pointerRoot, "sourceFingerprint"), sourceFingerprint, StringComparison.Ordinal))
        {
            throw CreateBootstrapRequiredException("Runtime 缓存与所选源码包不匹配。");
        }

        var runtimeRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetRuntimeRoot(projectRoot, sourceFingerprint);
        var manifestPath = Path.Combine(runtimeRoot, RUNTIME_MANIFEST_FILE_NAME);
        if (!File.Exists(manifestPath))
        {
            throw CreateBootstrapRequiredException("Runtime 缓存 manifest 不存在: " + manifestPath);
        }

        if (!RuntimeManifestIntegrityValidator.TryValidateProfile(
                manifestPath,
                runtimeRoot,
                runtimeProfile,
                requireCli: true,
                out _,
                out var error))
        {
            throw CreateBootstrapRequiredException("Runtime 缓存 manifest 无效: " + error);
        }
    }

    /// <summary>
    /// 读取 JSON 文件并将格式错误转换为可操作的 bootstrap 前置条件错误。
    /// </summary>
    /// <param name="path">JSON 文件完整路径。</param>
    /// <param name="errorPrefix">错误前缀。</param>
    /// <returns>已解析 JSON 文档。</returns>
    private static JsonDocument ReadJson(string path, string errorPrefix)
    {
        try
        {
            return JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException exception)
        {
            throw CreateBootstrapRequiredException(errorPrefix + exception.Message);
        }
    }

    /// <summary>
    /// 读取 JSON 对象中的字符串属性；缺失或类型不符时返回空文本。
    /// </summary>
    /// <param name="element">目标 JSON 对象。</param>
    /// <param name="propertyName">属性名称。</param>
    /// <returns>字符串值或空文本。</returns>
    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
    }

    /// <summary>
    /// 读取 JSON 对象中的整数属性；缺失或类型不符时返回 0。
    /// </summary>
    /// <param name="element">目标 JSON 对象。</param>
    /// <param name="propertyName">属性名称。</param>
    /// <returns>整数值或 0。</returns>
    private static int ReadInt32(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.TryGetInt32(out var result)
                ? result
                : 0;
    }

    /// <summary>
    /// 创建统一的前置条件异常，明确提示用户从待投影源码包先生成目标项目缓存。
    /// </summary>
    /// <param name="detail">具体不匹配说明。</param>
    /// <returns>可交给 UI/CLI 展示并识别恢复动作的异常。</returns>
    private static InvalidDataException CreateBootstrapRequiredException(string detail)
    {
        return RuntimeCacheBootstrapRequirement.Create(
            "Godot 安装需要先构建与当前源码包匹配的项目 Runtime 缓存。"
            + Environment.NewLine
            + detail
            + Environment.NewLine
            + "请运行源码包 YokiFrameWorkbench~/scripts/runtime-bootstrap/install-godot 脚本并传入 --project <GodotProjectRoot>，"
            + "或在 Installer 中点击“构建 Runtime 并重新打开”。");
    }
}
