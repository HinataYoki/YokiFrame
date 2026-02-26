using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// UIRoot - 初始化
    /// </summary>
    public partial class UIRoot
    {
        #region 初始化

        private void Initialize()
        {
            InitializeComponents();
            InitializeUILevels();
            InitializeInputModule();
            InitializeLevelPanels();
            InitializeFocusSystem();
        }

        private void InitializeComponents()
        {
            var root = transform.root;
            if (Canvas == default) Canvas = GetComponent<Canvas>();
            if (CanvasScaler == default) CanvasScaler = GetComponent<CanvasScaler>();
            if (GraphicRaycaster == default) GraphicRaycaster = GetComponent<GraphicRaycaster>();
            if (EventSystem == default) EventSystem = root.GetComponentInChildren<EventSystem>();

            // 应用配置到组件
            ApplyCanvasConfig();
            ApplyCanvasScalerConfig();
            ApplyGraphicRaycasterConfig();
        }

        /// <summary>
        /// 应用 Canvas 配置
        /// </summary>
        private void ApplyCanvasConfig()
        {
            if (Canvas == default) return;

            Canvas.renderMode = Config.RenderMode;
            Canvas.sortingOrder = Config.SortOrder;
            Canvas.targetDisplay = Config.TargetDisplay;
            Canvas.pixelPerfect = Config.PixelPerfect;
        }

        /// <summary>
        /// 应用 CanvasScaler 配置
        /// </summary>
        private void ApplyCanvasScalerConfig()
        {
            if (CanvasScaler == default) return;

            CanvasScaler.uiScaleMode = Config.ScaleMode;
            CanvasScaler.referenceResolution = Config.ReferenceResolution;
            CanvasScaler.screenMatchMode = Config.ScreenMatchMode;
            CanvasScaler.matchWidthOrHeight = Config.MatchWidthOrHeight;
            CanvasScaler.referencePixelsPerUnit = Config.ReferencePixelsPerUnit;
            CanvasScaler.physicalUnit = Config.PhysicalUnit;
            CanvasScaler.fallbackScreenDPI = Config.FallbackScreenDPI;
            CanvasScaler.defaultSpriteDPI = Config.DefaultSpriteDPI;
            CanvasScaler.dynamicPixelsPerUnit = Config.DynamicPixelsPerUnit;
        }

        /// <summary>
        /// 应用 GraphicRaycaster 配置
        /// </summary>
        private void ApplyGraphicRaycasterConfig()
        {
            if (GraphicRaycaster == default) return;

            GraphicRaycaster.ignoreReversedGraphics = Config.IgnoreReversedGraphics;
            GraphicRaycaster.blockingObjects = Config.BlockingObjects;
            GraphicRaycaster.blockingMask = Config.BlockingMask;
        }

        private void InitializeUILevels()
        {
            UILevelDic.Clear();
            foreach (UILevel level in Enum.GetValues(typeof(UILevel)))
            {
                var rect = GetOrCreateLevelNode(level.ToString());
                UILevelDic.Add(level, rect);
                SetupRectTransform(rect);
            }
        }

        private RectTransform GetOrCreateLevelNode(string name)
        {
            var child = transform.Find(name);
            if (child != default) return child as RectTransform;

            var obj = new GameObject(name, typeof(RectTransform));
            var rect = obj.transform as RectTransform;
            rect.SetParent(transform);
            return rect;
        }

        private void InitializeInputModule()
        {
            if (EventSystem == default) return;
            if (EventSystem.GetComponent<BaseInputModule>() != default) return;

#if YOKIFRAME_INPUTSYSTEM_SUPPORT && !ENABLE_LEGACY_INPUT_MANAGER
            EventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            EventSystem.gameObject.AddComponent<StandaloneInputModule>();
#endif
        }

        #endregion
    }
}
