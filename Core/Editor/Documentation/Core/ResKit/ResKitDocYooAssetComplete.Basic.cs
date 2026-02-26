#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit YooAsset 完整初始化示例文档 - 基础用法
    /// </summary>
    internal static partial class ResKitDocYooAssetComplete
    {
        /// <summary>
        /// YooInit 基础用法
        /// </summary>
        internal static DocSection CreateBasicSection()
        {
            return new DocSection
            {
                Title = "  YooInit 基础用法",
                Description = "API 名称统一为 InitAsync，根据是否有 UniTask 自动切换返回类型。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基础初始化",
                        Code = @"#if YOKIFRAME_YOOASSET_SUPPORT
using UnityEngine;
using YokiFrame;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Collections;
#endif

public class GameLauncher : MonoBehaviour
{
    [SerializeField] private YooInitConfig mConfig;
    
#if YOKIFRAME_UNITASK_SUPPORT
    // ========== UniTask 版本 ==========
    private async void Start()
    {
        // 初始化 YooAsset（返回 UniTask）
        await YooInit.InitAsync(mConfig);
        
        // 配置 UIKit 使用 YooAsset 加载面板
        YooInitUIKitExt.ConfigureUIKit();
        
        // 配置 SceneKit 场景切换时自动释放资源
        YooInitSceneKitExt.ConfigureSceneKit();
        
        Debug.Log(""YooAsset 初始化完成"");
    }
#else
    // ========== 协程版本 ==========
    private IEnumerator Start()
    {
        // 初始化 YooAsset（返回 IEnumerator）
        yield return YooInit.InitAsync(mConfig, () =>
        {
            // 配置 UIKit/SceneKit
            YooInitUIKitExt.ConfigureUIKit();
            YooInitSceneKitExt.ConfigureSceneKit();
            
            Debug.Log(""YooAsset 初始化完成"");
        });
    }
#endif
}
#endif",
                        Explanation = "InitAsync 方法名统一，编译时根据 YOKIFRAME_UNITASK_SUPPORT 宏自动选择实现。"
                    }
                }
            };
        }

        /// <summary>
        /// YooInit 完整启动流程
        /// </summary>
        internal static DocSection CreateBootSection()
        {
            return new DocSection
            {
                Title = "  YooInit 完整启动流程",
                Description = "推荐的启动流程示例。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "完整启动流程示例",
                        Code = @"#if YOKIFRAME_YOOASSET_SUPPORT
using UnityEngine;
using YokiFrame;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif

public class Boot : MonoBehaviour
{
    [SerializeField] private YooInitConfig mYooConfig;
    
#if YOKIFRAME_UNITASK_SUPPORT
    private async void Start()
    {
        // 1. 初始化 YooAsset
        await YooInit.InitAsync(mYooConfig);
        
        // 2. 配置 UIKit
        YooInitUIKitExt.ConfigureUIKit();
        
        // 3. 配置 SceneKit
        YooInitSceneKitExt.ConfigureSceneKit();
        
        // 4. 初始化其他系统（如 FMOD）
        await InitFMOD();
        
        // 5. 加载主场景
        await SceneSystem.AsyncToNextSceneAwait(1001);
    }
    
    private async UniTask InitFMOD()
    {
        // 使用智能查找加载 FMOD Bank
        var handle = await YooInit.LoadRawAsync(""Assets/Audio/Master.bank"");
        FMODUnity.RuntimeManager.LoadBank(handle.GetRawFileData());
        handle.Release();
    }
#endif
}
#endif",
                        Explanation = "推荐的启动流程：YooInit → UIKit → SceneKit → 其他系统 → 主场景。"
                    }
                }
            };
        }
    }
}
#endif
