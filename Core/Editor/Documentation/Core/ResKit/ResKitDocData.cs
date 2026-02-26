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
            var sections = new List<DocSection>
            {
                // ResKit 基础
                ResKitDocSync.CreateSection(),
                ResKitDocAsync.CreateSection(),
                ResKitDocCustomLoader.CreateSection(),
                ResKitDocRawFile.CreateSection(),
                ResKitDocRawFileInterface.CreateSection(),
                
                // YooAsset 集成
                ResKitDocYooAssetOverview.CreateSection(),
                ResKitDocYooAssetEditor.CreateSection(),
                ResKitDocYooAssetOffline.CreateSection(),
                ResKitDocYooAssetHost.CreateSection(),
                ResKitDocYooAssetUpdate.CreateSection(),
                ResKitDocYooAssetUsage.CreateSection()
            };
            
            // YooInit 完整初始化示例（包含多个子章节）
            sections.AddRange(ResKitDocYooAssetComplete.GetAllSections());
            
            return sections;
        }
    }
}
#endif
