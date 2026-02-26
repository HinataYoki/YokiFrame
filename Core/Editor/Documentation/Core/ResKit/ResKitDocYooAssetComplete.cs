#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit YooAsset 完整初始化示例文档 - 主入口
    /// </summary>
    internal static partial class ResKitDocYooAssetComplete
    {
        /// <summary>
        /// 完整初始化示例 - 概述
        /// </summary>
        internal static DocSection CreateOverviewSection()
        {
            return new DocSection
            {
                Title = "完整初始化示例",
                Description = "使用 YooInit 一键初始化 YooAsset 并自动配置 ResKit/UIKit/SceneKit。\n\n" +
                              "核心特性：\n" +
                              "• API 统一命名：有 UniTask 时返回 UniTask，无 UniTask 时返回 IEnumerator\n" +
                              "• 编辑器/真机模式分离：打包时自动切换，无需手动修改\n" +
                              "• 智能包查找：自动定位资源所在包，无需手动指定\n" +
                              "• 多包支持：统一管理多个资源包，第一个为默认包",
                CodeExamples = new List<CodeExample>()
            };
        }

        /// <summary>
        /// 获取所有子章节（兼容旧接口）
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                CreateOverviewSection(),
                CreateBasicSection(),
                CreateConfigSection(),
                CreatePackageSection(),
                CreateEncryptionSection(),
                CreateResourceSection(),
                CreateBootSection(),
                CreateCustomModeSection()
            };
        }

        /// <summary>
        /// 兼容旧接口 - 返回概述章节
        /// </summary>
        internal static DocSection CreateSection() => CreateOverviewSection();
    }
}
#endif
