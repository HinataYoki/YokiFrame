using System.Collections.Generic;
using System.IO;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖 Workbench 标题栏、左侧导航和 Skill 网格布局的外观契约，避免视觉细节回退。
/// </summary>
public sealed class WorkbenchShellChromeLayoutTests
{
    /// <summary>
    /// 验证标题栏圆形按钮使用统一的 36 像素方形尺寸，避免图标按钮大小不一致。
    /// </summary>
    [Fact]
    public void ChromeButtonStyleUsesUniformSquareSizing()
    {
        var buttons = ReadButtonsStyles();

        Assert.Contains("Button.icon-button", buttons);
        Assert.Contains("MinWidth\" Value=\"36\"", buttons);
        Assert.Contains("MinHeight\" Value=\"36\"", buttons);
        Assert.Contains("Height\" Value=\"36\"", buttons);
        Assert.Contains("Padding\" Value=\"0\"", buttons);
    }

    /// <summary>
    /// 验证标题栏使用 Stroke Path 渲染图标，避免 PathIcon 对开口几何的填充失真。
    /// </summary>
    [Fact]
    public void AppTitleBarUsesStrokePathsForToolbarIcons()
    {
        var xaml = ReadAppTitleBarXaml();

        Assert.Contains("xmlns:shapes=\"using:Avalonia.Controls.Shapes\"", xaml);
        Assert.Contains("<shapes:Path", xaml);
        Assert.DoesNotContain("PathIcon", xaml);
        Assert.Contains("StrokeThickness=\"1.75\"", xaml);
        Assert.Contains("StrokeLineCap=\"Round\"", xaml);
        Assert.Contains("StrokeJoin=\"Round\"", xaml);
        Assert.Contains("Data=\"{StaticResource Icon.Sun}\"", xaml);
        Assert.Contains("Data=\"{StaticResource Icon.Moon}\"", xaml);
        Assert.Contains("Data=\"{StaticResource Icon.Maximize}\"", xaml);
        Assert.Contains("Data=\"{StaticResource Icon.Close}\"", xaml);
    }

    /// <summary>
    /// 验证标题栏语言选择器使用单独的工具栏样式，确保它和图标按钮同高。
    /// </summary>
    [Fact]
    public void AppTitleBarUsesToolbarComboBoxStyle()
    {
        var xaml = ReadAppTitleBarXaml();
        var inputs = ReadInputsStyles();

        Assert.Contains("Classes=\"titlebar-select\"", xaml);
        Assert.Contains("ComboBox.titlebar-select", inputs);
        Assert.Contains("Height\" Value=\"36\"", inputs);
        Assert.Contains("MinHeight\" Value=\"36\"", inputs);
        Assert.Contains("HorizontalContentAlignment\" Value=\"Center\"", inputs);
    }

    /// <summary>
    /// 验证左侧导航和右侧工具窗口共享完整高度，品牌区为无背景的标题栏内容。
    /// </summary>
    [Fact]
    public void AppTitleBarBrandCardFillsSidebarFootprint()
    {
        var titleBar = ReadAppTitleBarXaml();
        var shell = ReadWorkbenchShellViewXaml();

        Assert.Contains("ColumnDefinitions=\"224,*,Auto,Auto,Auto,Auto,Auto\"", titleBar);
        Assert.Contains("Margin=\"16,8,18,8\"", titleBar);
        Assert.DoesNotContain("x:Name=\"BrandCard\"", titleBar);
        Assert.True(CountOccurrences(titleBar, "VerticalAlignment=\"Center\"") >= 5);
        Assert.Contains("RowDefinitions=\"60,*\"", shell);
        Assert.Contains("AppTitleBar ZIndex=\"1\"", shell);
        Assert.Contains("x:Name=\"BrandArea\"", shell);
        Assert.DoesNotContain("x:Name=\"BrandCard\"", shell);
        Assert.Contains("Grid.Row=\"0\"", shell);
        Assert.Contains("Height=\"60\"", shell);
        Assert.Contains("Width=\"48\"", shell);
        Assert.Contains("Height=\"48\"", shell);
        Assert.Contains("FontSize=\"20\"", shell);
        Assert.Contains("Margin=\"16,0,16,16\"", shell);
        Assert.Contains("<components:SideNavigation />", shell);
        Assert.Contains("x:Name=\"ToolWindowCard\"", shell);
        Assert.DoesNotContain("Margin=\"0,-24,0,0\"", shell);
    }

    /// <summary>验证所有页面共用的标题介绍区固定左对齐，不被右侧工具栏挤到星号列中央。</summary>
    [Fact]
    public void PageIntroductionAnchorsToHeaderLeftEdge()
    {
        var shell = ReadWorkbenchShellViewXaml();

        Assert.Contains("x:Name=\"PageIntroduction\"", shell);
        Assert.Contains("HorizontalAlignment=\"Left\"", shell);
    }

    /// <summary>
    /// 验证左侧导航使用 Tauri 对齐的矢量图标组件，不再使用方框和特殊字符充当图标。
    /// </summary>
    [Fact]
    public void SideNavigationUsesTauriVectorIcons()
    {
        var xaml = ReadSideNavigationXaml();
        var iconView = ReadWorkbenchFile("Components", "NavigationIcon.axaml");
        var iconSource = ReadWorkbenchFile("Components", "NavigationIcon.axaml.cs");
        var iconResources = ReadWorkbenchFile("Resources", "Icons.axaml");
        var colorResources = ReadWorkbenchFile("Resources", "Colors.axaml");
        var navigationStyles = ReadNavigationStyles();

        Assert.Contains("components:NavigationIcon", xaml);
        Assert.Contains("IconKey=\"{CompiledBinding IconKey}\"", xaml);
        Assert.DoesNotContain("CompiledBinding IconText", xaml);
        Assert.Contains("StrokeThickness=\"1.7\"", iconView);
        Assert.Contains("ActualThemeVariantChanged", iconSource);
        Assert.Contains("Icon.Navigation.Framework", iconResources);
        Assert.Contains("Icon.Navigation.Docs", iconResources);
        Assert.DoesNotContain("Icon.Navigation.Architecture", iconResources);
        Assert.Contains("Icon.Navigation.Fsm", iconResources);
        Assert.Contains("Icon.Navigation.TableKit", iconResources);
        Assert.Contains("Brush.Icon.Docs", colorResources);
        Assert.Equal(2, CountOccurrences(colorResources, "Brush.Icon.TableKit"));
        Assert.Contains("MinHeight\" Value=\"40\"", navigationStyles);
        Assert.Contains("Padding\" Value=\"10,8\"", navigationStyles);
    }

    /// <summary>
    /// 验证导航区和版本快捷链接区分别使用圆角面板，并让 GitHub 文本保持居中。
    /// </summary>
    [Fact]
    public void SideNavigationUsesSeparateRoundedCards()
    {
        var xaml = ReadSideNavigationXaml();

        Assert.Contains("x:Name=\"NavigationCard\"", xaml);
        Assert.Contains("x:Name=\"QuickLinksCard\"", xaml);
        Assert.Equal(2, CountOccurrences(xaml, "Classes=\"panel\""));
        Assert.Contains("Text=\"GitHub\"", xaml);
        Assert.Contains("Command=\"{CompiledBinding OpenRepositoryCommand}\"", xaml);
        Assert.Contains("Data=\"{StaticResource Icon.GitHub}\"", xaml);
        Assert.Contains("HorizontalAlignment=\"Center\"", xaml);
        Assert.Contains("TextAlignment=\"Center\"", xaml);
        Assert.Contains("Icon.GitHub", ReadWorkbenchFile("Resources", "Icons.axaml"));
    }

    /// <summary>
    /// 验证 Skill 安装区的状态卡和入口卡都使用三列网格，并保持两像素紧凑卡片间隔。
    /// </summary>
    [Fact]
    public void SkillInstallerCardsUseAlignedThreeColumnGrids()
    {
        var xaml = ReadWorkbenchShellViewXaml();
        var panels = ReadPanelsStyles();
        var buttons = ReadButtonsStyles();

        Assert.True(CountOccurrences(xaml, "UniformGrid Columns=\"3\"") >= 2);
        Assert.Contains("ItemsSource=\"{CompiledBinding SkillStatusCards}\"", xaml);
        Assert.Contains("ItemsSource=\"{CompiledBinding SkillOptions}\"", xaml);
        Assert.Contains("Border.metric-card", panels);
        Assert.Contains("Margin\" Value=\"2\"", panels);
        Assert.Contains("Button.skill-option-card", buttons);
        Assert.Contains("Margin\" Value=\"2\"", buttons);
    }

    /// <summary>
    /// 从测试目录向上查找标题栏 XAML。
    /// </summary>
    /// <returns>AppTitleBar.axaml 文本。</returns>
    private static string ReadAppTitleBarXaml()
    {
        return ReadWorkbenchFile("Components", "AppTitleBar.axaml");
    }

    /// <summary>
    /// 从测试目录向上查找 Workbench Shell XAML。
    /// </summary>
    /// <returns>WorkbenchShellView.axaml 文本。</returns>
    private static string ReadWorkbenchShellViewXaml()
    {
        return ReadWorkbenchFile("Views", "WorkbenchShellView.axaml");
    }

    /// <summary>
    /// 从测试目录向上查找左侧导航 XAML。
    /// </summary>
    /// <returns>SideNavigation.axaml 文本。</returns>
    private static string ReadSideNavigationXaml()
    {
        return ReadWorkbenchFile("Components", "SideNavigation.axaml");
    }

    /// <summary>
    /// 从测试目录向上查找按钮样式文件。
    /// </summary>
    /// <returns>Buttons.axaml 文本。</returns>
    private static string ReadButtonsStyles()
    {
        return ReadWorkbenchFile("Styles", "Buttons.axaml");
    }

    /// <summary>
    /// 从测试目录向上查找导航样式文件。
    /// </summary>
    /// <returns>Navigation.axaml 文本。</returns>
    private static string ReadNavigationStyles()
    {
        return ReadWorkbenchFile("Styles", "Navigation.axaml");
    }

    /// <summary>
    /// 从测试目录向上查找输入样式文件。
    /// </summary>
    /// <returns>Inputs.axaml 文本。</returns>
    private static string ReadInputsStyles()
    {
        return ReadWorkbenchFile("Styles", "Inputs.axaml");
    }

    /// <summary>
    /// 从测试目录向上查找面板样式文件。
    /// </summary>
    /// <returns>Panels.axaml 文本。</returns>
    private static string ReadPanelsStyles()
    {
        return ReadWorkbenchFile("Styles", "Panels.axaml");
    }

    /// <summary>
    /// 从测试输出目录向上搜索 Workbench 源文件树中的指定文件。
    /// </summary>
    /// <param name="subDirectory">相对 `src/YokiFrame.Workbench.Avalonia` 的子目录。</param>
    /// <param name="fileName">文件名。</param>
    /// <returns>读取到的文本内容。</returns>
    private static string ReadWorkbenchFile(string subDirectory, string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            foreach (var candidate in CreateWorkbenchFileCandidates(directory.FullName, subDirectory, fileName))
            {
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 Workbench 源文件。");
    }

    /// <summary>
    /// 生成源码树和测试输出树下可能存在的 Workbench 文件路径。
    /// </summary>
    /// <param name="directory">当前向上探测的目录。</param>
    /// <param name="subDirectory">目标子目录。</param>
    /// <param name="fileName">文件名。</param>
    /// <returns>候选文件路径。</returns>
    private static IEnumerable<string> CreateWorkbenchFileCandidates(string directory, string subDirectory, string fileName)
    {
        yield return Path.Combine(directory, "src", "YokiFrame.Workbench.Avalonia", subDirectory, fileName);
        yield return Path.Combine(directory, "Assets", "YokiFrame", "YokiFrameWorkbench~", "src", "YokiFrame.Workbench.Avalonia", subDirectory, fileName);
    }

    /// <summary>
    /// 统计文本中某个片段出现的次数，用于锁定重复网格声明的数量。
    /// </summary>
    /// <param name="text">待搜索文本。</param>
    /// <param name="value">要统计的片段。</param>
    /// <returns>出现次数。</returns>
    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while (true)
        {
            index = text.IndexOf(value, index, StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            index += value.Length;
        }
    }
}
