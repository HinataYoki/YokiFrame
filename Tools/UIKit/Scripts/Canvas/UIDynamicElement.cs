using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 动态 UI 元素标记 - 标记频繁更新的 UI 元素
    /// 用于 Canvas 动静分离优化
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("YokiFrame/UI/Dynamic Element")]
    public class UIDynamicElement : MonoBehaviour
    {
        #region 配置

        [SerializeField]
        [Tooltip("更新频率提示（用于批处理优化）")]
        private UpdateFrequency mUpdateFrequency = UpdateFrequency.EveryFrame;

        [SerializeField]
        [Tooltip("是否自动移动到动态 Canvas")]
        private bool mAutoMoveToCanvas = true;

        #endregion

        #region 属性

        /// <summary>
        /// 更新频率
        /// </summary>
        public UpdateFrequency Frequency
        {
            get => mUpdateFrequency;
            set => mUpdateFrequency = value;
        }

        /// <summary>
        /// 是否自动移动到动态 Canvas
        /// </summary>
        public bool AutoMoveToCanvas
        {
            get => mAutoMoveToCanvas;
            set => mAutoMoveToCanvas = value;
        }

        /// <summary>
        /// 所属面板
        /// </summary>
        public UIPanel Panel { get; private set; }

        #endregion

        #region 生命周期

        private void Awake()
        {
            Panel = GetComponentInParent<UIPanel>();
        }

        private void OnEnable()
        {
            if (mAutoMoveToCanvas && Panel != null)
            {
                // 通知面板此元素需要放置到动态 Canvas
                Panel.RegisterDynamicElement(this);
            }
        }

        private void OnDisable()
        {
            if (Panel != null)
            {
                Panel.UnregisterDynamicElement(this);
            }
        }

        #endregion
    }

    /// <summary>
    /// 更新频率枚举
    /// </summary>
    public enum UpdateFrequency
    {
        /// <summary>
        /// 每帧更新
        /// </summary>
        EveryFrame,

        /// <summary>
        /// 高频更新（每秒 30+ 次）
        /// </summary>
        High,

        /// <summary>
        /// 中频更新（每秒 10-30 次）
        /// </summary>
        Medium,

        /// <summary>
        /// 低频更新（每秒 1-10 次）
        /// </summary>
        Low,

        /// <summary>
        /// 偶尔更新（每秒少于 1 次）
        /// </summary>
        Occasional
    }
}
