#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 编辑器数据桥接 - 运行时数据变化通知编辑器
    /// 运行时调用 NotifyDataChanged，编辑器订阅 Subscribe
    /// 使用 [Conditional] 确保运行时零开销
    /// </summary>
    public static class EditorDataBridge
    {
        // 事件通道字典：通道名 -> 监听器列表
        private static readonly Dictionary<string, List<Delegate>> sChannels = new(16);
        
        // 节流状态：通道名 -> 最后通知时间
        private static readonly Dictionary<string, double> sThrottleTimestamps = new(8);

        #region 运行时调用（零开销）

        /// <summary>
        /// 通知数据变化（运行时调用）
        /// 使用 [Conditional] 确保非编辑器构建时完全移除
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void NotifyDataChanged<T>(string channel, T data)
        {
            if (!sChannels.TryGetValue(channel, out var listeners)) return;
            if (listeners.Count == 0) return;
            
            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                if (listeners[i] is Action<T> callback)
                {
                    try
                    {
                        callback(data);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogException(ex);
                    }
                }
            }
        }

        /// <summary>
        /// 通知数据变化（无参数版本）
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void NotifyDataChanged(string channel)
        {
            NotifyDataChanged<object>(channel, null);
        }

        #endregion

        #region 编辑器订阅

        /// <summary>
        /// 订阅数据变化（编辑器调用）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="channel">通道名</param>
        /// <param name="callback">回调函数</param>
        /// <returns>用于取消订阅的 IDisposable</returns>
        public static IDisposable Subscribe<T>(string channel, Action<T> callback)
        {
            if (string.IsNullOrEmpty(channel) || callback == null) 
                return Disposable.Empty;

            if (!sChannels.TryGetValue(channel, out var listeners))
            {
                listeners = new List<Delegate>(4);
                sChannels[channel] = listeners;
            }
            
            listeners.Add(callback);
            
            return Disposable.Create(() =>
            {
                listeners.Remove(callback);
                // 清理空通道
                if (listeners.Count == 0)
                {
                    sChannels.Remove(channel);
                }
            });
        }

        /// <summary>
        /// 订阅数据变化（带节流）
        /// 在指定时间间隔内只处理最后一次数据
        /// </summary>
        public static IDisposable SubscribeThrottled<T>(
            string channel, 
            Action<T> callback, 
            float intervalSeconds)
        {
            if (string.IsNullOrEmpty(channel) || callback == null) 
                return Disposable.Empty;

            string throttleKey = $"{channel}_throttle_{callback.GetHashCode()}";
            T latestData = default;
            bool hasPendingData = false;

            return Subscribe<T>(channel, data =>
            {
                var now = EditorApplication.timeSinceStartup;
                
                // 检查是否在节流间隔内
                if (sThrottleTimestamps.TryGetValue(throttleKey, out var lastTime))
                {
                    if (now - lastTime < intervalSeconds)
                    {
                        // 在间隔内，缓存数据等待下次执行
                        latestData = data;
                        hasPendingData = true;
                        return;
                    }
                }

                // 立即执行
                sThrottleTimestamps[throttleKey] = now;
                callback(data);
                hasPendingData = false;
            });
        }

        #endregion

        #region 生命周期管理

        /// <summary>
        /// 清理所有订阅（PlayMode 退出时调用）
        /// </summary>
        public static void ClearAll()
        {
            sChannels.Clear();
            sThrottleTimestamps.Clear();
        }

        /// <summary>
        /// 清理指定通道的所有订阅
        /// </summary>
        public static void ClearChannel(string channel)
        {
            sChannels.Remove(channel);
        }

        /// <summary>
        /// 获取通道订阅者数量（调试用）
        /// </summary>
        public static int GetSubscriberCount(string channel)
        {
            return sChannels.TryGetValue(channel, out var listeners) ? listeners.Count : 0;
        }

        #endregion

        #region 初始化

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // PlayMode 退出时清理所有订阅
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                {
                    ClearAll();
                }
            };
        }

        #endregion
    }
}
#endif
