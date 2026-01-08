using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 静态 UI 元素标记 - 标记不频繁更新的 UI 元素
    /// 用于 Canvas 动静分离优化
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("YokiFrame/UI/Static Element")]
    public class UIStaticElement : MonoBehaviour
    {
        #region 配置

        [SerializeField]
        [Tooltip("是否自动移动到静态 Canvas")]
        private bool mAutoMoveToCanvas = true;

        #endregion

        #region 属性

        /// <summary>
        /// 是否自动移动到静态 Canvas
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
                // 通知面板此元素需要放置到静态 Canvas
                Panel.RegisterStaticElement(this);
            }
        }

        private void OnDisable()
        {
            if (Panel != null)
            {
                Panel.UnregisterStaticElement(this);
            }
        }

        #endregion
    }
}
