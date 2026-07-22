#if UNITY_5_3_OR_NEWER
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 在 Unity Player 中显示 LogKit 已通过过滤的日志；仅由 Runtime Adapter 按设置创建。
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class UnityLogKitPlayerOverlay : MonoBehaviour
    {
        private const float PANEL_MARGIN = 12f;
        private const float PANEL_MAX_WIDTH = 720f;
        private const float PANEL_MAX_HEIGHT = 380f;
        private const float PANEL_PADDING = 8f;
        private const float SCROLLBAR_WIDTH = 18f;
        private static readonly object sSettingsLock = new();
        private static readonly SendOrPostCallback sApplyPostedSettings = ApplyPostedSettings;
        private static UnityLogKitPlayerOverlay sInstance;
        private static volatile UnityLogKitPlayerLogBuffer sBuffer;
        private static volatile SynchronizationContext sMainThreadContext;
        private static volatile int sMainThreadId;
        private static bool sRequestedEnabled;
        private static int sRequestedMaxLogCount = LogKitSettings.DEFAULT_IMGUI_MAX_LOG_COUNT;
        private readonly StringBuilder mTextBuilder = new(1024);
        private GUIContent mContent;
        private GUIStyle mContentStyle;
        private string mCachedText = "No LogKit messages.";
        private Vector2 mScrollPosition;
        private bool mScrollToBottom;

        /// <summary>
        /// 按 Runtime Settings 更新覆盖层状态；非 PlayMode 不创建 Unity 对象，避免 Editor 配置影响 Player 生命周期。
        /// </summary>
        /// <param name="enabled">是否启用 Player 调试覆盖层。</param>
        /// <param name="maxLogCount">最多保留的日志条数。</param>
        internal static void ApplySettings(bool enabled, int maxLogCount)
        {
            lock (sSettingsLock)
            {
                sRequestedEnabled = enabled;
                sRequestedMaxLogCount = NormalizeMaxLogCount(maxLogCount);
            }

            if (IsCurrentMainThread())
            {
                ApplyRequestedSettings();
                return;
            }

            SynchronizationContext context = sMainThreadContext;
            if (context != null)
            {
                context.Post(sApplyPostedSettings, null);
            }
        }

        /// <summary>
        /// 记录一条已经由 LogKit 过滤并格式化的日志；覆盖层未启用时直接返回，不触碰 Unity UI 对象。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="message">日志正文。</param>
        internal static void Record(LogLevel level, string message)
        {
            UnityLogKitPlayerLogBuffer buffer = sBuffer;
            if (buffer == null)
            {
                return;
            }

            buffer.Record(level, message);
        }

        /// <summary>
        /// 销毁当前 Player 覆盖层并清除缓冲引用，供 Unity 子系统重置和适配器关闭调用。
        /// </summary>
        internal static void Reset()
        {
            lock (sSettingsLock)
            {
                sRequestedEnabled = false;
                sRequestedMaxLogCount = LogKitSettings.DEFAULT_IMGUI_MAX_LOG_COUNT;
            }

            sBuffer = null;
            sMainThreadContext = null;
            sMainThreadId = 0;
            DestroyInstance();
        }

        /// <summary>
        /// 在场景加载后捕获 Unity 主线程上下文，并依据已解析的 Runtime Settings 初始化覆盖层。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CaptureMainThreadAndApplySettings()
        {
            sMainThreadContext = SynchronizationContext.Current;
            sMainThreadId = Thread.CurrentThread.ManagedThreadId;
            ApplySettings(
                LogKitSettings.GetBool(
                    LogKitSettings.ENABLE_IMGUI_IN_PLAYER_KEY,
                    LogKitSettings.DEFAULT_ENABLE_IMGUI_IN_PLAYER),
                LogKitSettings.GetInt(
                    LogKitSettings.IMGUI_MAX_LOG_COUNT_KEY,
                    LogKitSettings.DEFAULT_IMGUI_MAX_LOG_COUNT));
        }

        /// <summary>
        /// 由 Unity 主线程同步上下文执行已缓存的设置请求，避免后台日志首次初始化时直接创建 GameObject。
        /// </summary>
        /// <param name="_">同步上下文回调状态；当前不需要额外参数。</param>
        private static void ApplyPostedSettings(object _)
        {
            if (!IsCurrentMainThread())
            {
                return;
            }

            ApplyRequestedSettings();
        }

        /// <summary>
        /// 在 Unity 主线程应用最近一次设置请求，按需分配或释放覆盖层缓冲和跨场景对象。
        /// </summary>
        private static void ApplyRequestedSettings()
        {
            bool enabled;
            int maxLogCount;
            lock (sSettingsLock)
            {
                enabled = sRequestedEnabled;
                maxLogCount = sRequestedMaxLogCount;
            }

            if (!Application.isPlaying || !enabled)
            {
                sBuffer = null;
                DestroyInstance();
                return;
            }

            UnityLogKitPlayerLogBuffer buffer = sBuffer;
            if (buffer == null)
            {
                buffer = new UnityLogKitPlayerLogBuffer(maxLogCount);
                sBuffer = buffer;
            }
            else
            {
                buffer.SetMaxLogCount(maxLogCount);
            }

            EnsureInstance();
        }

        /// <summary>
        /// 判断当前调用是否位于已捕获的 Unity 主线程；未捕获时只缓存配置，等待场景加载完成。
        /// </summary>
        /// <returns>当前线程可以安全操作 Unity 对象时返回 true。</returns>
        private static bool IsCurrentMainThread()
        {
            return sMainThreadId != 0 && Thread.CurrentThread.ManagedThreadId == sMainThreadId;
        }

        /// <summary>
        /// 归一化日志容量，防止无效配置导致零容量队列的无限出队循环。
        /// </summary>
        /// <param name="maxLogCount">原始最大条数。</param>
        /// <returns>至少为一的安全容量。</returns>
        private static int NormalizeMaxLogCount(int maxLogCount)
        {
            return maxLogCount > 0 ? maxLogCount : 1;
        }

        /// <summary>
        /// 创建唯一的跨场景覆盖层；只有设置已启用且处于 PlayMode 时才会调用。
        /// </summary>
        private static void EnsureInstance()
        {
            if (sInstance != default)
            {
                return;
            }

            GameObject host = new("[YokiFrame LogKit Debug]");
            DontDestroyOnLoad(host);
            sInstance = host.AddComponent<UnityLogKitPlayerOverlay>();
        }

        /// <summary>
        /// 销毁已经存在的覆盖层，避免关闭配置后继续保留 GUI 回调或跨场景对象。
        /// </summary>
        private static void DestroyInstance()
        {
            UnityLogKitPlayerOverlay instance = sInstance;
            sInstance = null;
            if (instance != default)
            {
                UnityEngine.Object.Destroy(instance.gameObject);
            }
        }

        /// <summary>
        /// 登记唯一实例并保证它跨场景存在；异常重复实例会在本帧末尾销毁。
        /// </summary>
        private void Awake()
        {
            if (sInstance != default && sInstance != this)
            {
                Destroy(gameObject);
                return;
            }

            sInstance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 在对象销毁后清理静态引用，避免下一次启用设置复用失效 Unity 对象。
        /// </summary>
        private void OnDestroy()
        {
            if (sInstance == this)
            {
                sInstance = null;
            }
        }

        /// <summary>
        /// 每帧至多把已变更的缓存日志格式化一次，避免 IMGUI 的 Layout/Repaint 重复拼接文本。
        /// </summary>
        private void Update()
        {
            UnityLogKitPlayerLogBuffer buffer = sBuffer;
            if (buffer == null || !buffer.TryBuildText(mTextBuilder, out string text))
            {
                return;
            }

            mCachedText = text;
            mScrollToBottom = true;
        }

        /// <summary>
        /// 使用 Unity IMGUI 绘制可滚动的只读日志视图；绘制阶段只消费已经缓存的字符串。
        /// </summary>
        private void OnGUI()
        {
            if (sBuffer == null)
            {
                return;
            }

            EnsureGuiContent();
            float panelWidth = Mathf.Min(PANEL_MAX_WIDTH, Mathf.Max(1f, Screen.width - PANEL_MARGIN * 2f));
            float panelHeight = Mathf.Min(PANEL_MAX_HEIGHT, Mathf.Max(1f, Screen.height - PANEL_MARGIN * 2f));
            Rect panelRect = new(PANEL_MARGIN, PANEL_MARGIN, panelWidth, panelHeight);
            Rect viewportRect = new(
                panelRect.x + PANEL_PADDING,
                panelRect.y + PANEL_PADDING + 20f,
                Mathf.Max(1f, panelRect.width - PANEL_PADDING * 2f),
                Mathf.Max(1f, panelRect.height - PANEL_PADDING * 2f - 20f));

            GUI.Box(panelRect, "LogKit Debug");
            mContent.text = mCachedText;
            float contentWidth = Mathf.Max(1f, viewportRect.width - SCROLLBAR_WIDTH);
            float contentHeight = Mathf.Max(viewportRect.height, mContentStyle.CalcHeight(mContent, contentWidth));
            if (mScrollToBottom)
            {
                mScrollPosition.y = Mathf.Max(0f, contentHeight - viewportRect.height);
                mScrollToBottom = false;
            }

            Rect contentRect = new(0f, 0f, contentWidth, contentHeight);
            mScrollPosition = GUI.BeginScrollView(viewportRect, mScrollPosition, contentRect);
            GUI.Label(contentRect, mContent, mContentStyle);
            GUI.EndScrollView();
        }

        /// <summary>
        /// 在首个 GUI 事件中初始化样式和内容对象，避免普通 Runtime 日志路径分配 GUI 资源。
        /// </summary>
        private void EnsureGuiContent()
        {
            if (mContentStyle != null)
            {
                return;
            }

            mContent = new GUIContent();
            mContentStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true
            };
        }
    }

    /// <summary>
    /// 持有 Unity Player 调试覆盖层的有界日志缓冲；后台日志线程只会进入此纯 C# 缓冲，不会直接访问 GUI。
    /// </summary>
    internal sealed class UnityLogKitPlayerLogBuffer
    {
        private readonly object mLock = new();
        private readonly Queue<UnityLogKitPlayerLogEntry> mEntries = new();
        private int mMaxLogCount;
        private bool mDirty = true;

        /// <summary>
        /// 创建使用指定最大条数的空缓冲。
        /// </summary>
        /// <param name="maxLogCount">最多保留的日志条数。</param>
        internal UnityLogKitPlayerLogBuffer(int maxLogCount)
        {
            mMaxLogCount = maxLogCount > 0 ? maxLogCount : 1;
        }

        /// <summary>
        /// 更新保留上限，并立即移除最旧的超额日志。
        /// </summary>
        /// <param name="maxLogCount">新的最大条数。</param>
        internal void SetMaxLogCount(int maxLogCount)
        {
            lock (mLock)
            {
                mMaxLogCount = maxLogCount > 0 ? maxLogCount : 1;
                while (mEntries.Count > mMaxLogCount)
                {
                    mEntries.Dequeue();
                }

                mDirty = true;
            }
        }

        /// <summary>
        /// 追加最新日志并在达到容量时丢弃最旧内容；不在写入线程格式化 UI 字符串。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="message">日志正文。</param>
        internal void Record(LogLevel level, string message)
        {
            lock (mLock)
            {
                while (mEntries.Count >= mMaxLogCount)
                {
                    mEntries.Dequeue();
                }

                mEntries.Enqueue(new UnityLogKitPlayerLogEntry(level, message ?? string.Empty));
                mDirty = true;
            }
        }

        /// <summary>
        /// 仅在日志发生变化时重建展示文本，调用方可在 GUI 帧中直接复用返回值。
        /// </summary>
        /// <param name="builder">复用的文本构建器。</param>
        /// <param name="text">成功时返回完整显示文本。</param>
        /// <returns>日志内容发生变化时返回 true。</returns>
        internal bool TryBuildText(StringBuilder builder, out string text)
        {
            lock (mLock)
            {
                if (!mDirty)
                {
                    text = string.Empty;
                    return false;
                }

                builder.Length = 0;
                foreach (UnityLogKitPlayerLogEntry entry in mEntries)
                {
                    builder.Append('[').Append(entry.Level).Append("] ").AppendLine(entry.Message);
                }

                text = builder.Length == 0 ? "No LogKit messages." : builder.ToString();
                mDirty = false;
                return true;
            }
        }
    }

    /// <summary>
    /// 保存一条 Unity Player 覆盖层日志的等级和已格式化文本，避免为每条日志分配额外对象。
    /// </summary>
    internal readonly struct UnityLogKitPlayerLogEntry
    {
        /// <summary>
        /// 创建一条覆盖层日志记录。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="message">日志正文。</param>
        internal UnityLogKitPlayerLogEntry(LogLevel level, string message)
        {
            Level = level;
            Message = message;
        }

        /// <summary>获取日志等级。</summary>
        internal LogLevel Level { get; }

        /// <summary>获取已经由 LogKit 格式化的日志正文。</summary>
        internal string Message { get; }
    }
}
#endif
