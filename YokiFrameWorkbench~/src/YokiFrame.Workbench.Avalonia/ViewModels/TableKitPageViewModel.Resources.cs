namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>集中保存 TableKit 操作分部使用的本地化资源 key。</summary>
public sealed partial class TableKitPageViewModel
{
    /// <summary>按 Luban 表名寻址</summary>
    private const string AddressableModeKey = "String.TableKit.AddressableMode";

    /// <summary>Luban 配置目录不存在: {0}</summary>
    private const string ConfigDirMissingTemplateKey = "String.TableKit.ConfigDirMissingTemplate";

    /// <summary>已打开配置表目录: {0}</summary>
    private const string ConfigDirOpenedTemplateKey = "String.TableKit.ConfigDirOpenedTemplate";

    /// <summary>打开配置表目录失败: {0}</summary>
    private const string ConfigDirOpenFailedTemplateKey = "String.TableKit.ConfigDirOpenFailedTemplate";

    /// <summary>TableKit 配置已保存到当前项目。</summary>
    private const string ConfigSavedKey = "String.TableKit.ConfigSaved";

    /// <summary>luban.conf 解析失败: {0}</summary>
    private const string ConfParseFailedTemplateKey = "String.TableKit.ConfParseFailedTemplate";

    /// <summary>控制台已清空</summary>
    private const string ConsoleClearedKey = "String.TableKit.ConsoleCleared";

    /// <summary>控制台日志已复制到剪贴板。</summary>
    private const string ConsoleCopiedKey = "String.TableKit.ConsoleCopied";

    /// <summary>请确认工作目录包含 luban.conf，并配置 Luban.dll 或可执行文件路径。</summary>
    private const string EnvironmentMissingKey = "String.TableKit.EnvironmentMissing";

    /// <summary>已找到 luban.conf 和 Luban 工具，可以执行验证与生成。</summary>
    private const string EnvironmentReadyKey = "String.TableKit.EnvironmentReady";

    /// <summary>失败</summary>
    private const string FailedShortKey = "String.TableKit.FailedShort";

    /// <summary>当前没有可用剪贴板服务，请直接选择控制台文本。</summary>
    private const string NoClipboardKey = "String.TableKit.NoClipboard";

    /// <summary>当前窗口没有可用的目录选择器。</summary>
    private const string NoFolderPickerKey = "String.TableKit.NoFolderPicker";

    /// <summary>当前窗口没有可用的 Luban 文件选择器。</summary>
    private const string NoLubanFilePickerKey = "String.TableKit.NoLubanFilePicker";

    /// <summary>操作完成。</summary>
    private const string OperationDoneKey = "String.TableKit.OperationDone";

    /// <summary>未配置官方 Agent、MCP 或 Skill；旧版 Luban 可继续验证和生成。</summary>
    private const string OptionalLubanToolsEmptyKey = "String.TableKit.OptionalLubanToolsEmpty";

    /// <summary>已识别 {0}/3 项官方 AI 路径。配表需求由 YokiFrame Skill 自动读取这些路径并导入官方 Skill。</summary>
    private const string OptionalLubanToolsReadyKey = "String.TableKit.OptionalLubanToolsReady";

    /// <summary>选择 TableKit 数据输出目录</summary>
    private const string PickDataDirTitleKey = "String.TableKit.PickDataDirTitle";

    /// <summary>选择 Luban.Mcp.dll</summary>
    private const string PickLubanMcpTitleKey = "String.TableKit.PickLubanMcpTitle";

    /// <summary>选择 Luban Skill 目录</summary>
    private const string PickLubanSkillsTitleKey = "String.TableKit.PickLubanSkillsTitle";

    /// <summary>正在读取 Luban 临时输出。</summary>
    private const string ReadingTempOutputKey = "String.TableKit.ReadingTempOutput";

    /// <summary>已还原默认</summary>
    private const string ResetDetailKey = "String.TableKit.ResetDetail";

    /// <summary>已还原默认 TableKit 配置。</summary>
    private const string ResetDoneKey = "String.TableKit.ResetDone";

    /// <summary>已保存</summary>
    private const string SavedDetailKey = "String.TableKit.SavedDetail";

    /// <summary>保存失败</summary>
    private const string SaveFailedShortKey = "String.TableKit.SaveFailedShort";

    /// <summary>TableKit 配置保存失败: {0}</summary>
    private const string SaveFailedTemplateKey = "String.TableKit.SaveFailedTemplate";

    /// <summary>开始验证配置并生成临时 JSON 预览。</summary>
    private const string StartValidateKey = "String.TableKit.StartValidate";

    /// <summary>成功</summary>
    private const string SuccessKey = "String.TableKit.Success";

    /// <summary>已刷新 Luban target 列表。</summary>
    private const string TargetsRefreshedKey = "String.TableKit.TargetsRefreshed";

    /// <summary>正在验证</summary>
    private const string ValidatingKey = "String.TableKit.Validating";

    /// <summary>选择 Luban 工作目录</summary>
    private const string PickWorkDirTitleKey = "String.TableKit.PickWorkDirTitle";

    /// <summary>选择 Luban.dll</summary>
    private const string PickLubanDllTitleKey = "String.TableKit.PickLubanDllTitle";

    /// <summary>选择 Luban.Agent.dll</summary>
    private const string PickLubanAgentTitleKey = "String.TableKit.PickLubanAgentTitle";

    /// <summary>选择 TableKit 代码输出目录</summary>
    private const string PickCodeDirTitleKey = "String.TableKit.PickCodeDirTitle";

    /// <summary>选择 TableKit 编辑器数据目录</summary>
    private const string PickEditorDataDirTitleKey = "String.TableKit.PickEditorDataDirTitle";
}
