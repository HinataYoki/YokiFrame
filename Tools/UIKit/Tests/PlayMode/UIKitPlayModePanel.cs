#if UNITY_2022_3_OR_NEWER
using System;
using System.Threading;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 记录真实 Unity 与 UIKit 生命周期的 Play Mode 测试 Panel。
    /// </summary>
    public sealed class UIKitPlayModePanel : UIPanel
    {
        internal static int AwakeCount { get; private set; }
        internal static int InitCount { get; private set; }
        internal static int OpenCount { get; private set; }
        internal static int ShowCount { get; private set; }
        internal static int CloseCount { get; private set; }
        internal static int BeforeDestroyCount { get; private set; }
        internal static int DestroyCount { get; private set; }

        /// <summary>测试在 UIKit 提交销毁前执行一次的可控回调。</summary>
        internal static Action BeforeDestroyAction { get; set; }
        internal int CreationThreadId { get; private set; }

        /// <summary>
        /// 记录 Unity 创建线程与 Awake 次数。
        /// </summary>
        private void Awake()
        {
            AwakeCount++;
            CreationThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>
        /// 记录 UIKit 一次初始化。
        /// </summary>
        protected override void OnInit(IUIData data = null)
        {
            InitCount++;
        }

        /// <summary>
        /// 记录 UIKit 一次打开请求。
        /// </summary>
        protected override void OnOpen(IUIData data = null)
        {
            OpenCount++;
        }

        /// <summary>
        /// 记录 UIKit 一次真实显示转换。
        /// </summary>
        protected override void OnShow()
        {
            ShowCount++;
        }

        /// <summary>
        /// 记录 UIKit 一次关闭轮次。
        /// </summary>
        protected override void OnClose()
        {
            CloseCount++;
        }

        /// <summary>
        /// 记录 UIKit owner 在 Unity 销毁前提交的一次幂等通知。
        /// </summary>
        protected override void OnBeforeDestroy()
        {
            BeforeDestroyCount++;
            Action action = BeforeDestroyAction;
            BeforeDestroyAction = null;
            if (action != null) action();
        }

        /// <summary>
        /// 记录 Unity 销毁回调。
        /// </summary>
        private void OnDestroy()
        {
            DestroyCount++;
        }

        /// <summary>
        /// 清空测试间共享计数。
        /// </summary>
        internal static void ResetCounters()
        {
            AwakeCount = 0;
            InitCount = 0;
            OpenCount = 0;
            ShowCount = 0;
            CloseCount = 0;
            BeforeDestroyCount = 0;
            DestroyCount = 0;
            BeforeDestroyAction = null;
        }
    }
}
#endif
