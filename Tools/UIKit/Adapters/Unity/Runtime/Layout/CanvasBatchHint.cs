#if UNITY_2022_3_OR_NEWER
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>集中保存 Canvas 像素对齐、排序和 Raycaster 优化提示。</summary>
    [RequireComponent(typeof(Canvas))]
    [DisallowMultipleComponent]
    [AddComponentMenu("YokiFrame/UIKit/Canvas Batch Hint")]
    public sealed class CanvasBatchHint : MonoBehaviour
    {
        [SerializeField] private bool mPixelPerfect;
        [SerializeField] private bool mOverrideSorting;
        [SerializeField] private int mSortingOrder;
        [SerializeField] private bool mDisableRaycaster;
        private Canvas mCanvas;
        private GraphicRaycaster mRaycaster;

        /// <summary>获取绑定的 Canvas。</summary>
        public Canvas Canvas
        {
            get
            {
                EnsureInitialized();
                return mCanvas;
            }
        }

        /// <summary>获取或设置像素对齐。</summary>
        public bool PixelPerfect
        {
            get { return mPixelPerfect; }
            set
            {
                mPixelPerfect = value;
                EnsureInitialized();
                if (mCanvas != null) mCanvas.pixelPerfect = value;
            }
        }

        /// <summary>启动时读取组件引用并应用序列化设置。</summary>
        private void Awake()
        {
            EnsureInitialized();
            ApplySettings();
        }

        /// <summary>设置排序覆盖和 Raycaster 开关。</summary>
        public void ApplySettings()
        {
            EnsureInitialized();
            if (mCanvas == null) return;
            mCanvas.pixelPerfect = mPixelPerfect;
            mCanvas.overrideSorting = mOverrideSorting;
            if (mOverrideSorting) mCanvas.sortingOrder = mSortingOrder;
            if (mRaycaster != null) mRaycaster.enabled = !mDisableRaycaster;
        }

        /// <summary>启用 Canvas 排序覆盖并更新排序值。</summary>
        public void SetSortingOrder(int order)
        {
            EnsureInitialized();
            mSortingOrder = order;
            mOverrideSorting = true;
            if (mCanvas != null)
            {
                mCanvas.overrideSorting = true;
                mCanvas.sortingOrder = order;
            }
        }

        /// <summary>设置 GraphicRaycaster 是否参与事件处理。</summary>
        public void SetRaycasterEnabled(bool enabled)
        {
            EnsureInitialized();
            mDisableRaycaster = !enabled;
            if (mRaycaster != null) mRaycaster.enabled = enabled;
        }

        /// <summary>延迟获取同一 GameObject 上的 Canvas 与 Raycaster。</summary>
        private void EnsureInitialized()
        {
            if (mCanvas == null) mCanvas = GetComponent<Canvas>();
            if (mRaycaster == null) mRaycaster = GetComponent<GraphicRaycaster>();
        }

#if UNITY_EDITOR
        /// <summary>编辑器修改序列化配置时，在运行态应用新值。</summary>
        private void OnValidate()
        {
            EnsureInitialized();
            if (Application.isPlaying) ApplySettings();
        }

        /// <summary>在 Inspector 重置时恢复稳定默认值。</summary>
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
#endif
