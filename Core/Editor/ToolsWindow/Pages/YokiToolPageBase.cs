#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Shared base class for YokiFrame tool pages.
    /// </summary>
    /// <remarks>
    /// This class provides the editor page lifecycle, metadata lookup, query caching, and shared subscription
    /// cleanup. Concrete page UI and page-specific editor logic should stay inside each kit's own editor code.
    /// Other helper capabilities are split into partial files such as binding helpers and component builders.
    /// </remarks>
    public abstract partial class YokiToolPageBase : IYokiToolPage
    {
        #region Metadata

        private YokiToolPageAttribute mCachedAttribute;

        /// <summary>
        /// Gets the cached <see cref="YokiToolPageAttribute"/> on the current page type.
        /// </summary>
        private YokiToolPageAttribute GetAttribute()
        {
            if (mCachedAttribute == null)
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
        /// Display name shown in the tools window.
        /// </summary>
        public virtual string PageName => GetAttribute()?.Name ?? GetType().Name;

        /// <summary>
        /// Icon id used by the page.
        /// </summary>
        public virtual string PageIcon => GetAttribute()?.Icon ?? KitIcons.DOCUMENT;

        /// <summary>
        /// Sorting priority, lower values appear first.
        /// </summary>
        public virtual int Priority => GetAttribute()?.Priority ?? 100;

        #endregion

        #region Protected Properties

        /// <summary>
        /// Indicates whether the editor is currently in Play Mode.
        /// </summary>
        protected bool IsPlaying => EditorApplication.isPlaying;

        /// <summary>
        /// Root visual element of the page.
        /// </summary>
        protected VisualElement Root { get; private set; }

        /// <summary>
        /// Subscription container cleared automatically when the page deactivates.
        /// </summary>
        protected CompositeDisposable Subscriptions { get; } = new(8);

        #endregion

        #region Private Fields

        /// <summary>
        /// Cached element queries to avoid repeated <c>Q&lt;T&gt;()</c> lookups.
        /// </summary>
        private readonly Dictionary<string, VisualElement> mQueryCache = new(16);

        #endregion

        #region Lifecycle

        /// <summary>
        /// Creates the page root UI and delegates layout construction to <see cref="BuildUI"/>.
        /// </summary>
        public VisualElement CreateUI()
        {
            Root = new VisualElement();
            Root.style.flexGrow = 1;
            BuildUI(Root);
            return Root;
        }

        /// <summary>
        /// Builds the page UI.
        /// </summary>
        /// <param name="root">Page root element.</param>
        protected abstract void BuildUI(VisualElement root);

        /// <summary>
        /// Called when the page becomes active.
        /// </summary>
        public virtual void OnActivate()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>
        /// Called when the page is deactivated.
        /// </summary>
        public virtual void OnDeactivate()
        {
            Subscriptions.Clear();
            mQueryCache.Clear();
            EditorEventCenter.UnregisterAll(this);
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        /// <summary>
        /// Legacy polling update hook.
        /// </summary>
        /// <remarks>
        /// Prefer reactive subscriptions instead of per-frame polling.
        /// </remarks>
        [Obsolete("Use reactive subscriptions instead of polling. This method will be removed in a future version.")]
        public virtual void OnUpdate() { }

        /// <summary>
        /// Called when Play Mode state changes.
        /// </summary>
        /// <param name="state">Current Play Mode state.</param>
        protected virtual void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Subscriptions.Clear();
            }
        }

        #endregion

        #region Query Cache

        /// <summary>
        /// Queries an element by name and caches the result.
        /// </summary>
        /// <typeparam name="T">Expected element type.</typeparam>
        /// <param name="name">Element name.</param>
        /// <returns>The matching element or <see langword="null"/>.</returns>
        protected T QueryCached<T>(string name) where T : VisualElement
        {
            if (Root == null) return null;

            if (!mQueryCache.TryGetValue(name, out var element))
            {
                element = Root.Q<T>(name);
                if (element != null)
                {
                    mQueryCache[name] = element;
                }
            }

            return element as T;
        }

        /// <summary>
        /// Queries an element by USS class name and caches the result.
        /// </summary>
        /// <typeparam name="T">Expected element type.</typeparam>
        /// <param name="className">USS class name.</param>
        /// <returns>The matching element or <see langword="null"/>.</returns>
        protected T QueryCachedByClass<T>(string className) where T : VisualElement
        {
            if (Root == null) return null;

            var cacheKey = $"class:{className}";
            if (!mQueryCache.TryGetValue(cacheKey, out var element))
            {
                element = Root.Q<T>(className: className);
                if (element != null)
                {
                    mQueryCache[cacheKey] = element;
                }
            }

            return element as T;
        }

        /// <summary>
        /// Clears cached element query results.
        /// </summary>
        protected void ClearQueryCache()
        {
            mQueryCache.Clear();
        }

        #endregion
    }

    /// <summary>
    /// Shared base window for standalone monitor-style editor windows.
    /// </summary>
    /// <remarks>
    /// This skeleton keeps common monitor behavior inside Core while leaving concrete UI, refresh logic, and kit
    /// semantics inside each kit's editor implementation.
    /// </remarks>
    public abstract class YokiMonitorWindowBase : EditorWindow
    {
        private double mLastRefreshTime;

        /// <summary>
        /// Opens or focuses a monitor window with standard title and minimum size setup.
        /// </summary>
        protected static TWindow OpenMonitorWindow<TWindow>(string windowTitle, Vector2 minSize)
            where TWindow : EditorWindow
        {
            var window = GetWindow<TWindow>(false, windowTitle);
            window.titleContent = new GUIContent(windowTitle);
            window.minSize = minSize;
            window.Show();
            window.Focus();
            return window;
        }

        /// <summary>
        /// Refresh interval in seconds while the editor is in Play Mode.
        /// </summary>
        protected abstract float RefreshIntervalSeconds { get; }

        /// <summary>
        /// Owning kit name used for style lookup.
        /// </summary>
        protected abstract string MonitorKitName { get; }

        /// <summary>
        /// Builds the concrete monitor UI.
        /// </summary>
        protected abstract void BuildMonitorUI(VisualElement root);

        /// <summary>
        /// Called when the monitor window is enabled.
        /// </summary>
        protected virtual void OnMonitorEnabled() { }

        /// <summary>
        /// Called when the monitor window is disabled.
        /// </summary>
        protected virtual void OnMonitorDisabled() { }

        /// <summary>
        /// Called when Play Mode state changes.
        /// </summary>
        protected virtual void OnMonitorPlayModeStateChanged(PlayModeStateChange state) { }

        /// <summary>
        /// Refreshes monitor data when the update interval elapses.
        /// </summary>
        protected virtual void RefreshMonitorData() { }

        /// <summary>
        /// Initializes the shared monitor root styles and delegates content construction.
        /// </summary>
        protected void InitializeMonitorWindowUI(VisualElement root)
        {
            root.style.flexDirection = FlexDirection.Column;
            root.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f));

            YokiStyleService.Apply(root, YokiStyleProfile.Full);

            if (!string.IsNullOrEmpty(MonitorKitName))
            {
                YokiStyleService.ApplyKitStyleToElement(root, MonitorKitName);
            }

            BuildMonitorUI(root);
        }

        /// <summary>
        /// Hooks shared lifecycle behavior for <c>OnEnable</c>.
        /// </summary>
        protected void HandleMonitorWindowEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChangedInternal;
            OnMonitorEnabled();
        }

        /// <summary>
        /// Hooks shared lifecycle behavior for <c>OnDisable</c>.
        /// </summary>
        protected void HandleMonitorWindowDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChangedInternal;
            OnMonitorDisabled();
        }

        /// <summary>
        /// Handles throttled monitor refreshes from <c>Update</c>.
        /// </summary>
        protected void HandleMonitorWindowUpdate()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup - mLastRefreshTime <= RefreshIntervalSeconds)
            {
                return;
            }

            mLastRefreshTime = EditorApplication.timeSinceStartup;
            RefreshMonitorData();
        }

        /// <summary>
        /// Resets the internal refresh timer.
        /// </summary>
        protected void ResetMonitorRefreshTimer()
        {
            mLastRefreshTime = 0d;
        }

        /// <summary>
        /// Creates a lightweight monitor header row with optional icon and trailing element.
        /// </summary>
        protected VisualElement CreateMonitorHeaderRow(string title, string icon = null, VisualElement trailing = null)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            if (!string.IsNullOrEmpty(icon))
            {
                var iconElement = new Image { image = KitIcons.GetTexture(icon) };
                iconElement.style.width = 16;
                iconElement.style.height = 16;
                iconElement.style.marginRight = 6;
                header.Add(iconElement);
            }

            var titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
            titleLabel.style.flexGrow = 1;
            header.Add(titleLabel);

            if (trailing != null)
            {
                header.Add(trailing);
            }

            return header;
        }

        /// <summary>
        /// Creates a monitor panel header with built-in padding and divider styling.
        /// </summary>
        protected VisualElement CreateMonitorPanelHeader(string title, string icon = null, VisualElement trailing = null)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingTop = 8;
            header.style.paddingBottom = 8;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
            header.Add(CreateMonitorHeaderRow(title, icon, trailing));
            return header;
        }

        /// <summary>
        /// Resets shared timer state before delegating Play Mode changes to the derived window.
        /// </summary>
        private void OnPlayModeStateChangedInternal(PlayModeStateChange state)
        {
            ResetMonitorRefreshTimer();
            OnMonitorPlayModeStateChanged(state);
        }
    }
}
#endif
