#if !GODOT
using System;
using System.Collections.Generic;
using UnityEngine;
#if YOKIFRAME_ZSTRING_SUPPORT
using Cysharp.Text;
#endif

namespace YokiFrame.Unity
{
    /// <summary>
    /// Player 调试用 IMGUI 日志面板。它只在 Unity Adapter 中存在，不进入 Base 层。
    /// </summary>
    public sealed class UnityLogKitIMGUIConsole : MonoBehaviour
    {
        /// <summary>
        /// IMGUI 日志面板的日志类型过滤位。
        /// </summary>
        [Flags]
        public enum LogTypeFilter
        {
            /// <summary>
            /// 不显示任何日志类型。
            /// </summary>
            None = 0,

            /// <summary>
            /// 显示普通日志。
            /// </summary>
            Log = 1 << 0,

            /// <summary>
            /// 显示警告日志。
            /// </summary>
            Warning = 1 << 1,

            /// <summary>
            /// 显示错误日志。
            /// </summary>
            Error = 1 << 2,

            /// <summary>
            /// 显示异常日志。
            /// </summary>
            Exception = 1 << 3,

            /// <summary>
            /// 显示断言日志。
            /// </summary>
            Assert = 1 << 4,

            /// <summary>
            /// 显示所有日志类型。
            /// </summary>
            All = Log | Warning | Error | Exception | Assert
        }

        private struct LogEntry
        {
            public string Message;
            public string StackTrace;
            public LogType Type;
            public DateTime Time;
            public int Count;
        }

        /// <summary>
        /// 面板最多保留的日志条数。
        /// </summary>
        public int MaxLogCount = 200;

        /// <summary>
        /// 当前是否显示日志窗口。
        /// </summary>
        public bool ShowWindow = true;

        /// <summary>
        /// 当前是否显示日志时间戳。
        /// </summary>
        public bool ShowTimestamp = true;

        /// <summary>
        /// 当前是否自动滚动到最新日志。
        /// </summary>
        public bool AutoScroll = true;

        /// <summary>
        /// 当前日志类型过滤器。
        /// </summary>
        public LogTypeFilter Filter = LogTypeFilter.All;

        /// <summary>
        /// 日志窗口透明度。
        /// </summary>
        [Range(0.5f, 1f)] public float WindowAlpha = 0.9f;

        /// <summary>
        /// 触发窗口显示切换所需的同时触摸数量。
        /// </summary>
        public int ToggleTouchCount = 3;

        /// <summary>
        /// 触发窗口显示切换的键盘按键。
        /// </summary>
        public KeyCode ToggleKey = KeyCode.BackQuote;

        private readonly List<LogEntry> mLogEntries = new List<LogEntry>(256);
        private readonly object mLock = new object();
        private Vector2 mScrollPosition;
        private Rect mWindowRect;
        private bool mCollapseRepeated = true;
        private string mLastMessage;
        private int mLastMessageIndex = -1;
        private int mLogCount;
        private int mWarningCount;
        private int mErrorCount;
        private GUIStyle mLogStyle;
        private GUIStyle mWarningStyle;
        private GUIStyle mErrorStyle;
        private GUIStyle mToolbarStyle;
        private GUIStyle mBoxStyle;
        private bool mStylesInitialized;

        // 缓存的计数标签字符串，仅在计数变化时更新
        private string mLogCountLabel = "Log [0]";
        private string mWarningCountLabel = "Warn [0]";
        private string mErrorCountLabel = "Error [0]";
        private int mCachedLogCount;
        private int mCachedWarningCount;
        private int mCachedErrorCount;

        // 缓存 OnGUI 用 Color（避免 struct 每帧构造 + 拼接上下文）
        private Color mWindowColor;

        private static UnityLogKitIMGUIConsole sInstance;

        /// <summary>
        /// 获取当前 IMGUI 日志面板实例。
        /// </summary>
        public static UnityLogKitIMGUIConsole Instance
        {
            get { return sInstance; }
        }

        /// <summary>
        /// 启用 IMGUI 日志面板。
        /// </summary>
        /// <param name="maxLogCount">面板最多保留的日志条数。</param>
        /// <returns>启用后的面板实例。</returns>
        public static UnityLogKitIMGUIConsole Enable(int maxLogCount = 200)
        {
            if (sInstance != null)
                return sInstance;

            var go = new GameObject("[UnityLogKitIMGUIConsole]");
            if (Application.isPlaying)
                DontDestroyOnLoad(go);
            sInstance = go.AddComponent<UnityLogKitIMGUIConsole>();
            sInstance.MaxLogCount = maxLogCount > 0 ? maxLogCount : 200;
            return sInstance;
        }

        /// <summary>
        /// 禁用并销毁 IMGUI 日志面板。
        /// </summary>
        public static void Disable()
        {
            if (sInstance == null)
                return;

            var instance = sInstance;
            sInstance = null;
            if (instance.gameObject != null)
            {
                if (Application.isPlaying)
                    Destroy(instance.gameObject);
                else
                    DestroyImmediate(instance.gameObject);
            }
        }

        /// <summary>
        /// 清空当前面板中缓存的日志。
        /// </summary>
        public void ClearLogs()
        {
            lock (mLock)
            {
                mLogEntries.Clear();
                mLogCount = 0;
                mWarningCount = 0;
                mErrorCount = 0;
                mLastMessage = null;
                mLastMessageIndex = -1;
                mLogCountLabel = "Log [0]";
                mWarningCountLabel = "Warn [0]";
                mErrorCountLabel = "Error [0]";
                mCachedLogCount = 0;
                mCachedWarningCount = 0;
                mCachedErrorCount = 0;
            }
        }

        /// <summary>
        /// 切换日志窗口显示状态。
        /// </summary>
        public void ToggleWindow()
        {
            ShowWindow = !ShowWindow;
        }

        private void Awake()
        {
            if (sInstance != null && sInstance != this)
            {
                if (Application.isPlaying)
                    Destroy(gameObject);
                else
                    DestroyImmediate(gameObject);
                return;
            }

            sInstance = this;
            mWindowColor = new Color(1f, 1f, 1f, WindowAlpha);
            var width = Mathf.Min(Screen.width * 0.9f, 800f);
            var height = Mathf.Min(Screen.height * 0.6f, 400f);
            mWindowRect = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);
        }

        private void OnEnable()
        {
            Application.logMessageReceivedThreaded -= HandleLog;
            Application.logMessageReceivedThreaded += HandleLog;
        }

        private void OnDisable()
        {
            Application.logMessageReceivedThreaded -= HandleLog;
        }

        private void OnDestroy()
        {
            if (sInstance == this)
                sInstance = null;
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
                ToggleWindow();

            if (ToggleTouchCount <= 0 || Input.touchCount != ToggleTouchCount)
                return;

            var allBegan = true;
            for (var i = 0; i < ToggleTouchCount; i++)
            {
                if (Input.GetTouch(i).phase != TouchPhase.Began)
                {
                    allBegan = false;
                    break;
                }
            }

            if (allBegan)
                ToggleWindow();
        }

        private void OnGUI()
        {
            if (!ShowWindow)
                return;

            InitStyles();
            mWindowColor.a = WindowAlpha;
            GUI.color = mWindowColor;
            mWindowRect = GUILayout.Window(
                GetHashCode(),
                mWindowRect,
                DrawWindow,
                "LogKit Console",
                GUILayout.MinWidth(300f),
                GUILayout.MinHeight(200f));

            mWindowRect.x = Mathf.Clamp(mWindowRect.x, 0f, Screen.width - mWindowRect.width);
            mWindowRect.y = Mathf.Clamp(mWindowRect.y, 0f, Screen.height - mWindowRect.height);
            GUI.color = Color.white;
        }

        private void HandleLog(string message, string stackTrace, LogType type)
        {
            lock (mLock)
            {
                if (mCollapseRepeated && string.Equals(message, mLastMessage, StringComparison.Ordinal) && mLastMessageIndex >= 0)
                {
                    var entry = mLogEntries[mLastMessageIndex];
                    entry.Count++;
                    mLogEntries[mLastMessageIndex] = entry;
                    IncrementCount(type, 1);
                    return;
                }

                var newEntry = new LogEntry
                {
                    Message = message ?? string.Empty,
                    StackTrace = stackTrace ?? string.Empty,
                    Type = type,
                    Time = DateTime.Now,
                    Count = 1
                };

                mLogEntries.Add(newEntry);
                mLastMessage = message;
                mLastMessageIndex = mLogEntries.Count - 1;
                IncrementCount(type, 1);
                TrimOldEntries();
            }
        }

        private void TrimOldEntries()
        {
            while (mLogEntries.Count > MaxLogCount)
            {
                var removed = mLogEntries[0];
                mLogEntries.RemoveAt(0);
                mLastMessageIndex--;
                IncrementCount(removed.Type, -removed.Count);
            }
        }

        private void IncrementCount(LogType type, int delta)
        {
            switch (type)
            {
                case LogType.Log:
                    mLogCount = Mathf.Max(0, mLogCount + delta);
                    break;
                case LogType.Warning:
                    mWarningCount = Mathf.Max(0, mWarningCount + delta);
                    break;
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    mErrorCount = Mathf.Max(0, mErrorCount + delta);
                    break;
            }
            RefreshCountLabels();
        }

        private void RefreshCountLabels()
        {
            if (mCachedLogCount != mLogCount)
            {
                mCachedLogCount = mLogCount;
                mLogCountLabel = "Log [" + mLogCount + "]";
            }
            if (mCachedWarningCount != mWarningCount)
            {
                mCachedWarningCount = mWarningCount;
                mWarningCountLabel = "Warn [" + mWarningCount + "]";
            }
            if (mCachedErrorCount != mErrorCount)
            {
                mCachedErrorCount = mErrorCount;
                mErrorCountLabel = "Error [" + mErrorCount + "]";
            }
        }

        private void InitStyles()
        {
            if (mStylesInitialized)
                return;

            mStylesInitialized = true;
            mLogStyle = new GUIStyle(GUI.skin.label);
            mLogStyle.fontSize = 12;
            mLogStyle.wordWrap = true;
            mLogStyle.richText = true;
            mLogStyle.normal.textColor = Color.white;

            mWarningStyle = new GUIStyle(mLogStyle);
            mWarningStyle.normal.textColor = new Color(1f, 0.9f, 0.3f);

            mErrorStyle = new GUIStyle(mLogStyle);
            mErrorStyle.normal.textColor = new Color(1f, 0.4f, 0.4f);

            mToolbarStyle = new GUIStyle(GUI.skin.button);
            mToolbarStyle.fontSize = 11;
            mToolbarStyle.fixedHeight = 25f;

            mBoxStyle = new GUIStyle(GUI.skin.box);
            mBoxStyle.padding = new RectOffset(5, 5, 5, 5);
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear", mToolbarStyle, GUILayout.Width(60f)))
                ClearLogs();
            mCollapseRepeated = GUILayout.Toggle(mCollapseRepeated, "Collapse", mToolbarStyle, GUILayout.Width(76f));
            AutoScroll = GUILayout.Toggle(AutoScroll, "Auto", mToolbarStyle, GUILayout.Width(54f));
            ShowTimestamp = GUILayout.Toggle(ShowTimestamp, "Time", mToolbarStyle, GUILayout.Width(54f));
            GUILayout.FlexibleSpace();
            DrawFilterToggle(LogTypeFilter.Log, mLogCountLabel, Color.white);
            DrawFilterToggle(LogTypeFilter.Warning, mWarningCountLabel, new Color(1f, 0.9f, 0.3f));
            DrawFilterToggle(LogTypeFilter.Error, mErrorCountLabel, new Color(1f, 0.4f, 0.4f));
            if (GUILayout.Button("X", mToolbarStyle, GUILayout.Width(25f)))
                ShowWindow = false;
            GUILayout.EndHorizontal();

            mScrollPosition = GUILayout.BeginScrollView(mScrollPosition, mBoxStyle);
            lock (mLock)
            {
                for (var i = 0; i < mLogEntries.Count; i++)
                {
                    var entry = mLogEntries[i];
                    if (ShouldShowLog(entry.Type))
                        DrawLogEntry(ref entry);
                }
            }

            if (AutoScroll)
                mScrollPosition.y = float.MaxValue;
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, mWindowRect.width, 20f));
        }

        private void DrawFilterToggle(LogTypeFilter filterType, string label, Color color)
        {
            var isEnabled = (Filter & filterType) != 0;
            GUI.color = isEnabled ? color : Color.gray;
            if (GUILayout.Button(label, mToolbarStyle, GUILayout.Width(86f)))
            {
                if (isEnabled)
                    Filter &= ~filterType;
                else
                    Filter |= filterType;
            }
            mWindowColor.a = WindowAlpha;
            GUI.color = mWindowColor;
        }

        private void DrawLogEntry(ref LogEntry entry)
        {
            var style = mLogStyle;
            if (entry.Type == LogType.Warning)
                style = mWarningStyle;
            else if (entry.Type == LogType.Error || entry.Type == LogType.Exception || entry.Type == LogType.Assert)
                style = mErrorStyle;

            // 无时间戳且无重复计数时，直接显示原始消息，零分配
            if (!ShowTimestamp && entry.Count <= 1)
            {
                GUILayout.Label(entry.Message, style);
                return;
            }

#if YOKIFRAME_ZSTRING_SUPPORT
            using (var sb = ZString.CreateStringBuilder())
            {
                if (ShowTimestamp)
                {
                    sb.Append('[');
                    sb.Append(entry.Time.ToString("HH:mm:ss"));
                    sb.Append("] ");
                }
                sb.Append(entry.Message);
                if (entry.Count > 1)
                {
                    sb.Append(" <color=#888888>x");
                    sb.Append(entry.Count);
                    sb.Append("</color>");
                }
                GUILayout.Label(sb.ToString(), style);
            }
#else
            var prefix = ShowTimestamp ? "[" + entry.Time.ToString("HH:mm:ss") + "] " : string.Empty;
            var countSuffix = entry.Count > 1 ? " <color=#888888>x" + entry.Count + "</color>" : string.Empty;
            GUILayout.Label(prefix + entry.Message + countSuffix, style);
#endif
        }

        private bool ShouldShowLog(LogType type)
        {
            switch (type)
            {
                case LogType.Log:
                    return (Filter & LogTypeFilter.Log) != 0;
                case LogType.Warning:
                    return (Filter & LogTypeFilter.Warning) != 0;
                case LogType.Error:
                    return (Filter & LogTypeFilter.Error) != 0;
                case LogType.Exception:
                    return (Filter & LogTypeFilter.Exception) != 0;
                case LogType.Assert:
                    return (Filter & LogTypeFilter.Assert) != 0;
                default:
                    return true;
            }
        }
    }
}
#endif
