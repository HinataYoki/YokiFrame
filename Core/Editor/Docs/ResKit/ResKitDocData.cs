#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 文档数据 - 按功能模块拆分
    /// </summary>
    internal static class ResKitDocData
    {
        /// <summary>
        /// 获取所有 ResKit 文档模块
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                ResKitDocSync.CreateSection(),
                ResKitDocAsync.CreateSection(),
                ResKitDocCustomLoader.CreateSection(),
                ResKitDocRawFile.CreateSection(),
                ResKitDocRawFileInterface.CreateSection(),
                ResKitDocYooAssetOverview.CreateSection(),
                ResKitDocYooAssetEditor.CreateSection(),
                ResKitDocYooAssetOffline.CreateSection(),
                ResKitDocYooAssetHost.CreateSection(),
                ResKitDocYooAssetUpdate.CreateSection(),
                ResKitDocYooAssetUsage.CreateSection(),
                ResKitDocYooAssetComplete.CreateSection()
            };
        }
    }
}
#endif
