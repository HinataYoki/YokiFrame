using System.Text.Json;
using System.Text.Json.Nodes;
using YokiFrame;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Tooling.Application.Capabilities;

/// <summary>
/// `System/list_commands` 终态响应的统一读取器：把 wire JSON 解析为 Kit → actions 强类型目录。
/// 入口层（CLI / Avalonia）不得自行解析该 payload，必须经本读取器消费，保证安全过滤与容错语义单一。
/// </summary>
public static class CommandCatalogReader
{
    /// <summary>
    /// 容错解析命令目录 JSON：结构损坏时返回 false；单个不安全 Kit/action 标识被跳过而不是整体失败，
    /// 与宿主 capability 侧严格校验互补——此处只服务展示层的安全过滤需求。
    /// </summary>
    /// <param name="resultJson">System/list_commands 的业务结果 JSON。</param>
    /// <param name="catalog">解析成功的 Kit → action 列表目录（按 Ordinal 排序前原序保留）。</param>
    /// <returns>解析到至少一个可用 Kit 时返回 true。</returns>
    public static bool TryRead(
        string? resultJson,
        out IReadOnlyDictionary<string, IReadOnlyList<string>> catalog)
    {
        Dictionary<string, IReadOnlyList<string>> parsed = new(StringComparer.Ordinal);
        catalog = parsed;
        if (string.IsNullOrWhiteSpace(resultJson))
        {
            return false;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(resultJson);
        }
        catch (JsonException)
        {
            return false;
        }

        if (root?["kits"] is not JsonArray kits)
        {
            return false;
        }

        foreach (var kitNode in kits)
        {
            var kit = kitNode?["kit"]?.GetValue<string>() ?? string.Empty;
            var actions = kitNode?["actions"]?.AsArray();
            if (!YokiFrameSafeIdContract.IsSafeId(kit) || actions == null)
            {
                continue;
            }

            List<string> actionNames = new();
            foreach (var actionNode in actions)
            {
                var action = actionNode?["action"]?.GetValue<string>() ?? string.Empty;
                if (YokiFrameSafeIdContract.IsSafeId(action))
                {
                    actionNames.Add(action);
                }
            }

            if (actionNames.Count > 0)
            {
                parsed[kit] = actionNames;
            }
        }

        return parsed.Count > 0;
    }
}
