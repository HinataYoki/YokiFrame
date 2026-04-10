#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// LogKit 文档模块入口。
    /// </summary>
    internal static class LogKitDocData
    {
        /// <summary>
        /// 获取 LogKit 的全部文档章节。
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                LogKitDocBasic.CreateSection(),
                LogKitDocIMGUI.CreateSection(),
                LogKitDocIMGUIOperation.CreateSection(),
                LogKitDocConfig.CreateSection(),
                LogKitDocEditor.CreateSection(),
                LogKitDocBestPractice.CreateSection()
            };
        }
    }

    internal sealed class LogKitDocumentationProvider : IDocumentationModuleProvider
    {
        public IEnumerable<DocModule> GetModules()
        {
            yield return new DocModule
            {
                Name = "KitLogger",
                Icon = KitIcons.KITLOGGER,
                Category = "CORE KIT",
                Description = "支持文件输出、加密选项以及运行时与编辑器双端使用的日志系统。",
                Keywords = new List<string> { "日志", "调试", "文件", "运行时" },
                Sections = LogKitDocData.GetAllSections()
            };
        }
    }
}
#endif
