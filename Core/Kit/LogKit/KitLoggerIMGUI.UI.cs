using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// KitLogger IMGUI 日志显示组件 - UI 渲染
    /// </summary>
    public partial class KitLoggerIMGUI
    {
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
