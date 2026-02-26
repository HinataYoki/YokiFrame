#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit YooAsset 集成概述文档
    /// </summary>
    internal static class ResKitDocYooAssetOverview
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "YooAsset 集成概述",
                Description = "YokiFrame 内置 YooAsset 支持，安装 YooAsset 包后自动启用 YOKIFRAME_YOOASSET_SUPPORT 宏。YooAsset 是一个功能强大的资源管理系统，支持资源热更新、分包下载等功能。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "架构说明",
                        Code = @"// YokiFrame 提供的 YooAsset 加载器类型：
// 
// 1. YooAssetResLoader        - 基础加载器，实现 IResLoader 接口
// 2. YooAssetResLoaderPool    - 基础加载池，管理 YooAssetResLoader
// 3. YooAssetResLoaderUniTask - UniTask 加载器，实现 IResLoaderUniTask 接口
// 4. YooAssetResLoaderUniTaskPool - UniTask 加载池（推荐）
//
// 使用流程：
// 1. 初始化 YooAsset 资源包
// 2. 创建对应的加载池
// 3. 调用 ResKit.SetLoaderPool() 切换加载池
// 4. 使用 ResKit API 加载资源（API 不变）",
                        Explanation = "ResKit 通过策略模式支持多种资源加载方式，切换加载池后 API 保持一致。"
                    }
                }
            };
        }
    }
}
#endif
