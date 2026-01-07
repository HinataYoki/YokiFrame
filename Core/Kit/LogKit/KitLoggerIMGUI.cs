using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// KitLogger IMGUI 日志显示组件
    /// 用于在打包后通过 IMGUI 显示运行时日志，方便调试
    /// </summary>
    public class KitLoggerIMGUI : MonoBehaviour
    {
        #region 配置

        /// <summary>
        /// 最大显示日志条数
        /// </summary>
        public int MaxLogCount = 200;

        /// <summary>
        /// 是否显示日志窗口
        /// </summary>
        public bool ShowWindow = true;

        /// <summary>
        /// 是否显示时间戳
        /// </summary>
        public bool ShowTimestamp = true;

        /// <summary>
        /// 是否自动滚动到底部
        /// </summary>
        public bool AutoScroll = true;

        /// <summary>
        /// 日志过滤级别
        /// </summary>
        public LogTypeFilter Filter = LogTypeFilter.All;

        /// <summary>
        /// 窗口透明度
        /// </summary>
        [Range(0.5f, 1f)]
        public float WindowAlpha = 0.9f;

        /// <summary>
        /// 触发显示/隐藏的手指数量（移动端）
        /// </summary>
        public int ToggleTouchCount = 3;

        /// <summary>
        /// 触发显示/隐藏的按键（PC端）
        /// </summary>
        public KeyCode ToggleKey = KeyCode.BackQuote;

        #endregion

        #region 日志类型过滤枚举

        [Flags]
        public enum LogTypeFilter
        {
            None = 0,
            Log = 1 << 0,
            Warning = 1 << 1,
            Error = 1 << 2,
            Exception = 1 << 3,
            Assert = 1 << 4,
            All = Log | Warning | Error | Exception | Assert
        }

        #endregion

        #region 内部数据结构

        private struct LogEntry
        {
            public string Message;
            public string StackTrace;
            public LogType Type;
            public DateTime Time;
            public int Count; // 重复计数
        }

        #endregion

        #region 私有字段

        private readonly List<LogEntry> mLogEntries = new(256);
        private readonly object mLock = new();
        private Vector2 mScrollPosition;
        private Rect mWindowRect;
        private bool mIsCollapsed = true;
        private string mLastMessage;
        private int mLastMessageIndex = -1;

        // 统计
        private int mLogCount;
        private int mWarningCount;
        private int mErrorCount;

        // GUI 样式缓存
        private GUIStyle mLogStyle;
        private GUIStyle mWarningStyle;
        private GUIStyle mErrorStyle;
        private GUIStyle mToolbarStyle;
        private GUIStyle mBoxStyle;
        private bool mStylesInitialized;

        // 窗口拖拽
        private bool mIsDragging;
        private Vector2 mDragOffset;

        // 单例
        private static KitLoggerIMGUI sInstance;

        #endregion

        #region 公开接口

        /// <summary>
        /// 启用 IMGUI 日志显示
        /// </summary>
        /// <param name="maxLogCount">最大日志条数</param>
        public static KitLoggerIMGUI Enable(int maxLogCount = 200)
        {
            if (sInstance != null) return sInstance;

            var go = new GameObject("[KitLoggerIMGUI]");
            DontDestroyOnLoad(go);
            sInstance = go.AddComponent<KitLoggerIMGUI>();
            sInstance.MaxLogCount = maxLogCount;
            return sInstance;
        }

        /// <summary>
        /// 禁用 IMGUI 日志显示
        /// </summary>
        public static void Disable()
        {
            if (sInstance != null)
            {
                Destroy(sInstance.gameObject);
                sInstance = null;
            }
        }

        /// <summary>
        /// 获取实例
        /// </summary>
        public static KitLoggerIMGUI Instance => sInstance;

        /// <summary>
        /// 清空日志
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
            }
        }

        /// <summary>
        /// 切换窗口显示/隐藏
        /// </summary>
        public void ToggleWindow()
        {
            ShowWindow = !ShowWindow;
        }

        #endregion

        #region Unity 生命周期

        private void Awake()
        {
            if (sInstance != null && sInstance != this)
            {
                Destroy(gameObject);
                return;
            }
            sInstance = this;

            // 初始化窗口位置
            float width = Mathf.Min(Screen.width * 0.9f, 800);
            float height = Mathf.Min(Screen.height * 0.6f, 400);
            mWindowRect = new Rect(
                (Screen.width - width) / 2,
                (Screen.height - height) / 2,
                width,
                height
            );
        }

        private void OnEnable()
        {
            Application.logMessageReceivedThreaded += HandleLog;
        }

        private void OnDisable()
        {
            Application.logMessageReceivedThreaded -= HandleLog;
        }

        private void OnDestroy()
        {
            if (sInstance == this)
            {
                sInstance = null;
            }
        }

        private void Update()
        {
            // PC 端按键切换
            if (Input.GetKeyDown(ToggleKey))
            {
                ToggleWindow();
            }

            // 移动端多指触摸切换
            if (Input.touchCount == ToggleTouchCount)
            {
                bool allBegan = true;
                for (int i = 0; i < ToggleTouchCount; i++)
                {
                    if (Input.GetTouch(i).phase != TouchPhase.Began)
                    {
                        allBegan = false;
                        break;
                    }
                }
                if (allBegan)
                {
                    ToggleWindow();
                }
            }
        }

        #endregion

        #region 日志处理

        private void HandleLog(string message, string stackTrace, LogType type)
        {
            lock (mLock)
            {
                // 合并重复日志
                if (mIsCollapsed && message == mLastMessage && mLastMessageIndex >= 0)
                {
                    var entry = mLogEntries[mLastMessageIndex];
                    entry.Count++;
                    mLogEntries[mLastMessageIndex] = entry;
                    return;
                }

                // 添加新日志
                var newEntry = new LogEntry
                {
                    Message = message,
                    StackTrace = stackTrace,
                    Type = type,
                    Time = DateTime.Now,
                    Count = 1
                };

                mLogEntries.Add(newEntry);
                mLastMessage = message;
                mLastMessageIndex = mLogEntries.Count - 1;

                // 更新统计
                switch (type)
                {
                    case LogType.Log:
                        mLogCount++;
                        break;
                    case LogType.Warning:
                        mWarningCount++;
                        break;
                    case LogType.Error:
                    case LogType.Exception:
                    case LogType.Assert:
                        mErrorCount++;
                        break;
                }

                // 限制日志数量
                while (mLogEntries.Count > MaxLogCount)
                {
                    var removed = mLogEntries[0];
                    mLogEntries.RemoveAt(0);
                    mLastMessageIndex--;

                    // 更新统计
                    switch (removed.Type)
                    {
                        case LogType.Log:
                            mLogCount = Math.Max(0, mLogCount - removed.Count);
                            break;
                        case LogType.Warning:
                            mWarningCount = Math.Max(0, mWarningCount - removed.Count);
                            break;
                        case LogType.Error:
                        case LogType.Exception:
                        case LogType.Assert:
                            mErrorCount = Math.Max(0, mErrorCount - removed.Count);
                            break;
                    }
                }
            }
        }

        private bool ShouldShowLog(LogType type)
        {
            return type switch
            {
                LogType.Log => (Filter & LogTypeFilter.Log) != 0,
                LogType.Warning => (Filter & LogTypeFilter.Warning) != 0,
                LogType.Error => (Filter & LogTypeFilter.Error) != 0,
                LogType.Exception => (Filter & LogTypeFilter.Exception) != 0,
                LogType.Assert => (Filter & LogTypeFilter.Assert) != 0,
                _ => true
            };
        }

        #endregion

        #region IMGUI 渲染

        private void OnGUI()
        {
            if (!ShowWindow) return;

            InitStyles();

            // 设置窗口透明度
            GUI.color = new Color(1, 1, 1, WindowAlpha);

            mWindowRect = GUILayout.Window(
                GetHashCode(),
                mWindowRect,
                DrawWindow,
                "KitLogger Console",
                GUILayout.MinWidth(300),
                GUILayout.MinHeight(200)
            );

            // 限制窗口在屏幕内
            mWindowRect.x = Mathf.Clamp(mWindowRect.x, 0, Screen.width - mWindowRect.width);
            mWindowRect.y = Mathf.Clamp(mWindowRect.y, 0, Screen.height - mWindowRect.height);

            GUI.color = Color.white;
        }

        private void InitStyles()
        {
            if (mStylesInitialized) return;
            mStylesInitialized = true;

            mLogStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                richText = true,
                normal = { textColor = Color.white }
            };

            mWarningStyle = new GUIStyle(mLogStyle)
            {
                normal = { textColor = new Color(1f, 0.9f, 0.3f) }
            };

            mErrorStyle = new GUIStyle(mLogStyle)
            {
                normal = { textColor = new Color(1f, 0.4f, 0.4f) }
            };

            mToolbarStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fixedHeight = 25
            };

            mBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(5, 5, 5, 5)
            };
        }

        private void DrawWindow(int windowId)
        {
            // 工具栏
            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Clear", mToolbarStyle, GUILayout.Width(60)))
                {
                    ClearLogs();
                }

                mIsCollapsed = GUILayout.Toggle(mIsCollapsed, "Collapse", mToolbarStyle, GUILayout.Width(70));
                AutoScroll = GUILayout.Toggle(AutoScroll, "AutoScroll", mToolbarStyle, GUILayout.Width(80));
                ShowTimestamp = GUILayout.Toggle(ShowTimestamp, "Time", mToolbarStyle, GUILayout.Width(50));

                GUILayout.FlexibleSpace();

                // 过滤按钮
                DrawFilterToggle(LogTypeFilter.Log, $"Log [{mLogCount}]", Color.white);
                DrawFilterToggle(LogTypeFilter.Warning, $"Warn [{mWarningCount}]", new Color(1f, 0.9f, 0.3f));
                DrawFilterToggle(LogTypeFilter.Error, $"Error [{mErrorCount}]", new Color(1f, 0.4f, 0.4f));

                if (GUILayout.Button("X", mToolbarStyle, GUILayout.Width(25)))
                {
                    ShowWindow = false;
                }
            }
            GUILayout.EndHorizontal();

            // 日志列表
            mScrollPosition = GUILayout.BeginScrollView(mScrollPosition, mBoxStyle);
            {
                lock (mLock)
                {
                    for (int i = 0; i < mLogEntries.Count; i++)
                    {
                        var entry = mLogEntries[i];
                        if (!ShouldShowLog(entry.Type)) continue;

                        DrawLogEntry(ref entry);
                    }
                }

                // 自动滚动
                if (AutoScroll)
                {
                    mScrollPosition.y = float.MaxValue;
                }
            }
            GUILayout.EndScrollView();

            // 窗口拖拽
            GUI.DragWindow(new Rect(0, 0, mWindowRect.width, 20));
        }

        private void DrawFilterToggle(LogTypeFilter filterType, string label, Color color)
        {
            bool isEnabled = (Filter & filterType) != 0;
            GUI.color = isEnabled ? color : Color.gray;
            if (GUILayout.Button(label, mToolbarStyle, GUILayout.Width(80)))
            {
                if (isEnabled)
                    Filter &= ~filterType;
                else
                    Filter |= filterType;
            }
            GUI.color = new Color(1, 1, 1, WindowAlpha);
        }

        private void DrawLogEntry(ref LogEntry entry)
        {
            GUIStyle style = entry.Type switch
            {
                LogType.Warning => mWarningStyle,
                LogType.Error or LogType.Exception or LogType.Assert => mErrorStyle,
                _ => mLogStyle
            };

            string prefix = "";
            if (ShowTimestamp)
            {
                prefix = $"[{entry.Time:HH:mm:ss}] ";
            }

            string countSuffix = entry.Count > 1 ? $" <color=#888888>x{entry.Count}</color>" : "";
            string message = $"{prefix}{entry.Message}{countSuffix}";

            GUILayout.Label(message, style);
        }

        #endregion
    }
}
