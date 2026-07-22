#if UNITY_2022_3_OR_NEWER
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>为频繁变化的 UI 子树创建嵌套 Canvas，隔离父 Canvas rebuild。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("YokiFrame/UIKit/Dynamic Element")]
    public sealed class UIDynamicElement : MonoBehaviour
    {
        [SerializeField] private bool mEnableRaycast = true;
        [SerializeField] private bool mAutoInitialize = true;
        private Canvas mCanvas;
        private GraphicRaycaster mRaycaster;
        private bool mIsInitialized;

        /// <summary>获取动态元素使用的嵌套 Canvas。</summary>
        public Canvas Canvas => mCanvas;

        /// <summary>判断嵌套 Canvas 是否已初始化。</summary>
        public bool IsInitialized => mIsInitialized;

        /// <summary>获取或设置子树的 Raycaster 开关。</summary>
        public bool EnableRaycast
        {
            get { return mEnableRaycast; }
            set
            {
                mEnableRaycast = value;
                if (mRaycaster != null) mRaycaster.enabled = value;
            }
        }

        /// <summary>按配置自动初始化动态元素。</summary>
        private void Awake()
        {
            if (mAutoInitialize) Initialize();
        }

        /// <summary>创建或复用嵌套 Canvas 与必要的 Raycaster。</summary>
        public void Initialize()
        {
            if (mIsInitialized) return;
            mCanvas = GetComponent<Canvas>();
            if (mCanvas == null) mCanvas = gameObject.AddComponent<Canvas>();
            mCanvas.overrideSorting = false;
            bool hasInteractable = GetComponentInChildren<Selectable>(true) != null;
            if (mEnableRaycast && hasInteractable)
            {
                mRaycaster = GetComponent<GraphicRaycaster>();
                if (mRaycaster == null) mRaycaster = gameObject.AddComponent<GraphicRaycaster>();
                mRaycaster.enabled = true;
            }
            mIsInitialized = true;
        }

        /// <summary>请求 Unity 立即重建当前 Canvas。</summary>
        public void ForceRebuild()
        {
            if (mCanvas != null) Canvas.ForceUpdateCanvases();
        }

#if UNITY_EDITOR
        /// <summary>在 Inspector 重置时恢复稳定默认值。</summary>
        private void Reset()
        {
            mEnableRaycast = true;
            mAutoInitialize = true;
        }

        /// <summary>编辑器修改 Raycast 配置时同步现有组件。</summary>
        private void OnValidate()
        {
            if (mRaycaster != null) mRaycaster.enabled = mEnableRaycast;
        }
#endif
    }
}
#endif
