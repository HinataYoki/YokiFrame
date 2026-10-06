using System.Text.Json;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Pages;

/// <summary>
/// 把 Tooling.Application dashboard read model 投影为 Workbench 详情页段落。
/// </summary>
internal static class WorkbenchPageSectionProjector
{
    /// <summary>
    /// 创建 Framework 页对应的系统状态段落，供模块契约和未来复用使用。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    /// <returns>系统状态段落。</returns>
    internal static IReadOnlyList<WorkbenchDisplaySection> CreateFrameworkSections(WorkbenchDashboardState state)
    {
        return new[]
        {
            new WorkbenchDisplaySection("Project", state.ProjectRoot),
            new WorkbenchDisplaySection("Engines", string.Join(", ", state.Engines.Select(static engine => engine.EngineId))),
            new WorkbenchDisplaySection("Harness", state.HarnessSummary),
            new WorkbenchDisplaySection("Bridge Health", CreateBridgeHealthText(state.BridgeHealth)),
            new WorkbenchDisplaySection("Bridge Queues", CreateBridgeQueueText(state)),
            new WorkbenchDisplaySection("Evidence", string.Join(Environment.NewLine, state.BridgeHealth.EvidencePaths)),
            new WorkbenchDisplaySection("Errors", state.ErrorMessages.Count == 0 ? "none" : string.Join(Environment.NewLine, state.ErrorMessages))
        };
    }

    /// <summary>
    /// 创建 Engine 页段落：投影宿主引擎状态、执行目标与引擎操作开关。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    /// <returns>Engine 段落。</returns>
    internal static IReadOnlyList<WorkbenchDisplaySection> CreateEngineSections(WorkbenchDashboardState state)
    {
        WorkbenchSnapshotState? snapshot = state.Snapshots
            .FirstOrDefault(static item => string.Equals(item.Kit, "Engine", StringComparison.Ordinal));
        if (snapshot == null)
        {
            return new[]
            {
                new WorkbenchDisplaySection("Status", "Engine snapshot is not registered"),
                new WorkbenchDisplaySection("Next", "Rebuild the host so the Engine Kit registers its state snapshot.")
            };
        }

        if (!snapshot.Exists)
        {
            return new[]
            {
                new WorkbenchDisplaySection("Status", "unavailable"),
                new WorkbenchDisplaySection("Source", snapshot.Source),
                new WorkbenchDisplaySection("Reason", string.IsNullOrWhiteSpace(snapshot.ErrorMessage) ? snapshot.StaleReason : snapshot.ErrorMessage),
                new WorkbenchDisplaySection("Next", "Wait for the host to publish the Engine state snapshot, then refresh; if it never appears, rebuild the host."),
                new WorkbenchDisplaySection("Evidence", string.Join(Environment.NewLine, snapshot.EvidencePaths))
            };
        }

        var sections = new List<WorkbenchDisplaySection>
        {
            new("Source", snapshot.Source),
            new("Updated", snapshot.UpdatedAtUtc?.ToString("u") ?? "unknown")
        };
        try
        {
            using JsonDocument document = JsonDocument.Parse(snapshot.RawPayloadJson);
            JsonElement root = document.RootElement;
            sections.Add(new WorkbenchDisplaySection("Engine", ReadText(root, "engineKind") + " " + ReadText(root, "engineVersion")));
            sections.Add(new WorkbenchDisplaySection("Mode", ReadText(root, "mode") + " / target " + ReadText(root, "activeTarget")));
            sections.Add(new WorkbenchDisplaySection(
                "Play / Compile / Busy",
                ReadText(root, "isPlaying") + " / " + ReadText(root, "isCompiling") + " / " + ReadText(root, "isBusy")));
            string reason = ReadText(root, "settingsReason");
            sections.Add(new WorkbenchDisplaySection(
                "Execution switch",
                ReadText(root, "settingsState") + (string.IsNullOrWhiteSpace(reason) ? string.Empty : " - " + reason)));
            string sessionId = ReadText(root, "sessionId");
            sections.Add(new WorkbenchDisplaySection(
                "Session identity",
                (sessionId.Length > 0 ? sessionId : "unavailable")
                + " (generation " + ReadText(root, "generation")
                + ", available " + ReadText(root, "sessionIdentityAvailable") + ")"));
            sections.Add(new WorkbenchDisplaySection("Host targets", ReadText(root, "hostTargets")));
            AppendCapabilitySections(sections, root);
            AppendObjectSections(sections, root);
            AppendRecentRunSections(sections, root);
        }
        catch (JsonException exception)
        {
            sections.Add(new WorkbenchDisplaySection("Payload", exception.Message));
        }

        sections.Add(new WorkbenchDisplaySection(
            "Run an action",
            "yoki command send --kit Engine --action <action> [--payload '<json>']"
            + Environment.NewLine
            + "Dangerous actions also need \"confirmed\":true in the payload and a cli/workbench source; "
            + "with the execution switch off they are rejected while read-only actions keep working."));
        sections.Add(new WorkbenchDisplaySection(
            "Capabilities (detail)",
            "yoki command send --kit Engine --action engine_capabilities"));
        sections.Add(new WorkbenchDisplaySection("Evidence", string.Join(Environment.NewLine, snapshot.EvidencePaths)));
        return sections;
    }

    /// <summary>
    /// 追加能力矩阵段落：每个操作一行，逐目标可用性就地展示。
    /// </summary>
    /// <param name="sections">段落集合。</param>
    /// <param name="root">snapshot payload 根。</param>
    private static void AppendCapabilitySections(List<WorkbenchDisplaySection> sections, JsonElement root)
    {
        if (!root.TryGetProperty("capabilities", out JsonElement capabilities)
            || capabilities.ValueKind != JsonValueKind.Array)
        {
            sections.Add(new WorkbenchDisplaySection(
                "Operations",
                "snapshot does not carry a capability matrix; rebuild the host with the current Engine Kit"));
            return;
        }

        // 按 kind 分组计数：审查时最先关心的是"有哪些执行类、危险类操作"。
        var kindCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < capabilities.GetArrayLength(); index++)
        {
            string kindName = ReadText(capabilities[index], "kind");
            kindCounts.TryGetValue(kindName, out int current);
            kindCounts[kindName] = current + 1;
        }

        var kindSummary = new List<string>(kindCounts.Count);
        foreach (string ordered in new[] { "ReadOnly", "Maintenance", "UserAction", "Dangerous" })
        {
            if (kindCounts.TryGetValue(ordered, out int count))
            {
                kindSummary.Add(ordered + " " + count);
            }
        }

        foreach (KeyValuePair<string, int> pair in kindCounts)
        {
            if (pair.Key != "ReadOnly" && pair.Key != "Maintenance"
                && pair.Key != "UserAction" && pair.Key != "Dangerous")
            {
                kindSummary.Add(pair.Key + " " + pair.Value);
            }
        }

        sections.Add(new WorkbenchDisplaySection(
            "Operations",
            capabilities.GetArrayLength() + " registered"
            + (kindSummary.Count == 0 ? string.Empty : " (" + string.Join(" · ", kindSummary) + ")")));
        for (var index = 0; index < capabilities.GetArrayLength(); index++)
        {
            JsonElement capability = capabilities[index];
            string kind = ReadText(capability, "kind");
            sections.Add(new WorkbenchDisplaySection(
                "  " + ReadText(capability, "action"),
                kind + " -> " + DescribeTargetAvailability(capability)
                + (string.Equals(kind, "Dangerous", StringComparison.Ordinal)
                    ? "  [needs confirmed:true + cli/workbench source]"
                    : string.Empty)));
        }
    }

    /// <summary>
    /// 追加已注册服务的元数据摘要，不读取对象属性值。
    /// </summary>
    /// <param name="sections">段落集合。</param>
    /// <param name="root">snapshot payload 根。</param>
    private static void AppendObjectSections(List<WorkbenchDisplaySection> sections, JsonElement root)
    {
        if (!root.TryGetProperty("objectCatalogAvailable", out JsonElement available))
        {
            return;
        }

        if (available.ValueKind == JsonValueKind.False)
        {
            sections.Add(new WorkbenchDisplaySection(
                "Live services",
                "unavailable"));
            return;
        }

        int count = ReadInt(root, "objectCount");
        bool truncated = root.TryGetProperty("truncatedObjects", out JsonElement truncatedValue)
            && truncatedValue.ValueKind == JsonValueKind.True;
        sections.Add(new WorkbenchDisplaySection(
            "Live services",
            count + " discovered" + (truncated ? " (list truncated)" : string.Empty)));
        if (!root.TryGetProperty("objects", out JsonElement objects) || objects.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        for (var index = 0; index < objects.GetArrayLength(); index++)
        {
            JsonElement item = objects[index];
            sections.Add(new WorkbenchDisplaySection(
                "  " + ReadText(item, "type"),
                ReadText(item, "architecture") + " (" + ReadText(item, "assembly") + ")"));
        }
    }

    /// <summary>
    /// 追加最近运行段落（§10）：审查者不跑命令也能看到入口执行器实际跑了什么、结果路径在哪。
    /// </summary>
    /// <param name="sections">段落集合。</param>
    /// <param name="root">snapshot payload 根。</param>
    private static void AppendRecentRunSections(List<WorkbenchDisplaySection> sections, JsonElement root)
    {
        if (!root.TryGetProperty("recentRuns", out JsonElement runs) || runs.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        int count = ReadInt(root, "recentRunCount");
        bool truncated = root.TryGetProperty("truncatedRuns", out JsonElement truncatedValue)
            && truncatedValue.ValueKind == JsonValueKind.True;
        sections.Add(new WorkbenchDisplaySection(
            "Recent runs",
            (count == 0 ? "none recorded" : count + " recorded") + (truncated ? " (list truncated)" : string.Empty)));
        for (var index = 0; index < runs.GetArrayLength(); index++)
        {
            JsonElement run = runs[index];
            string errorCode = ReadText(run, "errorCode");
            sections.Add(new WorkbenchDisplaySection(
                "  " + (ReadText(run, "kind").Length > 0 ? ReadText(run, "kind") : "legacy"),
                ReadText(run, "state") + " @ " + ReadText(run, "target")
                + (errorCode.Length > 0 ? " - " + errorCode : string.Empty)
                + " (" + ReadText(run, "runId") + ")"));
        }
    }

    /// <summary>
    /// 把逐目标可用性压缩成一行文本，例如 <c>editor:Enabled, play:DisabledBySettings</c>。
    /// </summary>
    /// <param name="capability">能力条目。</param>
    /// <returns>可用性文本。</returns>
    private static string DescribeTargetAvailability(JsonElement capability)
    {
        if (!capability.TryGetProperty("targetAvailability", out JsonElement availability)
            || availability.ValueKind != JsonValueKind.Array)
        {
            return "unknown";
        }

        var parts = new List<string>(availability.GetArrayLength());
        for (var index = 0; index < availability.GetArrayLength(); index++)
        {
            JsonElement item = availability[index];
            parts.Add(ReadText(item, "target") + ":" + ReadText(item, "availability"));
        }

        return parts.Count == 0 ? "unknown" : string.Join(", ", parts);
    }

    /// <summary>
    /// 读取整数属性；缺失或不可解析时返回 0。
    /// </summary>
    /// <param name="element">JSON 对象。</param>
    /// <param name="propertyName">属性名。</param>
    /// <returns>整数值。</returns>
    private static int ReadInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out int parsed)
                ? parsed
                : 0;
    }

    /// <summary>
    /// 读取 JSON 属性文本；缺失或非标量时返回 unknown。
    /// </summary>
    /// <param name="element">JSON 对象。</param>
    /// <param name="propertyName">属性名。</param>
    /// <returns>属性文本。</returns>
    private static string ReadText(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
        {
            return "unknown";
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => value.ToString(),
            _ => value.ToString()
        };
    }

    /// <summary>
    /// 创建 Doctor 页段落；报告不可用时保留恢复建议。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    /// <returns>Doctor 段落。</returns>
    internal static IReadOnlyList<WorkbenchDisplaySection> CreateDoctorSections(WorkbenchDashboardState state)
    {
        var report = state.DoctorReport;
        if (report == null)
        {
            return new[]
            {
                new WorkbenchDisplaySection("Status", "unavailable"),
                new WorkbenchDisplaySection("Suggestion", state.BridgeHealth.Suggestion)
            };
        }

        return new[]
        {
            new WorkbenchDisplaySection("Level", report.Level),
            new WorkbenchDisplaySection("Issues", CreateDoctorIssueText(report.Issues)),
            new WorkbenchDisplaySection("Bridge Queues", CreateBridgeQueueText(state)),
            new WorkbenchDisplaySection("Heartbeat", CreateBridgeHealthText(state.BridgeHealth)),
            new WorkbenchDisplaySection("Generated", report.GeneratedAtUtc.ToLocalTime().ToString("HH:mm:ss"))
        };
    }

    /// <summary>
    /// 创建指定 Kit 的状态段落，只读取 Dashboard 已聚合的 snapshot，不触发额外 IO。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    /// <param name="kit">目标 Kit 名称。</param>
    /// <returns>Kit 状态段落。</returns>
    internal static IReadOnlyList<WorkbenchDisplaySection> CreateKitSections(
        WorkbenchDashboardState state,
        string kit)
    {
        var snapshot = state.Snapshots.FirstOrDefault(item => item.Kit == kit);
        if (snapshot == null)
        {
            return new[]
            {
                new WorkbenchDisplaySection("Kit", kit),
                new WorkbenchDisplaySection("Snapshot", "missing")
            };
        }

        return new[]
        {
            new WorkbenchDisplaySection("Kit", kit),
            new WorkbenchDisplaySection("Source", snapshot.Source),
            new WorkbenchDisplaySection("Path", snapshot.Path),
            new WorkbenchDisplaySection("Status", snapshot.Exists ? "available" : "missing"),
            new WorkbenchDisplaySection("Data", snapshot.Exists ? snapshot.PayloadPreview : snapshot.ErrorMessage)
        };
    }

    /// <summary>
    /// 创建 Documentation 页的实际项目路径和 harness 状态段落。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    /// <returns>文档路径段落。</returns>
    internal static IReadOnlyList<WorkbenchDisplaySection> CreateDocumentationSections(WorkbenchDashboardState state)
    {
        return new[]
        {
            new WorkbenchDisplaySection("Project", state.ProjectRoot),
            new WorkbenchDisplaySection("Documentation", Path.Combine(state.ProjectRoot, "Assets", "YokiFrame", "Documentation~")),
            new WorkbenchDisplaySection("Workbench", Path.Combine(state.ProjectRoot, "Assets", "YokiFrame", "YokiFrameWorkbench~")),
            new WorkbenchDisplaySection("Harness", state.HarnessSummary)
        };
    }

    /// <summary>
    /// 创建 FileBridge 健康摘要。
    /// </summary>
    /// <param name="health">FileBridge 健康状态。</param>
    /// <returns>可显示摘要。</returns>
    private static string CreateBridgeHealthText(WorkbenchBridgeHealth health)
    {
        var age = health.HeartbeatAgeSeconds.HasValue ? health.HeartbeatAgeSeconds.Value + "s" : "missing";
        return health.State
            + " | " + health.Message
            + " | heartbeatAge=" + age
            + " | threshold=" + health.StaleThresholdSeconds + "s"
            + " | session=" + CreateOptionalText(health.SessionId)
            + " | generation=" + health.Generation
            + " | sequence=" + health.Sequence
            + " | suggestion=" + health.Suggestion;
    }

    /// <summary>
    /// 创建 FileBridge 队列摘要。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    /// <returns>可显示摘要。</returns>
    private static string CreateBridgeQueueText(WorkbenchDashboardState state)
    {
        var status = state.BridgeStatus;
        if (status == null)
        {
            return "unavailable";
        }

        return "pending=" + status.PendingCount
            + ", processing=" + status.ProcessingCount
            + ", archive=" + status.ArchiveCount
            + ", deadletter=" + status.DeadletterCount
            + ", results=" + status.ResultCount;
    }

    /// <summary>
    /// 创建 Doctor issue 摘要文本。
    /// </summary>
    /// <param name="issues">诊断 issue 列表。</param>
    /// <returns>可显示摘要。</returns>
    private static string CreateDoctorIssueText(IReadOnlyList<WorkbenchDoctorIssue> issues)
    {
        if (issues.Count == 0)
        {
            return "none";
        }

        return string.Join(Environment.NewLine, issues.Select(static issue => issue.Code
            + " | " + issue.Message
            + " | suggestion=" + issue.Suggestion
            + " | evidence=" + string.Join(", ", issue.EvidencePaths)));
    }

    /// <summary>
    /// 把空文本转换为统一占位，避免状态段落出现空字段。
    /// </summary>
    /// <param name="value">待显示文本。</param>
    /// <returns>可显示文本。</returns>
    private static string CreateOptionalText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
    }
}
