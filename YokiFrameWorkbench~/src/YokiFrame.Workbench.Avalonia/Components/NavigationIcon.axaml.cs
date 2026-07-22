using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace YokiFrame.Workbench.Avalonia.Components;

/// <summary>
/// 根据稳定图标键解析 Tauri 对齐的矢量路径和主题语义色。
/// </summary>
public sealed partial class NavigationIcon : UserControl
{
    /// <summary>
    /// 定义导航图标稳定键属性。
    /// </summary>
    public static readonly StyledProperty<string> IconKeyProperty = AvaloniaProperty.Register<NavigationIcon, string>(
        nameof(IconKey),
        "framework");

    /// <summary>
    /// 初始化图标视图，并监听主题切换以刷新动态语义色。
    /// </summary>
    public NavigationIcon()
    {
        InitializeComponent();
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
        UpdateIcon();
    }

    /// <summary>
    /// 获取或设置用于解析图标资源的稳定键。
    /// </summary>
    public string IconKey
    {
        get => GetValue(IconKeyProperty);
        set => SetValue(IconKeyProperty, value);
    }

    /// <summary>
    /// 在进入应用视觉树后再次解析资源，确保父级主题字典已经可用。
    /// </summary>
    /// <param name="eventArgs">视觉树挂载事件参数。</param>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        UpdateIcon();
    }

    /// <summary>
    /// 在图标键变化后重新解析路径和颜色，其余 Avalonia 属性继续交给基类处理。
    /// </summary>
    /// <param name="change">当前属性变化信息。</param>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconKeyProperty)
        {
            UpdateIcon();
        }
    }

    /// <summary>
    /// 在浅色或深色主题变化后重新读取对应的 Tauri 图标语义色。
    /// </summary>
    /// <param name="sender">触发主题变化的图标控件。</param>
    /// <param name="eventArgs">主题变化事件参数。</param>
    private void OnActualThemeVariantChanged(object? sender, EventArgs eventArgs)
    {
        UpdateIcon();
    }

    /// <summary>
    /// 从应用资源中解析当前图标路径和主题色；未知键回落到框架图标。
    /// </summary>
    private void UpdateIcon()
    {
        var suffix = ResolveResourceSuffix(IconKey);
        if (this.TryFindResource("Icon.Navigation." + suffix, ActualThemeVariant, out var geometryResource)
            && geometryResource is Geometry geometry)
        {
            IconPath.Data = geometry;
        }

        if (this.TryFindResource("Brush.Icon." + suffix, ActualThemeVariant, out var brushResource)
            && brushResource is IBrush brush)
        {
            IconPath.Stroke = brush;
            IconPath.Fill = UsesFilledDetails(suffix) ? brush : Brushes.Transparent;
        }
    }

    /// <summary>
    /// 把页面模块使用的小写图标键转换为资源后缀，并限制为当前受支持集合。
    /// </summary>
    /// <param name="iconKey">页面模块提供的稳定图标键。</param>
    /// <returns>可安全拼接到资源键后的后缀。</returns>
    private static string ResolveResourceSuffix(string? iconKey)
    {
        return iconKey?.Trim().ToLowerInvariant() switch
        {
            "docs" => "Docs",
            "codegen" or "codegenkit" => "CodeGenKit",
            "inspector" or "inspectorkit" => "InspectorKit",
            "event" or "eventkit" => "EventKit",
            "fsm" or "fsmkit" => "Fsm",
            "log" or "logkit" => "LogKit",
            "pool" or "poolkit" => "PoolKit",
            "res" or "reskit" => "ResKit",
            "singleton" or "singletonkit" => "SingletonKit",
            "action" or "actionkit" => "ActionKit",
            "audio" or "audiokit" => "AudioKit",
            "localization" or "localizationkit" => "LocalizationKit",
            "scene" or "scenekit" => "SceneKit",
            "spatial" or "spatialkit" => "SpatialKit",
            "ui" or "uikit" => "UIKit",
            "table" or "tablekit" => "TableKit",
            "toolclass" => "ToolClass",
            "save" or "savekit" => "SaveKit",
            _ => "Framework"
        };
    }

    /// <summary>
    /// 判断文档基准图标是否包含需与轮廓同色填充的细节。
    /// </summary>
    /// <param name="suffix">已解析的图标资源后缀。</param>
    /// <returns>包含实心细节时返回 true。</returns>
    private static bool UsesFilledDetails(string suffix)
    {
        return suffix is "Fsm" or "SpatialKit" or "SceneKit";
    }
}
