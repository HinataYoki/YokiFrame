#if UNITY_EDITOR
namespace YokiFrame
{
    /// <summary>
    /// UIKit 面板绑定代码生成上下文。
    /// </summary>
    internal sealed class UIKitPanelCodeGenContext : IBindCodeGenContext
    {
        /// <summary>
        /// 创建面板代码生成上下文。
        /// </summary>
        public UIKitPanelCodeGenContext(string panelName, string scriptRootPath, string scriptNamespace)
        {
            PanelName = panelName;
            ScriptRootPath = scriptRootPath;
            ScriptNamespace = scriptNamespace;
        }

        /// <summary>面板类型名。</summary>
        public string PanelName { get; private set; }

        /// <summary>脚本输出根目录。</summary>
        public string ScriptRootPath { get; private set; }

        /// <summary>脚本命名空间。</summary>
        public string ScriptNamespace { get; private set; }
    }
}
#endif
