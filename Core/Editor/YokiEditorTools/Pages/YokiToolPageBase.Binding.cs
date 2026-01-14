#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YokiToolPageBase - 响应式数据绑定
    /// </summary>
    public abstract partial class YokiToolPageBase
    {
        #region 事件订阅

        /// <summary>
        /// 订阅 EditorEventCenter 类型事件（自动管理生命周期）
        /// </summary>
        protected void SubscribeEvent<T>(Action<T> handler)
        {
            Subscriptions.Add(EditorEventCenter.Register(this, handler));
        }

        /// <summary>
        /// 订阅 EditorEventCenter 枚举键事件（自动管理生命周期）
        /// </summary>
        protected void SubscribeEvent<TKey, TValue>(TKey key, Action<TValue> handler) where TKey : Enum
        {
            Subscriptions.Add(EditorEventCenter.Register(this, key, handler));
        }

        /// <summary>
        /// 订阅数据通道（自动管理生命周期）
        /// </summary>
        protected void SubscribeChannel<T>(string channel, Action<T> callback)
        {
            Subscriptions.Add(EditorDataBridge.Subscribe(channel, callback));
        }

        /// <summary>
        /// 订阅数据通道（带节流，自动管理生命周期）
        /// </summary>
        protected void SubscribeChannelThrottled<T>(string channel, Action<T> callback, float intervalSeconds)
        {
            Subscriptions.Add(EditorDataBridge.SubscribeThrottled(channel, callback, intervalSeconds));
        }

        #endregion

        #region Label 绑定

        /// <summary>
        /// 绑定 Label 到 ReactiveProperty（自动管理生命周期）
        /// </summary>
        protected IDisposable BindToLabel(Label label, ReactiveProperty<string> property)
        {
            if (label == default)
            {
                Debug.LogWarning("[YokiToolPage] BindToLabel: Label 为 null，绑定已跳过");
                return Disposable.Empty;
            }

            label.text = property.Value ?? string.Empty;
            var subscription = property.Subscribe(v => label.text = v ?? string.Empty);
            Subscriptions.Add(subscription);
            return subscription;
        }

        /// <summary>
        /// 绑定 Label 到 ReactiveProperty（带格式化）
        /// </summary>
        protected IDisposable BindToLabel<T>(Label label, ReactiveProperty<T> property, Func<T, string> formatter)
        {
            if (label == default)
            {
                Debug.LogWarning("[YokiToolPage] BindToLabel: Label 为 null，绑定已跳过");
                return Disposable.Empty;
            }

            label.text = formatter(property.Value);
            var subscription = property.Subscribe(value => label.text = formatter(value));
            Subscriptions.Add(subscription);
            return subscription;
        }

        #endregion

        #region 可见性绑定

        /// <summary>
        /// 绑定 VisualElement 可见性到 ReactiveProperty
        /// </summary>
        protected IDisposable BindToVisibility(VisualElement element, ReactiveProperty<bool> property)
        {
            if (element == default)
            {
                Debug.LogWarning("[YokiToolPage] BindToVisibility: Element 为 null，绑定已跳过");
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

        #region ListView 绑定

        /// <summary>
        /// 绑定 ListView 到 ReactiveCollection
        /// </summary>
        protected IDisposable BindToListView<T>(
            ListView listView,
            ReactiveCollection<T> collection,
            Func<VisualElement> makeItem,
            Action<VisualElement, int> bindItem)
        {
            if (listView == default)
            {
                Debug.LogWarning("[YokiToolPage] BindToListView: ListView 为 null，绑定已跳过");
                return Disposable.Empty;
            }

            listView.makeItem = makeItem;
            listView.bindItem = bindItem;
            listView.itemsSource = collection;

            // 使用闭包捕获 listView（ReactiveCollection.Subscribe 不支持 context 参数）
            var subscription = collection.Subscribe(_ => listView.RefreshItems());
            Subscriptions.Add(subscription);
            return subscription;
        }

        #endregion

        #region 防抖/节流

        /// <summary>
        /// 创建防抖器（自动管理生命周期）
        /// </summary>
        protected Debounce CreateDebounce(float delaySeconds)
        {
            var debounce = new Debounce(delaySeconds, Root);
            Subscriptions.Add(debounce);
            return debounce;
        }

        /// <summary>
        /// 创建节流器（自动管理生命周期）
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
