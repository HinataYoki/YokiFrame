#if UNITY_2022_3_OR_NEWER
using System.Text;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>在 Player 中显示 UIKit 面板、栈与焦点摘要的轻量调试覆盖层。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("YokiFrame/UIKit/Debug Overlay")]
    public sealed class UIDebugOverlay : MonoBehaviour
    {
        private const float UPDATE_INTERVAL = 0.2f;
        private static UIDebugOverlay sInstance;
        [SerializeField] private bool mVisible;
        [SerializeField] private Vector2 mPosition = new(12f, 12f);
        [SerializeField] private Vector2 mSize = new(300f, 180f);
        private float mNextUpdateTime;
        private string mCachedText = "UIKit: no root";

        /// <summary>获取或创建跨场景调试覆盖层。</summary>
        public static UIDebugOverlay Instance
        {
            get
            {
                if (sInstance != null) return sInstance;
                GameObject host = new("[YokiFrame UIKit Debug]");
                DontDestroyOnLoad(host);
                sInstance = host.AddComponent<UIDebugOverlay>();
                return sInstance;
            }
        }

        /// <summary>获取或设置覆盖层是否可见。</summary>
        public bool IsVisible
        {
            get { return mVisible; }
            set { mVisible = value; }
        }

        /// <summary>显示运行时覆盖层。</summary>
        public static void Show()
        {
            Instance.mVisible = true;
        }

        /// <summary>隐藏已经存在的覆盖层，不创建新对象。</summary>
        public static void Hide()
        {
            if (sInstance != null) sInstance.mVisible = false;
        }

        /// <summary>切换覆盖层可见性。</summary>
        public static void Toggle()
        {
            UIDebugOverlay overlay = Instance;
            overlay.mVisible = !overlay.mVisible;
        }

        /// <summary>登记唯一实例并保持跨场景生命周期。</summary>
        private void Awake()
        {
            if (sInstance != null && sInstance != this)
            {
                Destroy(gameObject);
                return;
            }
            sInstance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>销毁时清除静态实例引用。</summary>
        private void OnDestroy()
        {
            if (sInstance == this) sInstance = null;
        }

        /// <summary>以有限频率刷新诊断文本，避免每帧构造字符串。</summary>
        private void Update()
        {
            if (!mVisible || Time.unscaledTime < mNextUpdateTime) return;
            mNextUpdateTime = Time.unscaledTime + UPDATE_INTERVAL;
            mCachedText = BuildText(UIKit.CaptureRuntimeDiagnostics());
        }

        /// <summary>使用 IMGUI 绘制只读诊断文本。</summary>
        private void OnGUI()
        {
            if (!mVisible) return;
            GUI.Box(new Rect(mPosition, mSize), mCachedText);
        }

        /// <summary>把快照格式化为覆盖层文本。</summary>
        private static string BuildText(UIKitRuntimeSnapshot snapshot)
        {
            if (!snapshot.HasRoot) return "UIKit\nRoot: offline";
            var builder = new StringBuilder(256);
            builder.AppendLine("UIKit");
            builder.Append("Panels: ").Append(snapshot.VisiblePanelCount).Append('/').AppendLine(snapshot.LoadedPanelCount.ToString());
            builder.Append("Stacks: ").AppendLine(snapshot.StackCount.ToString());
            builder.Append("Focus: ").AppendLine(snapshot.FocusName ?? "none");
            builder.Append("Input: ").Append(snapshot.InputMode);
            return builder.ToString();
        }
    }
}
#endif
