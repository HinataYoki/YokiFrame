namespace YokiFrame.Workbench.Avalonia.Pages;

/// <summary>
/// 描述 Workbench 页面使用专用总览布局还是统一详情段落布局。
/// </summary>
public enum WorkbenchPagePresentation
{
    /// <summary>
    /// 使用 Framework 专用工作台总览布局。
    /// </summary>
    Overview,

    /// <summary>
    /// 使用标题和结构化段落列表布局。
    /// </summary>
    Detail,

    /// <summary>
    /// 使用 EventKit 专用实时事件观察工作台。
    /// </summary>
    EventKit,

    /// <summary>
    /// 使用 FsmKit 专用实时诊断工作台。
    /// </summary>
    FsmKit,

    /// <summary>
    /// 使用 LogKit 专用运行配置工作台。
    /// </summary>
    LogKit,

    /// <summary>
    /// 使用 PoolKit 专用对象池诊断工作台。
    /// </summary>
    PoolKit,

    /// <summary>
    /// 使用 ResKit 专用资源与卸载历史诊断工作台。
    /// </summary>
    ResKit,

    /// <summary>
    /// 使用 ActionKit 专用活动树与终态诊断工作台。
    /// </summary>
    ActionKit,

    /// <summary>
    /// 使用 AudioKit 专用混音、voice 与历史工作台。
    /// </summary>
    AudioKit,

    /// <summary>使用 SpatialKit 专用索引实例和密度诊断工作台。</summary>
    SpatialKit,

    /// <summary>使用 Unity UIKit 专用 Runtime 面板和栈诊断工作台。</summary>
    UIKit,

    /// <summary>使用 TableKit Luban 验证与生成工作台。</summary>
    TableKit,

    /// <summary>使用 LocalizationKit 文本搜索与缺失诊断工作台。</summary>
    LocalizationKit,

    /// <summary>使用 SaveKit 存档目录配置与文件浏览工作台。</summary>
    SaveKit,

    /// <summary>
    /// 使用包内离线文档阅读器。
    /// </summary>
    Documentation,

}
