using System;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// UIKit Canvas 配置
    /// 存储在 Assets/Settings/Resources/UIKitSettings.asset
    /// </summary>
    [CreateAssetMenu(fileName = "UIKitSettings", menuName = "YokiFrame/UIKit Settings")]
    public class UIKitSettings : ScriptableObject
    {
        private const string RESOURCES_PATH = "UIKitSettings";
        private const string ASSET_PATH = "Assets/Settings/Resources/UIKitSettings.asset";
        private const string ASSET_DIR = "Assets/Settings/Resources";

        private static UIKitSettings sInstance;

        /// <summary>
        /// 获取配置实例
        /// </summary>
        public static UIKitSettings Instance
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

        [Header("Canvas 配置")]
        [Tooltip("Canvas 渲染模式")]
        public RenderMode RenderMode = RenderMode.ScreenSpaceOverlay;

        [Tooltip("排序顺序")]
        public int SortOrder;

        [Tooltip("目标显示器")]
        public int TargetDisplay;

        [Tooltip("像素完美")]
        public bool PixelPerfect;

        [Header("CanvasScaler 配置")]
        [Tooltip("UI 缩放模式")]
        public CanvasScaler.ScaleMode ScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        [Tooltip("参考分辨率")]
        public Vector2 ReferenceResolution = new(3840, 2160);

        [Tooltip("屏幕匹配模式")]
        public CanvasScaler.ScreenMatchMode ScreenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        [Tooltip("宽高匹配权重 (0=宽度, 1=高度)")]
        [Range(0f, 1f)]
        public float MatchWidthOrHeight;

        [Tooltip("参考像素每单位")]
        public float ReferencePixelsPerUnit = 100f;

        [Tooltip("物理单位")]
        public CanvasScaler.Unit PhysicalUnit = CanvasScaler.Unit.Points;

        [Tooltip("回退屏幕 DPI")]
        public float FallbackScreenDPI = 96f;

        [Tooltip("默认精灵 DPI")]
        public float DefaultSpriteDPI = 96f;

        [Tooltip("动态像素每单位")]
        public float DynamicPixelsPerUnit = 1f;

        [Header("GraphicRaycaster 配置")]
        [Tooltip("忽略反向图形")]
        public bool IgnoreReversedGraphics;

        [Tooltip("阻挡对象类型")]
        public GraphicRaycaster.BlockingObjects BlockingObjects = GraphicRaycaster.BlockingObjects.None;

        [Tooltip("阻挡层级")]
        public LayerMask BlockingMask = -1;

        /// <summary>
        /// 重置为默认值
        /// </summary>
        public void ResetToDefault()
        {
            RenderMode = RenderMode.ScreenSpaceOverlay;
            SortOrder = 0;
            TargetDisplay = 0;
            PixelPerfect = false;

            ScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            ReferenceResolution = new(3840, 2160);
            ScreenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            MatchWidthOrHeight = 0f;
            ReferencePixelsPerUnit = 100f;
            PhysicalUnit = CanvasScaler.Unit.Points;
            FallbackScreenDPI = 96f;
            DefaultSpriteDPI = 96f;
            DynamicPixelsPerUnit = 1f;

            IgnoreReversedGraphics = false;
            BlockingObjects = GraphicRaycaster.BlockingObjects.None;
            BlockingMask = -1;
        }

        private static UIKitSettings LoadOrCreate()
        {
#if UNITY_EDITOR
            return LoadOrCreateInEditor();
#else
            return LoadInRuntime();
#endif
        }

        private static UIKitSettings LoadInRuntime()
        {
            var settings = Resources.Load<UIKitSettings>(RESOURCES_PATH);
            if (settings == default)
            {
                Debug.LogWarning("[UIKit] 配置文件不存在，使用默认配置");
                settings = CreateInstance<UIKitSettings>();
            }
            return settings;
        }

#if UNITY_EDITOR
        private static UIKitSettings LoadOrCreateInEditor()
        {
            var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<UIKitSettings>(ASSET_PATH);
            if (settings == default)
            {
                settings = CreateInstance<UIKitSettings>();

                // 确保目录存在
                if (!System.IO.Directory.Exists(ASSET_DIR))
                {
                    System.IO.Directory.CreateDirectory(ASSET_DIR);
                    UnityEditor.AssetDatabase.Refresh();
                }

                UnityEditor.AssetDatabase.CreateAsset(settings, ASSET_PATH);
                UnityEditor.AssetDatabase.SaveAssets();
                Debug.Log($"[UIKit] 配置文件已创建: {ASSET_PATH}");
            }
            return settings;
        }
#endif
    }
}
