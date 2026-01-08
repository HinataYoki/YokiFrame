using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// Canvas 批处理优化提示组件
    /// 用于标记和优化 Canvas 的批处理行为
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [DisallowMultipleComponent]
    [AddComponentMenu("YokiFrame/UI/Canvas Batch Hint")]
    public class CanvasBatchHint : MonoBehaviour
    {
        #region 配置

        [SerializeField]
        [Tooltip("Canvas 类型")]
        private CanvasType mCanvasType = CanvasType.Mixed;

        [SerializeField]
        [Tooltip("是否启用像素对齐（减少模糊但可能影响性能）")]
        private bool mPixelPerfect = false;

        [SerializeField]
        [Tooltip("排序顺序偏移")]
        private int mSortingOrderOffset;

        [SerializeField]
        [Tooltip("是否覆盖排序层")]
        private bool mOverrideSorting;

        #endregion

        #region 缓存

        private Canvas mCanvas;
        private CanvasScaler mCanvasScaler;
        private GraphicRaycaster mRaycaster;

        #endregion

        #region 属性

        /// <summary>
        /// Canvas 类型
        /// </summary>
        public CanvasType Type
        {
            get => mCanvasType;
            set
            {
                if (mCanvasType != value)
                {
                    mCanvasType = value;
                    ApplySettings();
                }
            }
        }

        /// <summary>
        /// 是否启用像素对齐
        /// </summary>
        public bool PixelPerfect
        {
            get => mPixelPerfect;
            set
            {
                if (mPixelPerfect != value)
                {
                    mPixelPerfect = value;
                    ApplySettings();
                }
            }
        }

        /// <summary>
        /// 关联的 Canvas
        /// </summary>
        public Canvas Canvas => mCanvas;

        #endregion

        #region 生命周期

        private void Awake()
        {
            mCanvas = GetComponent<Canvas>();
            mCanvasScaler = GetComponent<CanvasScaler>();
            mRaycaster = GetComponent<GraphicRaycaster>();
        }

        private void OnEnable()
        {
            ApplySettings();
        }

        #endregion

        #region 设置应用

        /// <summary>
        /// 应用优化设置
        /// </summary>
        private void ApplySettings()
        {
            if (mCanvas == null) return;

            // 像素对齐
            mCanvas.pixelPerfect = mPixelPerfect;

            // 覆盖排序
            if (mOverrideSorting)
            {
                mCanvas.overrideSorting = true;
                mCanvas.sortingOrder += mSortingOrderOffset;
            }

            // 根据类型优化
            switch (mCanvasType)
            {
                case CanvasType.Static:
                    OptimizeForStatic();
                    break;
                case CanvasType.Dynamic:
                    OptimizeForDynamic();
                    break;
                case CanvasType.Mixed:
                    // 默认设置，不做特殊优化
                    break;
            }
        }

        /// <summary>
        /// 静态 Canvas 优化
        /// </summary>
        private void OptimizeForStatic()
        {
            // 静态 Canvas 通常不需要 Raycaster（除非有交互）
            // 这里只是提示，实际是否禁用由用户决定
        }

        /// <summary>
        /// 动态 Canvas 优化
        /// </summary>
        private void OptimizeForDynamic()
        {
            // 动态 Canvas 可能需要更频繁的重建
            // 确保 Canvas 是独立的以避免影响其他元素
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 强制重建 Canvas
        /// </summary>
        public void ForceRebuild()
        {
            if (mCanvas != null)
            {
                Canvas.ForceUpdateCanvases();
            }
        }

        /// <summary>
        /// 设置排序顺序
        /// </summary>
        public void SetSortingOrder(int order)
        {
            if (mCanvas != null)
            {
                mCanvas.sortingOrder = order;
            }
        }

        /// <summary>
        /// 启用/禁用 Raycaster
        /// </summary>
        public void SetRaycasterEnabled(bool enabled)
        {
            if (mRaycaster != null)
            {
                mRaycaster.enabled = enabled;
            }
        }

        #endregion

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (mCanvas == null)
            {
                mCanvas = GetComponent<Canvas>();
            }
            
            if (Application.isPlaying)
            {
                ApplySettings();
            }
        }

        private void Reset()
        {
            mCanvasType = CanvasType.Mixed;
            mPixelPerfect = false;
        }
#endif
    }

    /// <summary>
    /// Canvas 类型枚举
    /// </summary>
    public enum CanvasType
    {
        /// <summary>
        /// 静态 Canvas - 内容很少变化
        /// </summary>
        Static,

        /// <summary>
        /// 动态 Canvas - 内容频繁变化
        /// </summary>
        Dynamic,

        /// <summary>
        /// 混合 Canvas - 包含静态和动态内容
        /// </summary>
        Mixed
    }
}
