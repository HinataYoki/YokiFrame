using System.Text.Json;
using YokiFrame.Packaging.Models;

namespace YokiFrame.Packaging.Services;

/// <summary>
/// 原子读写项目级 Runtime 当前指针，避免包目录和项目缓存之间产生可变耦合。
/// </summary>
public sealed class RuntimeCachePointerStore
{
    private const int LAYOUT_VERSION = 1;
    /// <summary>
    /// 读取项目当前 Runtime 指针；文件不存在时返回空。
    /// </summary>
    /// <param name="projectRoot">目标 Unity 或 Godot 项目根。</param>
    /// <returns>已解析指针；不存在或内容无效时返回空。</returns>
    public RuntimeCachePointer? ReadIfExists(string projectRoot)
    {
        var path = YokiFrameWorkbenchRuntimeCacheLayout.GetCurrentFilePath(projectRoot);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var pointer = JsonSerializer.Deserialize(
                File.ReadAllText(path), RuntimePackagingJsonContext.Default.RuntimeCachePointer);
            return IsValid(pointer) ? pointer : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// 把已验证 Runtime 指纹原子设为当前指针；只在 Runtime profile 已完整提交后调用。
    /// </summary>
    /// <param name="projectRoot">目标 Unity 或 Godot 项目根。</param>
    /// <param name="sourceFingerprint">当前有效 Workbench 源码指纹。</param>
    public void Write(string projectRoot, string sourceFingerprint)
    {
        var pointer = new RuntimeCachePointer(LAYOUT_VERSION, sourceFingerprint, DateTimeOffset.UtcNow);
        var path = YokiFrameWorkbenchRuntimeCacheLayout.GetCurrentFilePath(projectRoot);
        var directory = Path.GetDirectoryName(path)
            ?? throw new DirectoryNotFoundException("Runtime cache pointer directory could not be resolved.");
        Directory.CreateDirectory(directory);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, pointer, RuntimePackagingJsonContext.Default.RuntimeCachePointer);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>
    /// 验证反序列化后的指针只含当前布局和安全 SHA-256 目录名。
    /// </summary>
    /// <param name="pointer">待验证指针。</param>
    /// <returns>指针可用于推导缓存目录时返回 true。</returns>
    private static bool IsValid(RuntimeCachePointer? pointer)
    {
        if (pointer == null || pointer.LayoutVersion != LAYOUT_VERSION)
        {
            return false;
        }

        try
        {
            _ = YokiFrameWorkbenchRuntimeCacheLayout.GetRuntimeRoot(".", pointer.SourceFingerprint);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
