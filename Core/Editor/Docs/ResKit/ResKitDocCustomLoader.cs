#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 自定义加载器文档
    /// </summary>
    internal static class ResKitDocCustomLoader
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "自定义加载器",
                Description = "通过实现 IResLoaderPool 接口扩展加载方式，支持 YooAsset、Addressables 等。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "设置自定义加载池",
                        Code = @"// 切换到自定义加载池
ResKit.SetLoaderPool(new CustomLoaderPool());

// 获取当前加载池
var pool = ResKit.GetLoaderPool();

// 清理所有缓存
ResKit.ClearAll();"
                    }
                }
            };
        }
    }
}
#endif
