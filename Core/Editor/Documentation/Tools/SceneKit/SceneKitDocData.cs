#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SceneKit 文档数据 - 按功能模块拆分
    /// </summary>
    internal static class SceneKitDocData
    {
        /// <summary>
        /// 获取所有 SceneKit 文档模块
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                SceneKitDocBasic.CreateSection(),
                SceneKitDocTransition.CreateSection(),
                SceneKitDocPreload.CreateSection(),
                SceneKitDocUnload.CreateSection(),
                SceneKitDocQuery.CreateSection(),
                SceneKitDocEvent.CreateSection(),
                SceneKitDocUniTask.CreateSection(),
                SceneKitDocLoader.CreateSection(),
                SceneKitDocEditor.CreateSection(),
                SceneKitDocBestPractice.CreateSection()
            };
        }
    }
}
#endif
