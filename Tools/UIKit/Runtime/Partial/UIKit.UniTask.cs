#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>
    /// UIKit UniTask 异步扩展
    /// </summary>
    public partial class UIKit
    {
        #region UniTask 异步方法

        /// <summary>
        /// [UniTask] 异步打开指定类型的Panel
        /// </summary>
        public static async UniTask<T> OpenPanelUniTaskAsync<T>(UILevel level = UILevel.Common, IUIData data = null, CancellationToken cancellationToken = default) where T : UIPanel
        {
            WeakenHot();
            var type = typeof(T);
            
            // 1. 检查主缓存
            if (TryGetHandler(type, out var handler))
            {
                handler.Data = data;
                OpenAndShowPanel(handler.Panel, data);
                return handler.Panel as T;
            }
            
            // 2. 检查预加载缓存
            if (UICacheManager.TryGetPreloaded(type, out handler))
            {
                handler.Data = data;
                // 需要访问 PanelCacheDic，通过内部方法
                AddToMainCache(type, handler);
                handler.Hot += OpenHot;
                OpenAndShowPanel(handler.Panel, data);
                return handler.Panel as T;
            }
            
            // 3. 创建新 Handler
            handler = PanelHandler.Allocate();
            handler.Type = type;
            handler.Level = level;
            handler.Data = data;
            
            // 异步创建 UI
            var panel = await CreateUIUniTaskAsync(handler, cancellationToken);
            
            if (panel != null && panel.Transform != null)
            {
                return panel as T;
            }
            
            KitLogger.Error($"[UIKit] OpenPanelUniTaskAsync: {type.Name} 创建失败");
            return null;
        }

        /// <summary>
        /// [UniTask] 异步打开并压入Panel到栈中
        /// </summary>
        public static async UniTask<T> PushOpenPanelUniTaskAsync<T>(UILevel level = UILevel.Common, IUIData data = null, bool hidePreLevel = true, CancellationToken cancellationToken = default) where T : UIPanel
        {
            var panel = await OpenPanelUniTaskAsync<T>(level, data, cancellationToken);
            if (panel != null)
            {
                PushPanel(panel, hidePreLevel);
            }
            return panel;
        }

        /// <summary>
        /// [UniTask] 异步弹出面板（等待动画完成）
        /// </summary>
        public static UniTask<IPanel> PopPanelUniTaskAsync(bool showPreLevel = true, bool autoClose = true, CancellationToken cancellationToken = default)
        {
            return UIStackManager.PopUniTaskAsync(UIStackManager.DEFAULT_STACK, showPreLevel, autoClose, cancellationToken);
        }

        /// <summary>
        /// [UniTask] 从指定命名栈异步弹出面板（等待动画完成）
        /// </summary>
        public static UniTask<IPanel> PopPanelUniTaskAsync(string stackName, bool showPreLevel = true, bool autoClose = true, CancellationToken cancellationToken = default)
        {
            return UIStackManager.PopUniTaskAsync(stackName, showPreLevel, autoClose, cancellationToken);
        }

        /// <summary>
        /// [UniTask] 异步创建 UI
        /// </summary>
        private static async UniTask<IPanel> CreateUIUniTaskAsync(PanelHandler handler, CancellationToken cancellationToken)
        {
            if (handler == null) return null;
            
            var panel = await UIRoot.Instance.LoadPanelUniTaskAsync(handler, cancellationToken);
            
            if (panel != null && panel.Transform != null)
            {
                SetupPanel(handler, panel);
                OpenAndShowPanel(panel, handler.Data);
                return panel;
            }
            
            handler.Recycle();
            return null;
        }

        /// <summary>
        /// 添加到主缓存（内部方法，供 UniTask 扩展使用）
        /// </summary>
        private static void AddToMainCache(System.Type type, PanelHandler handler)
        {
            // partial class 可以直接访问私有成员 PanelCacheDic
            PanelCacheDic.TryAdd(type, handler);
        }

        #endregion
    }
}
#endif
