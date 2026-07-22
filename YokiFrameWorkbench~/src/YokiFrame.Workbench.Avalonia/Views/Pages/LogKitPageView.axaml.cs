using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace YokiFrame.Workbench.Avalonia.Views.Pages;

/// <summary>承载 LogKit 项目配置与 Runtime 生效状态的单页工作台。</summary>
public sealed partial class LogKitPageView : UserControl
{
    private const double TWO_COLUMN_LAYOUT_WIDTH = 920D;

    /// <summary>初始化 LogKit 页面组件。</summary>
    public LogKitPageView()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
        ApplyResponsiveLayout(0D);
    }

    /// <summary>按页面真实可用宽度切换配置区的等分双列与单列堆叠，避免固定卡片宽度造成空白。</summary>
    /// <param name="sender">触发布局变化的 LogKit 页面。</param>
    /// <param name="eventArgs">包含最新页面尺寸的事件参数。</param>
    private void OnSizeChanged(object? sender, SizeChangedEventArgs eventArgs)
    {
        ApplyResponsiveLayout(eventArgs.NewSize.Width);
    }

    /// <summary>将文件卡片固定在顶部全宽，并按可用宽度排布下方两张配置卡片。</summary>
    /// <param name="availableWidth">LogKit 页面当前可用于 Banner、配置区和 Footer 的完整宽度。</param>
    private void ApplyResponsiveLayout(double availableWidth)
    {
        bool useTwoColumns = availableWidth >= TWO_COLUMN_LAYOUT_WIDTH;
        Classes.Set("logkit-wide", useTwoColumns);
        Classes.Set("logkit-compact", !useTwoColumns);

        ConfigGrid.ColumnDefinitions = new ColumnDefinitions(useTwoColumns ? "*,*" : "*");
        ConfigGrid.RowDefinitions = new RowDefinitions(useTwoColumns ? "Auto,Auto" : "Auto,Auto,Auto");

        Grid.SetRow(FileSettingsCard, 0);
        Grid.SetColumn(FileSettingsCard, 0);
        Grid.SetColumnSpan(FileSettingsCard, useTwoColumns ? 2 : 1);
        FileSettingsCard.Width = double.NaN;
        FileSettingsCard.MaxWidth = double.PositiveInfinity;

        Grid.SetRow(OutputSettingsCard, 1);
        Grid.SetColumn(OutputSettingsCard, 0);
        Grid.SetColumnSpan(OutputSettingsCard, 1);

        Grid.SetRow(CapacitySettingsCard, useTwoColumns ? 1 : 2);
        Grid.SetColumn(CapacitySettingsCard, useTwoColumns ? 1 : 0);
        Grid.SetColumnSpan(CapacitySettingsCard, 1);

        if (useTwoColumns)
        {
            FileFieldsGrid.ColumnDefinitions = new ColumnDefinitions("2*,*,*");
            FileFieldsGrid.RowDefinitions = new RowDefinitions("Auto");
            Grid.SetColumn(LogDirectoryField, 0);
            Grid.SetRow(LogDirectoryField, 0);
            Grid.SetColumn(EditorFileNameField, 1);
            Grid.SetRow(EditorFileNameField, 0);
            Grid.SetColumn(PlayerFileNameField, 2);
            Grid.SetRow(PlayerFileNameField, 0);
        }
        else
        {
            FileFieldsGrid.ColumnDefinitions = new ColumnDefinitions("*");
            FileFieldsGrid.RowDefinitions = new RowDefinitions("Auto,Auto,Auto");
            Grid.SetColumn(LogDirectoryField, 0);
            Grid.SetRow(LogDirectoryField, 0);
            Grid.SetColumn(EditorFileNameField, 0);
            Grid.SetRow(EditorFileNameField, 1);
            Grid.SetColumn(PlayerFileNameField, 0);
            Grid.SetRow(PlayerFileNameField, 2);
        }
    }

    /// <summary>点击设置名称时切换关联开关，扩大二元配置的有效点击区域。</summary>
    /// <param name="sender">携带目标开关名称的设置标签。</param>
    /// <param name="eventArgs">点击手势参数。</param>
    private void OnSettingLabelPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (sender is not Control { Tag: string toggleName }
            || this.FindControl<ToggleSwitch>(toggleName) is not { IsEffectivelyEnabled: true } toggle)
        {
            return;
        }

        if (eventArgs.Source is Visual source)
        {
            for (Visual? current = source; current != null; current = current.GetVisualParent())
            {
                if (ReferenceEquals(current, toggle))
                {
                    return;
                }
            }
        }

        toggle.IsChecked = toggle.IsChecked != true;
        eventArgs.Handled = true;
    }
}
