#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// PoolKit 文档模块入口。
    /// </summary>
    internal static class PoolKitDocData
    {
        /// <summary>
        /// 获取 PoolKit 的全部文档章节。
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                PoolKitDocSafe.CreateSection(),
                PoolKitDocSimple.CreateSection(),
                PoolKitDocFactory.CreateSection(),
                PoolKitDocCustom.CreateSection(),
                PoolKitDocContainer.CreateSection(),
                PoolKitDocMonitor.CreateSection()
            };
        }
    }

    internal sealed class PoolKitDocumentationProvider : IDocumentationModuleProvider
    {
        public IEnumerable<DocModule> GetModules()
        {
            yield return new DocModule
            {
                Name = "PoolKit",
                Icon = KitIcons.POOLKIT,
                Category = "CORE KIT",
                Description = "用于对象复用、降低 GC 压力并配合运行时监控的对象池工具集。",
                Keywords = new List<string> { "对象池", "复用", "GC", "性能" },
                Sections = PoolKitDocData.GetAllSections()
            };
        }
    }
}
#endif
