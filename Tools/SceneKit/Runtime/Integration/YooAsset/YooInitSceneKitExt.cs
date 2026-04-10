#if YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>
    /// YooInit 的 SceneKit 扩展
    /// </summary>
    public static class YooInitSceneKitExt
    {
        private static bool sConfigured;

        /// <summary>
        /// 配置 SceneKit 在场景加载完成后自动释放未使用资源
        /// 在 YooInit.InitAsync() 之后调用
        /// </summary>
        public static void ConfigureSceneKit()
        {
            if (!YooInit.Initialized)
            {
                KitLogger.Warning("[YooInit] 请先调用 InitAsync() 初始化 YooAsset");
                return;
            }

            if (sConfigured)
            {
                KitLogger.Warning("[YooInit] SceneKit 已配置，无需重复调用");
                return;
            }

            // 注册场景加载完成事件，自动释放无用资源
            EventKit.Type.Register<SceneLoadCompleteEvent>(static _ => 
                YooInit.UnloadUnusedAssetsAsync().Forget());

            sConfigured = true;
            KitLogger.Log("[YooInit] SceneKit 自动资源释放已配置");
        }
    }
}
#endif
