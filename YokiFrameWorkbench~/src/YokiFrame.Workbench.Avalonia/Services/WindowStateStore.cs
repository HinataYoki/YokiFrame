using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;

namespace YokiFrame.Workbench.Avalonia.Services;

/// <summary>
/// 负责保存和恢复 Workbench 窗口尺寸与屏幕位置。
/// </summary>
public sealed partial class WindowStateStore
{
    private const string STATE_DIRECTORY = ".yokiframe";
    private const string WORKBENCH_DIRECTORY = "workbench";
    private const string STATE_FILE_NAME = "window-state.json";
    private const double MIN_WIDTH = 900;
    private const double MIN_HEIGHT = 680;

    private readonly string mStatePath;

    /// <summary>
    /// 创建窗口状态存储。
    /// </summary>
    /// <param name="projectRoot">Unity / 宿主项目根目录。</param>
    public WindowStateStore(string projectRoot)
    {
        var fullProjectRoot = Path.GetFullPath(projectRoot);
        mStatePath = Path.Combine(fullProjectRoot, STATE_DIRECTORY, WORKBENCH_DIRECTORY, STATE_FILE_NAME);
    }

    /// <summary>
    /// 读取上次关闭时保存的窗口位置和尺寸；状态缺失或不可见时回退默认居中。
    /// </summary>
    /// <param name="defaultWidth">默认窗口宽度。</param>
    /// <param name="defaultHeight">默认窗口高度。</param>
    /// <param name="defaultStartupLocation">默认启动定位策略。</param>
    /// <param name="workAreas">当前可用屏幕工作区；为空时只校验尺寸。</param>
    /// <returns>可直接应用到窗口的启动位置。</returns>
    public WindowPlacement Load(double defaultWidth, double defaultHeight, WindowStartupLocation defaultStartupLocation, IReadOnlyList<WindowWorkArea>? workAreas)
    {
        var defaultPlacement = new WindowPlacement(defaultWidth, defaultHeight, null, defaultStartupLocation);
        var state = ReadState();
        if (state == null || !IsValidSize(state.Width, state.Height))
        {
            return defaultPlacement;
        }

        var position = new PixelPoint(state.X, state.Y);
        if (!IsVisibleOnAnyWorkArea(position, state.Width, state.Height, workAreas))
        {
            return defaultPlacement;
        }

        return new WindowPlacement(state.Width, state.Height, position, WindowStartupLocation.Manual);
    }

    /// <summary>
    /// 保存当前页面，并只在 normal 状态下更新窗口矩形。
    /// </summary>
    /// <param name="position">窗口左上角屏幕像素坐标。</param>
    /// <param name="width">窗口宽度。</param>
    /// <param name="height">窗口高度。</param>
    /// <param name="windowState">窗口状态。</param>
    /// <param name="selectedPage">关闭时选中的稳定页面名称。</param>
    public void Save(
        PixelPoint position,
        double width,
        double height,
        WindowState windowState,
        string selectedPage)
    {
        try
        {
            var previousState = ReadState();
            var canUpdateBounds = windowState == WindowState.Normal && IsValidSize(width, height);
            var state = canUpdateBounds
                ? new PersistedWindowState(position.X, position.Y, width, height, selectedPage)
                : new PersistedWindowState(
                    previousState?.X ?? 0,
                    previousState?.Y ?? 0,
                    previousState?.Width ?? 0,
                    previousState?.Height ?? 0,
                    selectedPage);
            var directory = Path.GetDirectoryName(mStatePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(mStatePath, JsonSerializer.Serialize(state, WindowStateJsonContext.Default.PersistedWindowState) + Environment.NewLine);
        }
        catch (Exception)
        {
            // 窗口状态只是体验增强，保存失败不能影响关闭流程。
        }
    }

    /// <summary>读取上次关闭时的页面名称；旧状态或损坏状态返回空。</summary>
    /// <returns>尚未经过页面 Catalog 校验的稳定页面名称。</returns>
    public string LoadSelectedPage()
    {
        return ReadState()?.SelectedPage ?? string.Empty;
    }

    /// <summary>
    /// 尝试读取持久化状态；文件损坏或缺失时返回空。
    /// </summary>
    /// <returns>持久化状态；不可用时返回 null。</returns>
    private PersistedWindowState? ReadState()
    {
        try
        {
            if (!File.Exists(mStatePath))
            {
                return null;
            }

            return JsonSerializer.Deserialize(File.ReadAllText(mStatePath), WindowStateJsonContext.Default.PersistedWindowState);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 校验窗口尺寸是否可用，避免损坏状态造成不可操作的小窗口。
    /// </summary>
    /// <param name="width">窗口宽度。</param>
    /// <param name="height">窗口高度。</param>
    /// <returns>尺寸可恢复时返回 true。</returns>
    private static bool IsValidSize(double width, double height)
    {
        return width >= MIN_WIDTH
            && height >= MIN_HEIGHT
            && !double.IsNaN(width)
            && !double.IsNaN(height)
            && !double.IsInfinity(width)
            && !double.IsInfinity(height);
    }

    /// <summary>
    /// 判断窗口矩形是否与任一屏幕工作区相交；没有屏幕信息时允许恢复。
    /// </summary>
    /// <param name="position">窗口左上角像素坐标。</param>
    /// <param name="width">窗口宽度。</param>
    /// <param name="height">窗口高度。</param>
    /// <param name="workAreas">屏幕工作区集合。</param>
    /// <returns>窗口至少部分可见时返回 true。</returns>
    private static bool IsVisibleOnAnyWorkArea(PixelPoint position, double width, double height, IReadOnlyList<WindowWorkArea>? workAreas)
    {
        if (workAreas == null || workAreas.Count == 0)
        {
            return true;
        }

        var widthPixels = Math.Max(1, (int)Math.Round(width));
        var heightPixels = Math.Max(1, (int)Math.Round(height));
        var windowRect = new PixelRect(position, new PixelSize(widthPixels, heightPixels));
        foreach (var workArea in workAreas)
        {
            if (workArea.Bounds.Intersects(windowRect))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 窗口状态文件的序列化模型。
    /// </summary>
    /// <param name="x">窗口左上角 X 坐标。</param>
    /// <param name="y">窗口左上角 Y 坐标。</param>
    /// <param name="width">窗口宽度。</param>
    /// <param name="height">窗口高度。</param>
    internal sealed class PersistedWindowState
    {
        /// <summary>
        /// 创建窗口状态文件模型。
        /// </summary>
        /// <param name="x">窗口左上角 X 坐标。</param>
        /// <param name="y">窗口左上角 Y 坐标。</param>
        /// <param name="width">窗口宽度。</param>
        /// <param name="height">窗口高度。</param>
        [JsonConstructor]
        public PersistedWindowState(int x, int y, double width, double height, string? selectedPage)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            SelectedPage = selectedPage ?? string.Empty;
        }

        /// <summary>
        /// 获取窗口左上角 X 坐标。
        /// </summary>
        public int X { get; }

        /// <summary>
        /// 获取窗口左上角 Y 坐标。
        /// </summary>
        public int Y { get; }

        /// <summary>
        /// 获取窗口宽度。
        /// </summary>
        public double Width { get; }

        /// <summary>
        /// 获取窗口高度。
        /// </summary>
        public double Height { get; }

        /// <summary>获取关闭时选中的稳定页面名称。</summary>
        public string SelectedPage { get; }
    }

    /// <summary>
    /// 为窗口状态文件提供 Native AOT 友好的 System.Text.Json 元数据。
    /// </summary>
    [JsonSourceGenerationOptions(
        GenerationMode = JsonSourceGenerationMode.Metadata,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true)]
    [JsonSerializable(typeof(PersistedWindowState))]
    private sealed partial class WindowStateJsonContext : JsonSerializerContext
    {
    }
}
