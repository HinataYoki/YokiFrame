#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// InputKit 文档数据入口
    /// </summary>
    internal static class InputKitDocData
    {
        /// <summary>
        /// 获取所有 InputKit 文档章节
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                InputKitDocQuickStart.CreateSection(),
                InputKitDocInit.CreateSection(),
                InputKitDocInput.CreateSection(),
                InputKitDocRebind.CreateSection(),
                InputKitDocBuffer.CreateSection(),
                InputKitDocCombo.CreateSection(),
                InputKitDocContext.CreateSection(),
                InputKitDocTouch.CreateSection(),
                InputKitDocHaptic.CreateSection(),
                InputKitDocDebug.CreateSection()
            };
        }
    }
}
#endif
