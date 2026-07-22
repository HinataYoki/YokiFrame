#if UNITY_EDITOR
namespace YokiFrame
{
    /// <summary>为 Default/Minimal 保留当前生成行为的内置恒等模板。</summary>
    internal sealed class UIKitBuiltInCodeTemplate : IUIKitCodeTemplate
    {
        /// <summary>创建指定名称和说明的内置模板。</summary>
        /// <param name="name">稳定模板名。</param>
        /// <param name="description">模板说明。</param>
        internal UIKitBuiltInCodeTemplate(string name, string description)
        {
            Name = name;
            Description = description;
        }

        /// <inheritdoc />
        public string Name { get; }

        /// <inheritdoc />
        public string Description { get; }

        /// <summary>保留内置生成器已经产生的 Default/Minimal 源码。</summary>
        /// <param name="part">当前文件角色。</param>
        /// <param name="context">生成上下文。</param>
        /// <param name="generatedSource">内置源码。</param>
        /// <returns>未修改的源码。</returns>
        public string Transform(
            UIKitCodeTemplatePart part,
            UIKitCodeTemplateContext context,
            string generatedSource)
        {
            return generatedSource;
        }
    }
}
#endif
