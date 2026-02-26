#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// LogKit (KitLogger) 文档数据 - 按功能模块拆分
    /// </summary>
    internal static class LogKitDocData
    {
        /// <summary>
        /// 获取所有 LogKit 文档模块
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
}
#endif
