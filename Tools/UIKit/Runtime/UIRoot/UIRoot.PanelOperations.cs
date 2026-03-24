using System;
using System.Collections.Generic;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
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

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// [UniTask] 异步打开面板（内部使用，直接走原生 UniTask 加载路径）
        /// </summary>
        internal async UniTask<IPanel> OpenPanelUniTaskAsyncInternal(Type type, UILevel level, IUIData data,
            System.Threading.CancellationToken ct, string tag = null)
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

            var panel = await LoadPanelUniTaskAsync(handler, ct);

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
#endif

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
            panel.Show();
        }

        internal void ClosePanelInternal(IPanel panel)
        {
            if (panel == default) return;

            var unityObj = panel as UnityEngine.Object;
            if (unityObj == default)
            {
                if (panel.Handler != default)
                {
                    RemoveFromStack(panel);
                    UnregisterPanelFromLevel(panel);
                    RemoveFromOpenedCache(panel.Handler.Type);
                    panel.Handler.Recycle();
                }
                return;
            }

            panel.Close();
            if (panel.Handler == default) return;

            RemoveFromStack(panel);
            UnregisterPanelFromLevel(panel);
            OnPanelCloseFocus(panel);

            // 根据 CacheMode 决策是否销毁
            if (ShouldDestroyOnClose(panel.Handler))
            {
                DestroyPanelInternal(panel);
                RemoveFromOpenedCache(panel.Handler.Type);
                panel.Handler.Recycle();
            }
        }

        internal void DestroyPanelInternal(IPanel panel)
        {
            if (panel != default && panel.Transform != default && panel.Transform.gameObject != default)
            {
                panel.Cleanup();
                Destroy(panel.Transform.gameObject);
            }
        }

        #endregion
    }
}
