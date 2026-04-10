using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// Canvas 优化提示组件 - 用于配置 Canvas 的渲染优化选项
    /// </summary>
    /// <remarks>
    /// 主要用于：
    /// - 配置像素对齐（减少模糊但可能影响性能）
    /// - 配置排序顺序
    /// - 控制 Raycaster 启用状态
    /// </remarks>
    [RequireComponent(typeof(Canvas))]
    [DisallowMultipleComponent]
    [AddComponentMenu("YokiFrame/UI/Canvas Batch Hint")]
    public class CanvasBatchHint : MonoBehaviour
    {
        #region 配置

        [SerializeField]
        [Tooltip("是否启用像素对齐（减少模糊但可能影响性能）")]
        private bool mPixelPerfect;

        [SerializeField]
        [Tooltip("是否覆盖排序层")]
        private bool mOverrideSorting;

        [SerializeField]
        [Tooltip("排序顺序（仅在覆盖排序时生效）")]
        private int mSortingOrder;

        [SerializeField]
        [Tooltip("是否禁用 Raycaster（无交互的 Canvas 可禁用以提升性能）")]
        private bool mDisableRaycaster;

        #endregion

        #region 缓存

        private Canvas mCanvas;
        private GraphicRaycaster mRaycaster;

        #endregion

        #region 属性

        /// <summary>
        /// 关联的 Canvas
        /// </summary>
        public Canvas Canvas
        {
            get
            {
                EnsureInitialized();
                return mCanvas;
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
                mPixelPerfect = value;
                EnsureInitialized();
                if (mCanvas != null)
                {
                    mCanvas.pixelPerfect = value;
                }
            }
        }

        #endregion

        #region 生命周期

        private void Awake()
        {
            mCanvas = GetComponent<Canvas>();
            mRaycaster = GetComponent<GraphicRaycaster>();
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
                mCanvas.sortingOrder = mSortingOrder;
            }

            // Raycaster 控制
            if (mRaycaster != null && mDisableRaycaster)
            {
                mRaycaster.enabled = false;
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置排序顺序
        /// </summary>
        public void SetSortingOrder(int order)
        {
            EnsureInitialized();
            mSortingOrder = order;
            if (mCanvas != null)
            {
                mCanvas.overrideSorting = true;
                mCanvas.sortingOrder = order;
            }
        }

        /// <summary>
        /// 启用/禁用 Raycaster
        /// </summary>
        public void SetRaycasterEnabled(bool enabled)
        {
            EnsureInitialized();
            mDisableRaycaster = !enabled;
            if (mRaycaster != null)
            {
                mRaycaster.enabled = enabled;
            }
        }

        /// <summary>
        /// 确保组件已初始化
        /// </summary>
        private void EnsureInitialized()
        {
            if (mCanvas == null)
            {
                mCanvas = GetComponent<Canvas>();
            }
            if (mRaycaster == null)
            {
                mRaycaster = GetComponent<GraphicRaycaster>();
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
            if (mRaycaster == null)
            {
                mRaycaster = GetComponent<GraphicRaycaster>();
            }

            if (Application.isPlaying)
            {
                ApplySettings();
            }
        }

        private void Reset()
        {
            mPixelPerfect = false;
            mOverrideSorting = false;
            mSortingOrder = 0;
            mDisableRaycaster = false;
        }
#endif
    }
}
