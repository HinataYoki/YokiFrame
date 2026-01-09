using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// UI 代码生成模板接口 - 用户可实现此接口自定义代码生成样式
    /// </summary>
    public interface IUICodeGenTemplate
    {
        /// <summary>
        /// 模板名称（用于配置选择）
        /// </summary>
        string TemplateName { get; }

        /// <summary>
        /// 模板描述
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 生成 Panel 用户文件
        /// </summary>
        /// <param name="context">生成上下文</param>
        void WritePanel(UICodeGenContext context);

        /// <summary>
        /// 生成 Panel Designer 文件
        /// </summary>
        /// <param name="context">生成上下文</param>
        void WritePanelDesigner(UICodeGenContext context);

        /// <summary>
        /// 生成绑定类型的用户文件（Element/Component）
        /// </summary>
        /// <param name="context">生成上下文</param>
        /// <param name="bindInfo">绑定信息</param>
        /// <param name="strategy">绑定策略</param>
        void WriteBindTypeUserFile(UICodeGenContext context, BindCodeInfo bindInfo, IBindTypeStrategy strategy);

        /// <summary>
        /// 生成绑定类型的 Designer 文件
        /// </summary>
        /// <param name="context">生成上下文</param>
        /// <param name="bindInfo">绑定信息</param>
        /// <param name="strategy">绑定策略</param>
        void WriteBindTypeDesignerFile(UICodeGenContext context, BindCodeInfo bindInfo, IBindTypeStrategy strategy);

        /// <summary>
        /// 递归生成绑定类型代码（Element/Component）
        /// </summary>
        /// <param name="bindInfo">绑定信息</param>
        /// <param name="context">生成上下文</param>
        void WriteBindTypeCode(BindCodeInfo bindInfo, UICodeGenContext context);
    }

    /// <summary>
    /// UI 代码生成上下文 - 包含生成所需的所有信息
    /// </summary>
    public class UICodeGenContext : IBindCodeGenContext
    {
        /// <summary>
        /// 面板名称
        /// </summary>
        public string PanelName { get; set; }

        /// <summary>
        /// 脚本根路径
        /// </summary>
        public string ScriptRootPath { get; set; }

        /// <summary>
        /// 脚本命名空间
        /// </summary>
        public string ScriptNamespace { get; set; }

        /// <summary>
        /// 绑定信息（Panel 级别）
        /// </summary>
        public BindCodeInfo BindCodeInfo { get; set; }

        /// <summary>
        /// 代码生成选项
        /// </summary>
        public PanelCodeGenOptions Options { get; set; }

        /// <summary>
        /// 已生成的类型集合（避免重复生成）
        /// </summary>
        public System.Collections.Generic.HashSet<string> GeneratedTypes { get; } = new(16);

        #region 路径辅助方法

        /// <summary>
        /// 获取 Panel 用户文件路径
        /// </summary>
        public string GetPanelFilePath()
            => $"{ScriptRootPath}/{PanelName}/{PanelName}{UICodeGenConstants.SCRIPT_SUFFIX}";

        /// <summary>
        /// 获取 Panel Designer 文件路径
        /// </summary>
        public string GetPanelDesignerPath()
            => $"{ScriptRootPath}/{PanelName}/{PanelName}{UICodeGenConstants.DESIGNER_SUFFIX}";

        /// <summary>
        /// 检查文件是否存在
        /// </summary>
        public bool FileExists(string path) => File.Exists(path);

        #endregion

        #region 类型生成跟踪

        /// <summary>
        /// 检查类型是否已生成
        /// </summary>
        /// <param name="bindType">绑定类型</param>
        /// <param name="typeName">类型名称</param>
        /// <returns>是否已生成</returns>
        public bool IsTypeGenerated(BindType bindType, string typeName)
        {
            var key = $"{bindType}_{typeName}";
            return GeneratedTypes.Contains(key);
        }

        /// <summary>
        /// 标记类型为已生成
        /// </summary>
        /// <param name="bindType">绑定类型</param>
        /// <param name="typeName">类型名称</param>
        /// <returns>是否为新增（之前未生成）</returns>
        public bool MarkTypeGenerated(BindType bindType, string typeName)
        {
            var key = $"{bindType}_{typeName}";
            return GeneratedTypes.Add(key);
        }

        #endregion

        #region 工厂方法

        /// <summary>
        /// 从配置创建上下文
        /// </summary>
        /// <param name="panelName">面板名称</param>
        /// <param name="bindCodeInfo">绑定信息</param>
        /// <param name="options">代码生成选项</param>
        /// <returns>上下文实例</returns>
        public static UICodeGenContext Create(string panelName, BindCodeInfo bindCodeInfo = null, PanelCodeGenOptions options = null)
        {
            return new UICodeGenContext
            {
                PanelName = panelName,
                ScriptRootPath = UIKitCreateConfig.Instance.ScriptGeneratePath,
                ScriptNamespace = UIKitCreateConfig.Instance.ScriptNamespace,
                BindCodeInfo = bindCodeInfo,
                Options = options
            };
        }

        /// <summary>
        /// 从配置创建上下文（指定命名空间）
        /// </summary>
        /// <param name="panelName">面板名称</param>
        /// <param name="scriptNamespace">命名空间</param>
        /// <param name="bindCodeInfo">绑定信息</param>
        /// <param name="options">代码生成选项</param>
        /// <returns>上下文实例</returns>
        public static UICodeGenContext Create(string panelName, string scriptNamespace, BindCodeInfo bindCodeInfo = null, PanelCodeGenOptions options = null)
        {
            return new UICodeGenContext
            {
                PanelName = panelName,
                ScriptRootPath = UIKitCreateConfig.Instance.ScriptGeneratePath,
                ScriptNamespace = scriptNamespace,
                BindCodeInfo = bindCodeInfo,
                Options = options
            };
        }

        #endregion
    }
}
