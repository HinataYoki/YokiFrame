#if !GODOT
using System;
using System.Collections.Generic;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// UIRoot - 面板操作
    /// </summary>
    public partial class UIRoot
    {
        /// <summary>
        /// 正在异步加载中的面板类型集合，防止并发重复创建
        /// </summary>
        private readonly HashSet<Type> mLoadingPanelTypes = new();

        #region 面板操作（供 UIKit 调用）

        internal IPanel OpenPanelInternal(Type type, UILevel level, IUIData data, string tag = null)
        {
            WeakenAllHot();

            if (TryGetCachedHandler(type, out var handler))
            {
                handler.Data = data;
                handler.Hot += OpenHot;
                ApplyCachedHandlerParams(handler, level, tag);
                OpenAndShowPanelInternal(handler.Panel, data);
                return handler.Panel;
            }

            if (mLoadingPanelTypes.Contains(type))
            {
                KitLogger.Warning($"[UIRoot] 面板正在异步加载中，忽略同步打开: {type.Name}");
                return null;
            }

            handler = PanelHandler.Allocate();
            handler.Type = type;
            handler.Level = level;
            handler.Data = data;
            handler.Tag = tag;

            var panel = LoadPanel(handler);
            if (panel != default && panel.Transform != default)
            {
                SetupPanelInternal(handler, panel);
                OpenAndShowPanelInternal(panel, data);
                return panel;
            }

            handler.Recycle();
            return null;
        }

        internal void OpenPanelAsyncInternal(Type type, UILevel level, IUIData data, Action<IPanel> callback, string tag = null)
        {
            if (TryGetCachedHandler(type, out var handler))
            {
                handler.Data = data;
                handler.Hot += OpenHot;
                ApplyCachedHandlerParams(handler, level, tag);
                OpenAndShowPanelInternal(handler.Panel, data);
                callback?.Invoke(handler.Panel);
                return;
            }

            if (mLoadingPanelTypes.Contains(type))
            {
                KitLogger.Warning($"[UIRoot] 面板正在加载中，忽略重复请求: {type.Name}");
                callback?.Invoke(null);
                return;
            }

            mLoadingPanelTypes.Add(type);

            handler = PanelHandler.Allocate();
            handler.Type = type;
            handler.Level = level;
            handler.Data = data;
            handler.Tag = tag;

            LoadPanelAsync(handler, panel =>
            {
                mLoadingPanelTypes.Remove(type);

                if (panel != default && panel.Transform != default)
                {
                    SetupPanelInternal(handler, panel);
                    OpenAndShowPanelInternal(panel, data);
                    callback?.Invoke(panel);
                }
                else
                {
                    handler.Recycle();
                    callback?.Invoke(null);
                }
            });
        }

        /// <summary>
        /// 异步打开面板。安装 UniTask 后返回 UniTask，否则返回 Task。
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        internal async UniTask<IPanel> OpenPanelAsyncInternal(Type type, UILevel level, IUIData data,
            CancellationToken ct, string tag = null)
#else
        internal async Task<IPanel> OpenPanelAsyncInternal(Type type, UILevel level, IUIData data,
            CancellationToken ct, string tag = null)
#endif
        {
            if (TryGetCachedHandler(type, out var handler))
            {
                handler.Data = data;
                handler.Hot += OpenHot;
                ApplyCachedHandlerParams(handler, level, tag);
                OpenAndShowPanelInternal(handler.Panel, data);
                return handler.Panel;
            }

            if (mLoadingPanelTypes.Contains(type))
            {
                KitLogger.Warning($"[UIRoot] 面板正在加载中，忽略重复请求: {type.Name}");
                return null;
            }

            mLoadingPanelTypes.Add(type);

            handler = PanelHandler.Allocate();
            handler.Type = type;
            handler.Level = level;
            handler.Data = data;
            handler.Tag = tag;

#if YOKIFRAME_UNITASK_SUPPORT
            var panel = await LoadPanelAsync(handler, ct);
#else
            var panel = await LoadPanelAsync(handler, ct);
#endif

            mLoadingPanelTypes.Remove(type);

            if (panel != default && panel.Transform != default)
            {
                SetupPanelInternal(handler, panel);
                OpenAndShowPanelInternal(panel, data);
                return panel;
            }

            handler.Recycle();
            return null;
        }

        /// <summary>
        /// 缓存命中时，将新传入的参数（Level、Tag）应用到已有 Handler
        /// </summary>
        private void ApplyCachedHandlerParams(PanelHandler handler, UILevel level, string tag)
        {
            // 更新 Tag（同步 TagIndex）
            if (handler.Tag != tag)
            {
                // 先从旧 Tag 索引移除
                if (!string.IsNullOrEmpty(handler.Tag) && mTagIndex.TryGetValue(handler.Tag, out var oldSet))
                {
                    oldSet.Remove(handler.Type);
                    if (oldSet.Count == 0) mTagIndex.Remove(handler.Tag);
                }
                handler.Tag = tag;
                // 添加到新 Tag 索引
                if (!string.IsNullOrEmpty(tag))
                {
                    if (!mTagIndex.TryGetValue(tag, out var newSet))
                    {
                        newSet = new System.Collections.Generic.HashSet<Type>();
                        mTagIndex[tag] = newSet;
                    }
                    newSet.Add(handler.Type);
                }
            }

            // 更新 Level（需要重新注册层级和移动 Transform 父节点）
            if (handler.Level != level)
            {
                UnregisterPanelFromLevel(handler.Panel);
                handler.Level = level;
                if (UnityEngine.Application.isPlaying)
                {
                    SetLevelOfPanel(level, handler.Panel);
                }
                RegisterPanelToLevel(handler.Panel);
            }
        }

        private void SetupPanelInternal(PanelHandler handler, IPanel panel)
        {
            panel.Transform.gameObject.name = handler.Type.Name;
            AddToOpenedCache(handler.Type, handler);
            handler.Hot += OpenHot;
            panel.Init(handler.Data);
            RegisterPanelToLevel(panel);
        }

        private void OpenAndShowPanelInternal(IPanel panel, IUIData data)
        {
            if (panel == default) return;
            panel.Open(data);
            RegisterPanelToLevel(panel);
            panel.Show();
        }

        /// <summary>
        /// 发起一次由 UIRoot 管理的幂等关闭流程。
        /// </summary>
        internal void ClosePanelInternal(IPanel panel)
        {
            if (panel == default) return;

            var handler = panel.Handler;
            if (handler == default) return;
            if (!ReferenceEquals(handler.Panel, panel))
            {
                if (ReferenceEquals(panel.Handler, handler)) panel.Handler = null;
                return;
            }
            if (handler.IsRecycled || handler.Type == default)
            {
                if (ReferenceEquals(panel.Handler, handler)) panel.Handler = null;
                return;
            }

            if (panel is UnityEngine.Object unityPanel && unityPanel == null)
            {
                CompleteDestroyedPanelCloseInternal(panel, handler);
                return;
            }

            if (handler.RootCloseState != PanelRootCloseState.None) return;

            handler.RootCloseState = PanelRootCloseState.Pending;
            handler.RootCloseVersion = unchecked(handler.RootCloseVersion + 1);
            var closeVersion = handler.RootCloseVersion;

            if (panel is UIPanel uiPanel)
            {
                uiPanel.Close(() => CompletePanelCloseInternal(panel, handler, closeVersion));
                return;
            }

            panel.Close();
            CompletePanelCloseInternal(panel, handler, closeVersion);
        }

        /// <summary>
        /// 完成指定 Handler 所属打开轮次的栈、层级、缓存和销毁清理。
        /// </summary>
        private void CompletePanelCloseInternal(IPanel panel, PanelHandler handler, int closeVersion)
        {
            if (panel == default || handler == default) return;

            if (panel is UnityEngine.Object unityPanel && unityPanel == null)
            {
                if (!handler.IsRecycled && ReferenceEquals(panel.Handler, handler) &&
                    ReferenceEquals(handler.Panel, panel))
                {
                    CompleteDestroyedPanelCloseInternal(panel, handler);
                }
                return;
            }

            if (!IsCurrentPanelClose(panel, handler, closeVersion, PanelRootCloseState.Pending)) return;

            bool shouldDestroy = ShouldDestroyOnClose(handler);
            var panelType = handler.Type;
            handler.RootCloseState = PanelRootCloseState.Finalizing;
            if (shouldDestroy)
            {
                RemoveFromOpenedCache(panelType, handler);
            }

            RemoveFromStack(panel);
            if (panel is UnityEngine.Object currentUnityPanel && currentUnityPanel == null)
            {
                if (!handler.IsRecycled && handler.RootCloseVersion == closeVersion &&
                    ReferenceEquals(panel.Handler, handler) && ReferenceEquals(handler.Panel, panel))
                {
                    CompleteDestroyedPanelCloseInternal(panel, handler);
                }
                return;
            }
            if (!IsCurrentPanelClose(panel, handler, closeVersion, PanelRootCloseState.Finalizing)) return;

            UnregisterPanelFromLevel(panel);
            OnPanelCloseFocus(panel);
            handler.RootCloseState = PanelRootCloseState.Finalized;

            if (shouldDestroy)
            {
                DestroyPanelInternal(panel);
                handler.Recycle();
            }
        }

        /// <summary>
        /// 清理已被外部销毁、但仍由当前 Handler 持有的面板记录。
        /// </summary>
        private void CompleteDestroyedPanelCloseInternal(IPanel panel, PanelHandler handler)
        {
            if (handler == default || handler.IsRecycled || handler.Type == default) return;
            if (!ReferenceEquals(panel.Handler, handler) || !ReferenceEquals(handler.Panel, panel)) return;
            if (handler.RootCloseState == PanelRootCloseState.DestroyedFinalizing) return;

            var panelType = handler.Type;
            var closeVersion = handler.RootCloseVersion;
            handler.RootCloseState = PanelRootCloseState.DestroyedFinalizing;
            RemoveFromOpenedCache(panelType, handler);
            RemoveFromStack(panel);
            if (!IsCurrentDestroyedPanelClose(panel, handler, closeVersion)) return;

            UnregisterPanelFromLevel(panel);
            OnPanelCloseFocus(panel);
            handler.RootCloseState = PanelRootCloseState.Finalized;

            if (ReferenceEquals(panel.Handler, handler)) panel.Handler = null;
            if (ReferenceEquals(handler.Panel, panel)) handler.Panel = null;
            handler.Recycle();
        }

        /// <summary>
        /// 判断回调是否仍属于面板当前的根关闭轮次。
        /// </summary>
        private static bool IsCurrentPanelClose(IPanel panel, PanelHandler handler, int closeVersion,
            PanelRootCloseState expectedState)
        {
            if (panel == default || handler == default || handler.IsRecycled) return false;
            if (panel is UnityEngine.Object unityPanel && unityPanel == null) return false;
            if (handler.RootCloseState != expectedState || handler.RootCloseVersion != closeVersion)
                return false;
            return ReferenceEquals(panel.Handler, handler) && ReferenceEquals(handler.Panel, panel) &&
                   handler.Type != default;
        }

        /// <summary>
        /// 判断 fake-null 清理是否仍持有同一面板和关闭轮次。
        /// </summary>
        private static bool IsCurrentDestroyedPanelClose(IPanel panel, PanelHandler handler, int closeVersion)
        {
            if (panel == default || handler == default || handler.IsRecycled) return false;
            if (handler.RootCloseState != PanelRootCloseState.DestroyedFinalizing ||
                handler.RootCloseVersion != closeVersion || handler.Type == default)
                return false;
            if (panel is not UnityEngine.Object unityPanel || unityPanel != null) return false;
            return ReferenceEquals(panel.Handler, handler) && ReferenceEquals(handler.Panel, panel);
        }

        /// <summary>
        /// 清理面板资源、解除 Handler 所有权并销毁宿主对象。
        /// </summary>
        internal void DestroyPanelInternal(IPanel panel)
        {
            if (panel == default) return;
            if (panel is UnityEngine.Object unityPanel && unityPanel == null) return;

            var panelTransform = panel.Transform;
            if (panelTransform == default || panelTransform.gameObject == default) return;

            var panelObject = panelTransform.gameObject;
            var handler = panel.Handler;
            panel.Cleanup();

            bool panelIsAlive = !(panel is UnityEngine.Object currentUnityPanel) || currentUnityPanel != null;
            if (panelIsAlive && handler != default && ReferenceEquals(panel.Handler, handler))
            {
                panel.Handler = null;
            }
            if (handler != default && ReferenceEquals(handler.Panel, panel))
            {
                handler.Panel = null;
            }

            if (panelObject == default) return;

            if (UnityEngine.Application.isPlaying)
            {
                Destroy(panelObject);
            }
            else
            {
                DestroyImmediate(panelObject);
            }
        }

        #endregion
    }
}
#endif
