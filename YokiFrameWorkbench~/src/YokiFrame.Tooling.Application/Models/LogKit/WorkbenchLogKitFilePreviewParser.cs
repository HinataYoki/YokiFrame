using System.Text.Json;

namespace YokiFrame.Tooling.Application.Models.LogKit;

/// <summary>解析显式 read_log_file 命令的有界文件预览。</summary>
internal static class WorkbenchLogKitFilePreviewParser
{
    /// <summary>解析文件预览并保留实际命令传输与证据。</summary>
    internal static WorkbenchLogKitFilePreview Parse(
        string resultJson,
        string requestedKind,
        string transport,
        IReadOnlyList<string> evidencePaths)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
        {
            throw new InvalidDataException("LogKit file preview response is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(resultJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("LogKit file preview response must be an object.");
            }

            return ParseRoot(root, requestedKind, transport, evidencePaths);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("LogKit file preview response is invalid JSON.", exception);
        }
    }

    /// <summary>解析已验证为对象的预览根。</summary>
    private static WorkbenchLogKitFilePreview ParseRoot(
        JsonElement root,
        string requestedKind,
        string transport,
        IReadOnlyList<string> evidencePaths)
    {
        var responseKind = WorkbenchLogKitJsonReader.ReadString(root, "kind");
        if (!string.Equals(responseKind, requestedKind, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("LogKit file preview kind does not match the requested source.");
        }

        return new WorkbenchLogKitFilePreview(
            requestedKind,
            WorkbenchLogKitJsonReader.ReadString(root, "path"),
            WorkbenchLogKitJsonReader.ReadString(root, "fileName"),
            WorkbenchLogKitJsonReader.ReadBoolean(root, "exists"),
            WorkbenchLogKitJsonReader.ReadInt64(root, "sizeBytes"),
            WorkbenchLogKitJsonReader.ReadString(root, "modifiedUtc"),
            WorkbenchLogKitJsonReader.ReadInt32(root, "lineCount"),
            WorkbenchLogKitJsonReader.ReadBoolean(root, "truncated"),
            WorkbenchLogKitJsonReader.ReadString(root, "content"),
            WorkbenchLogKitJsonReader.ReadString(root, "errorMessage"),
            transport ?? string.Empty,
            evidencePaths ?? Array.Empty<string>());
    }
}
