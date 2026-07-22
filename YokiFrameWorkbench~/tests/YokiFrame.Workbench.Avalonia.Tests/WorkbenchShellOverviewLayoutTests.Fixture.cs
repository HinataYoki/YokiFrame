using System.Reflection;
using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Tooling.Application.Models;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 提供框架总览布局测试使用的状态夹具、反射入口和源码定位方法。
/// </summary>
public sealed partial class WorkbenchShellOverviewLayoutTests
{
    /// <summary>
    /// 创建包含 snapshot 成功和失败状态的 dashboard，验证总览区能同时展示两类结果。
    /// </summary>
    /// <returns>测试用 dashboard 状态。</returns>
    private static WorkbenchDashboardState CreateDashboardState()
    {
        return CreateDashboardState("F:/Project");
    }

    /// <summary>
    /// 创建包含 snapshot 成功和失败状态的 dashboard，验证总览区能同时展示两类结果。
    /// </summary>
    /// <param name="projectRoot">测试用项目根目录。</param>
    /// <returns>测试用 dashboard 状态。</returns>
    private static WorkbenchDashboardState CreateDashboardState(string projectRoot)
    {
        FileBridgeStatus status = new(
            "unity-editor",
            projectRoot + "/.yokiframe/engines/unity-editor",
            projectRoot + "/.yokiframe/engines/unity-editor/commands",
            projectRoot + "/.yokiframe/engines/unity-editor/results");
        WorkbenchBridgeHealth health = new(
            WorkbenchBridgeConnectionState.Online,
            "FileBridge is online for unity-editor.",
            "No action needed.",
            new[] { status.EngineRoot },
            1,
            15,
            "test-session",
            7,
            "EditMode",
            3);
        WorkbenchSnapshotState[] snapshots =
        {
            new("EventKit", "workbench", projectRoot + "/event.json", "snapshot", true, "{\"listeners\":2}", string.Empty),
            new("LogKit", "workbench", projectRoot + "/log.json", "telemetry", false, string.Empty, "stale")
        };

        return new WorkbenchDashboardState(
            projectRoot,
            DateTimeOffset.UtcNow,
            Array.Empty<EngineRegistryEntry>(),
            "unity-editor",
            status,
            health,
            null,
            snapshots,
            "{}",
            Array.Empty<string>());
    }

    /// <summary>
    /// 断言指定标题的卡片包含预期主值和详情。
    /// </summary>
    /// <param name="cards">卡片集合。</param>
    /// <param name="title">卡片标题。</param>
    /// <param name="value">预期主值。</param>
    /// <param name="detail">预期详情。</param>
    private static void AssertMetricCard(
        IReadOnlyList<WorkbenchMetricCard> cards,
        string title,
        string value,
        string detail)
    {
        var card = Assert.Single(cards, item => item.Title == title);
        Assert.Equal(value, card.Value);
        Assert.Equal(detail, card.Detail);
    }

    /// <summary>
    /// 从当前测试目录向上查找 Workbench Shell XAML，用于验证总览布局契约。
    /// </summary>
    /// <returns>Workbench Shell XAML 文本。</returns>
    private static string ReadWorkbenchShellViewXaml()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            foreach (var candidate in CreateWorkbenchShellViewXamlCandidates(directory.FullName))
            {
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 WorkbenchShellView.axaml。");
    }

    /// <summary>
    /// 从当前测试目录向上查找 MetricCard XAML，用于验证状态卡压缩契约。
    /// </summary>
    /// <returns>MetricCard XAML 文本。</returns>
    private static string ReadMetricCardXaml()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            foreach (var candidate in CreateMetricCardXamlCandidates(directory.FullName))
            {
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 MetricCard.axaml。");
    }

    /// <summary>
    /// 从当前测试目录向上查找 LogConsole XAML，用于验证日志行样式绑定契约。
    /// </summary>
    /// <returns>LogConsole XAML 文本。</returns>
    private static string ReadLogConsoleXaml()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            foreach (var candidate in CreateLogConsoleXamlCandidates(directory.FullName))
            {
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 LogConsole.axaml。");
    }

    /// <summary>
    /// 从当前测试目录向上查找 LogConsole code-behind，用于验证复制按钮实际访问剪贴板。
    /// </summary>
    /// <returns>LogConsole.axaml.cs 文本。</returns>
    private static string ReadLogConsoleCodeBehind()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            foreach (var candidate in CreateLogConsoleCodeBehindCandidates(directory.FullName))
            {
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 LogConsole.axaml.cs。");
    }

    /// <summary>
    /// 从当前测试目录向上查找终端样式 XAML，用于验证日志颜色样式契约。
    /// </summary>
    /// <returns>Terminal.axaml 文本。</returns>
    private static string ReadTerminalStylesXaml()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            foreach (var candidate in CreateTerminalStylesXamlCandidates(directory.FullName))
            {
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 Terminal.axaml。");
    }

    /// <summary>
    /// 断言日志行具备预期类型和消息片段，使用反射让缺失属性表现为明确的契约失败。
    /// </summary>
    /// <param name="line">待检查日志行。</param>
    /// <param name="expectedKind">预期日志类型名称。</param>
    /// <param name="expectedMessagePart">预期消息片段。</param>
    private static void AssertLogLineKind(WorkbenchLogLine line, string expectedKind, string expectedMessagePart)
    {
        var kindProperty = typeof(WorkbenchLogLine).GetProperty("Kind");

        Assert.NotNull(kindProperty);
        Assert.Equal(expectedKind, kindProperty.GetValue(line)?.ToString());
        Assert.Contains(expectedMessagePart, line.Message);
    }

    /// <summary>
    /// 通过反射调用日志文本生成方法，让缺失方法表现为明确的契约失败。
    /// </summary>
    /// <param name="viewModel">待检查 ViewModel。</param>
    /// <returns>用于复制到剪贴板的日志文本。</returns>
    private static string InvokeCreateLogClipboardText(WorkbenchShellViewModel viewModel)
    {
        var method = typeof(WorkbenchShellViewModel).GetMethod("CreateLogClipboardText", BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(method);
        return Assert.IsType<string>(method.Invoke(viewModel, Array.Empty<object>()));
    }

    /// <summary>
    /// 通过反射执行清空日志命令，让缺失命令表现为明确的契约失败。
    /// </summary>
    /// <param name="viewModel">待检查 ViewModel。</param>
    private static void InvokeClearLogCommand(WorkbenchShellViewModel viewModel)
    {
        InvokeCommandProperty(viewModel, "ClearLogCommand");
    }

    /// <summary>
    /// 通过反射执行 ViewModel 命令属性，避免新增 API 缺失时测试直接编译失败。
    /// </summary>
    /// <param name="viewModel">待检查 ViewModel。</param>
    /// <param name="propertyName">命令属性名。</param>
    private static void InvokeCommandProperty(WorkbenchShellViewModel viewModel, string propertyName)
    {
        var property = typeof(WorkbenchShellViewModel).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);
        var command = Assert.IsAssignableFrom<System.Windows.Input.ICommand>(property.GetValue(viewModel));
        Assert.True(command.CanExecute(null));
        command.Execute(null);
    }

    /// <summary>
    /// 通过反射设置 ViewModel 字符串属性，便于测试新增绑定属性。
    /// </summary>
    /// <param name="viewModel">待检查 ViewModel。</param>
    /// <param name="propertyName">属性名。</param>
    /// <param name="value">属性值。</param>
    private static void SetPropertyValue(WorkbenchShellViewModel viewModel, string propertyName, string value)
    {
        var property = typeof(WorkbenchShellViewModel).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);
        property.SetValue(viewModel, value);
    }

    /// <summary>
    /// 创建包含一个包内 Skill 的最小项目目录。
    /// </summary>
    /// <param name="skillName">Skill 名称。</param>
    /// <returns>测试项目根目录。</returns>
    private static string CreateProjectWithPackagedSkill(string skillName)
    {
        var root = Path.Combine(Path.GetTempPath(), "yokiframe-workbench-skill-tests", Guid.NewGuid().ToString("N"));
        var skillRoot = Path.Combine(root, "Assets", "YokiFrame", "Core", "Editor", "Skills", skillName);
        Directory.CreateDirectory(skillRoot);
        File.WriteAllText(Path.Combine(skillRoot, "SKILL.md"), "---\nname: " + skillName + "\ndescription: test\n---\n");
        return root;
    }

    /// <summary>
    /// 生成源码树和测试输出树下可能存在的 Workbench Shell XAML 路径。
    /// </summary>
    /// <param name="directory">当前向上探测的目录。</param>
    /// <returns>候选 XAML 路径。</returns>
    private static IEnumerable<string> CreateWorkbenchShellViewXamlCandidates(string directory)
    {
        yield return Path.Combine(
            directory,
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Views",
            "WorkbenchShellView.axaml");
        yield return Path.Combine(
            directory,
            "Assets",
            "YokiFrame",
            "YokiFrameWorkbench~",
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Views",
            "WorkbenchShellView.axaml");
    }

    /// <summary>
    /// 生成源码树和测试输出树下可能存在的 MetricCard XAML 路径。
    /// </summary>
    /// <param name="directory">当前向上探测的目录。</param>
    /// <returns>候选 XAML 路径。</returns>
    private static IEnumerable<string> CreateMetricCardXamlCandidates(string directory)
    {
        yield return Path.Combine(
            directory,
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Components",
            "MetricCard.axaml");
        yield return Path.Combine(
            directory,
            "Assets",
            "YokiFrame",
            "YokiFrameWorkbench~",
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Components",
            "MetricCard.axaml");
    }

    /// <summary>
    /// 生成源码树和测试输出树下可能存在的 LogConsole XAML 路径。
    /// </summary>
    /// <param name="directory">当前向上探测的目录。</param>
    /// <returns>候选 XAML 路径。</returns>
    private static IEnumerable<string> CreateLogConsoleXamlCandidates(string directory)
    {
        yield return Path.Combine(
            directory,
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Components",
            "LogConsole.axaml");
        yield return Path.Combine(
            directory,
            "Assets",
            "YokiFrame",
            "YokiFrameWorkbench~",
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Components",
            "LogConsole.axaml");
    }

    /// <summary>
    /// 生成源码树和测试输出树下可能存在的 LogConsole code-behind 路径。
    /// </summary>
    /// <param name="directory">当前向上探测的目录。</param>
    /// <returns>候选源码路径。</returns>
    private static IEnumerable<string> CreateLogConsoleCodeBehindCandidates(string directory)
    {
        yield return Path.Combine(
            directory,
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Components",
            "LogConsole.axaml.cs");
        yield return Path.Combine(
            directory,
            "Assets",
            "YokiFrame",
            "YokiFrameWorkbench~",
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Components",
            "LogConsole.axaml.cs");
    }

    /// <summary>
    /// 生成源码树和测试输出树下可能存在的终端样式 XAML 路径。
    /// </summary>
    /// <param name="directory">当前向上探测的目录。</param>
    /// <returns>候选 XAML 路径。</returns>
    private static IEnumerable<string> CreateTerminalStylesXamlCandidates(string directory)
    {
        yield return Path.Combine(
            directory,
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Styles",
            "Terminal.axaml");
        yield return Path.Combine(
            directory,
            "Assets",
            "YokiFrame",
            "YokiFrameWorkbench~",
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Styles",
            "Terminal.axaml");
    }
}
