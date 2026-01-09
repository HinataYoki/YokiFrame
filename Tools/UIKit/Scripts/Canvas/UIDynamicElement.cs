using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// 动态 UI 元素标记 - 自动创建嵌套 Canvas 实现动静分离
    /// 添加此组件后，该元素及其子元素的更新不会触发父 Canvas 重建
    /// </summary>
    /// <remarks>
    /// 适用场景：
    /// - 血条、蓝条等实时更新的数值显示
    /// - 计时器、倒计时文本
    /// - 动画进度条
    /// - 频繁变化的列表项内容
    /// 
    /// 不需要使用的场景：
    /// - 静态背景、边框（不会触发 rebuild）
    /// - 按钮点击（不会触发 rebuild）
    /// - 偶尔更新的文本
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("YokiFrame/UI/Dynamic Element")]
    public class UIDynamicElement : MonoBehaviour
    {
        #region 配置

        [SerializeField]
        [Tooltip("是否需要接收射线检测（有交互元素时启用）")]
        private bool mEnableRaycast = true;

        [SerializeField]
        [Tooltip("是否在 Awake 时自动初始化（禁用后需手动调用 Initialize）")]
        private bool mAutoInitialize = true;

        #endregion

        #region 缓存

        private Canvas mCanvas;
        private GraphicRaycaster mRaycaster;
        private bool mIsInitialized;

        #endregion

        #region 属性

        /// <summary>
        /// 嵌套 Canvas 引用
        /// </summary>
        public Canvas Canvas => mCanvas;

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized => mIsInitialized;

        /// <summary>
        /// 是否启用射线检测
        /// </summary>
        public bool EnableRaycast
        {
            get => mEnableRaycast;
            set
            {
                mEnableRaycast = value;
                if (mRaycaster != null)
                {
                    mRaycaster.enabled = value;
                }
            }
        }

        #endregion

        #region 生命周期

        private void Awake()
        {
            if (mAutoInitialize)
            {
                Initialize();
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 初始化嵌套 Canvas
        /// </summary>
        public void Initialize()
        {
            if (mIsInitialized) return;

            SetupNestedCanvas();
            SetupRaycaster();
            mIsInitialized = true;
        }

        /// <summary>
        /// 强制刷新 Canvas
        /// </summary>
        public void ForceRebuild()
        {
            if (mCanvas != null)
            {
                Canvas.ForceUpdateCanvases();
            }
        }

        #endregion

        #region 内部方法

        /// <summary>
        /// 设置嵌套 Canvas
        /// </summary>
        private void SetupNestedCanvas()
        {
            // 检查是否已有 Canvas
            mCanvas = GetComponent<Canvas>();
            if (mCanvas == null)
            {
                mCanvas = gameObject.AddComponent<Canvas>();
            }

            // 嵌套 Canvas 配置：
            // - overrideSorting = false：继承父 Canvas 的排序设置
            // - 不需要设置 renderMode，嵌套 Canvas 自动继承
            mCanvas.overrideSorting = false;
        }

        /// <summary>
        /// 设置射线检测器
        /// </summary>
        private void SetupRaycaster()
        {
            if (!mEnableRaycast) return;

            // 检查子元素是否有可交互组件
            bool hasInteractable = GetComponentInChildren<Selectable>(true) != null;
            if (!hasInteractable) return;

            mRaycaster = GetComponent<GraphicRaycaster>();
            if (mRaycaster == null)
            {
                mRaycaster = gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        #endregion

#if UNITY_EDITOR
        private void Reset()
        {
            // 默认配置
            mEnableRaycast = true;
            mAutoInitialize = true;
        }

        private void OnValidate()
        {
            // 编辑器中实时更新 Raycaster 状态
            if (mRaycaster != null)
            {
                mRaycaster.enabled = mEnableRaycast;
            }
        }
#endif
    }
}
