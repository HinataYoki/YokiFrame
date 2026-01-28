using System;
using System.IO;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// YokiFrame 统一配置
    /// 存储在 Assets/Settings/Resources/YokiFrameSettings.asset
    /// </summary>
    [CreateAssetMenu(fileName = "YokiFrameSettings", menuName = "YokiFrame/Settings")]
    public partial class YokiFrameSettings : ScriptableObject
    {
        private const string RESOURCES_PATH = "YokiFrameSettings";
        private const string ASSET_PATH = "Assets/Settings/Resources/YokiFrameSettings.asset";
        private const string ASSET_DIR = "Assets/Settings/Resources";

        private static YokiFrameSettings sInstance;

        /// <summary>
        /// 获取配置实例
        /// </summary>
        public static YokiFrameSettings Instance
        {
            get
            {
                if (sInstance == default)
                {
                    sInstance = LoadOrCreate();
                }
                return sInstance;
            }
        }

        private static YokiFrameSettings LoadOrCreate()
        {
#if UNITY_EDITOR
            return LoadOrCreateInEditor();
#else
            return LoadInRuntime();
#endif
        }

        private static YokiFrameSettings LoadInRuntime()
        {
            var settings = Resources.Load<YokiFrameSettings>(RESOURCES_PATH);
            if (settings == default)
            {
                Debug.LogWarning("[YokiFrame] 配置文件不存在，使用默认配置");
                settings = CreateInstance<YokiFrameSettings>();
            }
            return settings;
        }
    }
}
