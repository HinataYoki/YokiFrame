#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Binding and subscription helpers shared by all tool pages.
    /// These helpers centralize lifecycle-safe subscriptions so page implementations do not need
    /// to manually track every disposable in common scenarios.
    /// </summary>
    public abstract partial class YokiToolPageBase
    {
        #region Event Subscription

        /// <summary>
        /// Subscribes to an <see cref="EditorEventCenter"/> type-based event and automatically
        /// attaches the subscription to the page lifecycle.
        /// </summary>
        protected void SubscribeEvent<T>(Action<T> handler)
        {
            Subscriptions.Add(EditorEventCenter.Register(this, handler));
        }

        /// <summary>
        /// Subscribes to an <see cref="EditorEventCenter"/> keyed event and automatically
        /// attaches the subscription to the page lifecycle.
        /// </summary>
        protected void SubscribeEvent<TKey, TValue>(TKey key, Action<TValue> handler) where TKey : Enum
        {
            Subscriptions.Add(EditorEventCenter.Register(this, key, handler));
        }

        /// <summary>
        /// Subscribes to an <see cref="EditorDataBridge"/> channel and automatically disposes
        /// the subscription when the page deactivates.
        /// </summary>
        protected void SubscribeChannel<T>(string channel, Action<T> callback)
        {
            Subscriptions.Add(EditorDataBridge.Subscribe(channel, callback));
        }

        /// <summary>
        /// Subscribes to an <see cref="EditorDataBridge"/> channel with throttling and automatically
        /// disposes the subscription when the page deactivates.
        /// </summary>
        protected void SubscribeChannelThrottled<T>(string channel, Action<T> callback, float intervalSeconds)
        {
            Subscriptions.Add(EditorDataBridge.SubscribeThrottled(channel, callback, intervalSeconds));
        }

        #endregion

        #region Label Binding

        /// <summary>
        /// Binds a label to a string reactive property.
        /// </summary>
        protected IDisposable BindToLabel(Label label, ReactiveProperty<string> property)
        {
            if (label == default)
            {
                Debug.LogWarning("[YokiToolPage] BindToLabel: label is null, binding skipped.");
                return Disposable.Empty;
            }

            label.text = property.Value ?? string.Empty;
            var subscription = property.Subscribe(v => label.text = v ?? string.Empty);
            Subscriptions.Add(subscription);
            return subscription;
        }

        /// <summary>
        /// Binds a label to a reactive property with a custom formatter.
        /// </summary>
        protected IDisposable BindToLabel<T>(Label label, ReactiveProperty<T> property, Func<T, string> formatter)
        {
            if (label == default)
            {
                Debug.LogWarning("[YokiToolPage] BindToLabel: label is null, binding skipped.");
                return Disposable.Empty;
            }

            label.text = formatter(property.Value);
            var subscription = property.Subscribe(value => label.text = formatter(value));
            Subscriptions.Add(subscription);
            return subscription;
        }

        #endregion

        #region Visibility Binding

        /// <summary>
        /// Binds a visual element's display state to a boolean reactive property.
        /// </summary>
        protected IDisposable BindToVisibility(VisualElement element, ReactiveProperty<bool> property)
        {
            if (element == default)
            {
                Debug.LogWarning("[YokiToolPage] BindToVisibility: element is null, binding skipped.");
                return Disposable.Empty;
            }

            element.style.display = property.Value ? DisplayStyle.Flex : DisplayStyle.None;
            var subscription = property.Subscribe(visible =>
            {
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            });
            Subscriptions.Add(subscription);
            return subscription;
        }

        #endregion

        #region ListView Binding

        /// <summary>
        /// Binds a <see cref="ListView"/> to a reactive collection and refreshes the view whenever
        /// the collection changes.
        /// </summary>
        protected IDisposable BindToListView<T>(
            ListView listView,
            ReactiveCollection<T> collection,
            Func<VisualElement> makeItem,
            Action<VisualElement, int> bindItem)
        {
            if (listView == default)
            {
                Debug.LogWarning("[YokiToolPage] BindToListView: ListView is null, binding skipped.");
                return Disposable.Empty;
            }

            listView.makeItem = makeItem;
            listView.bindItem = bindItem;
            listView.itemsSource = collection;

            var subscription = collection.Subscribe(_ => listView.RefreshItems());
            Subscriptions.Add(subscription);
            return subscription;
        }

        #endregion

        #region Debounce And Throttle

        /// <summary>
        /// Creates a debounce helper bound to the page lifecycle.
        /// </summary>
        protected Debounce CreateDebounce(float delaySeconds)
        {
            var debounce = new Debounce(delaySeconds, Root);
            Subscriptions.Add(debounce);
            return debounce;
        }

        /// <summary>
        /// Creates a throttle helper bound to the page lifecycle.
        /// </summary>
        protected Throttle CreateThrottle(float intervalSeconds)
        {
            var throttle = new Throttle(intervalSeconds);
            Subscriptions.Add(throttle);
            return throttle;
        }

        #endregion
    }
}
#endif
