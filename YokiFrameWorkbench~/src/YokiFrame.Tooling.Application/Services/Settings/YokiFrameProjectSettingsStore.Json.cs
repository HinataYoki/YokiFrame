using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace YokiFrame.Tooling.Application.Services.Settings;

public sealed partial class YokiFrameProjectSettingsStore
{
    /// <summary>读取并完整校验 Unity 稀疏 JSON；缺失文件创建空内存文档。</summary>
    internal static YokiFrameProjectSettingsBackendDocument LoadJsonBackendDocument(
        YokiFrameProjectSettingsTarget target,
        string path)
    {
        if (!File.Exists(path))
        {
            return new YokiFrameProjectSettingsBackendDocument(
                target, path, false, string.Empty, new List<YokiFrameProjectSetting>());
        }

        byte[] bytes = ReadBoundedFile(path);
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes);
            List<YokiFrameProjectSetting> settings = ParseJsonDocument(document.RootElement);
            return new YokiFrameProjectSettingsBackendDocument(
                target, path, true, Encoding.UTF8.GetString(bytes), settings, ComputeFingerprint(bytes));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("YokiFrame settings JSON is invalid: " + path, exception);
        }
    }

    /// <summary>校验 formatVersion 和 settings 数组并解析全部条目。</summary>
    private static List<YokiFrameProjectSetting> ParseJsonDocument(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("formatVersion", out JsonElement version)
            || !version.TryGetInt32(out int formatVersion)
            || formatVersion != FORMAT_VERSION
            || !root.TryGetProperty("settings", out JsonElement settings)
            || settings.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("YokiFrame settings require formatVersion 1 and a settings array.");
        }

        List<YokiFrameProjectSetting> result = new();
        var index = 0;
        foreach (JsonElement element in settings.EnumerateArray())
        {
            result.Add(ParseJsonSetting(element, index));
            index++;
        }

        return result;
    }

    /// <summary>解析并校验一个 kit/key/value 稀疏条目。</summary>
    private static YokiFrameProjectSetting ParseJsonSetting(JsonElement element, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("YokiFrame settings entry " + index + " must be an object.");
        }

        string owner = ReadJsonString(element, "kit", index, false);
        string key = ReadJsonString(element, "key", index, false);
        string value = ReadJsonString(element, "value", index, true);
        try
        {
            ValidateIdentifier(owner, "kit");
            ValidateIdentifier(key, "key");
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("YokiFrame settings entry " + index + " has an unsafe identifier.", exception);
        }

        return new YokiFrameProjectSetting(owner, key, value);
    }

    /// <summary>读取条目中的必需字符串，并按字段语义处理空值。</summary>
    private static string ReadJsonString(
        JsonElement element,
        string propertyName,
        int index,
        bool allowEmpty)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property)
            || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException(
                "YokiFrame settings entry " + index + " has invalid " + propertyName + ".");
        }

        string value = property.GetString() ?? string.Empty;
        if (!allowEmpty && value.Length == 0)
        {
            throw new InvalidDataException(
                "YokiFrame settings entry " + index + " has empty " + propertyName + ".");
        }

        return value;
    }

    /// <summary>以稳定 formatVersion/settings 结构序列化全部稀疏条目。</summary>
    internal static string SerializeJsonBackendDocument(IReadOnlyList<YokiFrameProjectSetting> settings)
    {
        JsonArray entries = new();
        foreach (YokiFrameProjectSetting setting in settings)
        {
            entries.Add(new JsonObject
            {
                ["kit"] = setting.Owner,
                ["key"] = setting.Key,
                ["value"] = setting.Value
            });
        }

        JsonObject root = new()
        {
            ["formatVersion"] = FORMAT_VERSION,
            ["settings"] = entries
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + global::System.Environment.NewLine;
    }
}
