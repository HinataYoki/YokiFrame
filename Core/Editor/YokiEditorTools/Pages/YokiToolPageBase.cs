#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YokiFrame 工具页面基类
    /// 
    /// 提供现代化 UI 组件的便捷创建方法和响应式数据绑定支持。
    /// 按功能拆分为多个 partial class 文件：
    /// - YokiToolPageBase.cs          ← 核心生命周期（你在这里）
    /// - YokiToolPageBase.Binding.cs  ← 响应式数据绑定
    /// - YokiToolPageBase.Components.cs ← UI 组件创建
    /// 
    /// 页面元数据通过 [YokiToolPage] 特性声明，无需重写属性。
    /// </summary>
    public abstract partial class YokiToolPageBase : IYokiToolPage
    {
        #region 元数据（从 Attribute 自动获取）

        private YokiToolPageAttribute mCachedAttribute;

        /// <summary>
        /// 获取页面特性（缓存）
        /// </summary>
        private YokiToolPageAttribute GetAttribute()
        {
            if (mCachedAttribute == default)
            {
                var attrs = GetType().GetCustomAttributes(typeof(YokiToolPageAttribute), false);
                if (attrs.Length > 0)
                {
                    mCachedAttribute = (YokiToolPageAttribute)attrs[0];
                }
            }
            return mCachedAttribute;
        }

        /// <summary>
        /// 页面名称（从 [YokiToolPage] 特性获取）
        /// </summary>
        public virtual string PageName => GetAttribute()?.Name ?? GetType().Name;

        /// <summary>
        /// 页面图标（从 [YokiToolPage] 特性获取）
        /// </summary>
        public virtual string PageIcon => GetAttribute()?.Icon ?? KitIcons.DOCUMENT;

        /// <summary>
        /// 排序优先级（从 [YokiToolPage] 特性获取）
        /// </summary>
        public virtual int Priority => GetAttribute()?.Priority ?? 100;

        #endregion
        #region 保护属性

        /// <summary>
        /// 当前是否处于 PlayMode
        /// </summary>
        protected bool IsPlaying => EditorApplication.isPlaying;

        /// <summary>
        /// 页面根元素
        /// </summary>
        protected VisualElement Root { get; private set; }

        /// <summary>
        /// 订阅管理器 - 自动在 OnDeactivate 时清理所有订阅
        /// </summary>
        protected CompositeDisposable Subscriptions { get; } = new(8);

        #endregion

        #region 私有字段

        /// <summary>
        /// VisualElement 查询缓存 - 避免重复查询
        /// </summary>
        private readonly Dictionary<string, VisualElement> mQueryCache = new(16);

        #endregion

        #region 生命周期

        public VisualElement CreateUI()
        {
            Root = new VisualElement();
            Root.style.flexGrow = 1;
            BuildUI(Root);
            return Root;
        }

        /// <summary>
        /// 构建页面 UI（子类实现）
        /// </summary>
        protected abstract void BuildUI(VisualElement root);

        public virtual void OnActivate()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public virtual void OnDeactivate()
        {
            // 清理所有订阅
            Subscriptions.Clear();
            // 清理查询缓存
            mQueryCache.Clear();
            // 清理 EditorEventCenter 中属于此页面的订阅
            EditorEventCenter.UnregisterAll(this);
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        /// <summary>
        /// 轮询更新（已废弃，请使用响应式订阅）
        /// </summary>
        [Obsolete("使用响应式订阅替代轮询。此方法将在未来版本中移除。")]
        public virtual void OnUpdate() { }

        /// <summary>
        /// PlayMode 状态变化回调，子类可重写
        /// </summary>
        protected virtual void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Subscriptions.Clear();
            }
        }

        #endregion

        #region 查询缓存

        /// <summary>
        /// 缓存查询 VisualElement - 避免重复调用 Q&lt;T&gt;()
        /// </summary>
        protected T QueryCached<T>(string name) where T : VisualElement
        {
            if (Root == default) return default;

            if (!mQueryCache.TryGetValue(name, out var element))
            {
                element = Root.Q<T>(name);
                if (element != default) mQueryCache[name] = element;
            }
            return element as T;
        }

        /// <summary>
        /// 缓存查询 VisualElement（通过类名）
        /// </summary>
        protected T QueryCachedByClass<T>(string className) where T : VisualElement
        {
            if (Root == default) return default;

            var cacheKey = $"class:{className}";
            if (!mQueryCache.TryGetValue(cacheKey, out var element))
            {
                element = Root.Q<T>(className: className);
                if (element != default) mQueryCache[cacheKey] = element;
            }
            return element as T;
        }

        /// <summary>
        /// 清除查询缓存
        /// </summary>
        /// protected void ClearQueryCache() => mQueryCache.Clear();

        #endregion
    }
}
#endif
