using System;

namespace YokiFrame
{
    public static partial class ResKit
    {
        private static IResSceneBackend sSceneBackend;

        /// <summary>
        /// 当前场景后端名称；未配置时返回 None。
        /// </summary>
        public static string SceneBackendName => sSceneBackend != null ? sSceneBackend.BackendName : "None";

        /// <summary>
        /// 设置 ResKit 使用的场景后端。SceneKit 默认会委托到这里。
        /// </summary>
        public static void SetSceneBackend(IResSceneBackend backend)
        {
            sSceneBackend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        /// <summary>
        /// 清除 ResKit 场景后端。
        /// </summary>
        public static void ClearSceneBackend()
        {
            sSceneBackend = null;
        }

        /// <summary>
        /// 获取当前 ResKit 场景后端。
        /// </summary>
        public static IResSceneBackend GetSceneBackend()
        {
            return sSceneBackend;
        }

        internal static IResSceneBackend EnsureSceneBackend()
        {
            if (sSceneBackend == null)
                throw new InvalidOperationException("ResKit scene backend is not configured. Call ResKit.SetSceneBackend from an engine adapter first.");

            return sSceneBackend;
        }
    }
}
