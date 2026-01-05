using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// FsmKit 状态机可视化查看器
    /// </summary>
    public class FsmKitViewerWindow : EditorWindow
    {
        private const string WINDOW_TITLE = "FsmKit Viewer";
        private const float REFRESH_INTERVAL = 0.2f;

        private enum ViewMode { Runtime, History }

        private ViewMode mViewMode = ViewMode.Runtime;
        private Vector2 mLeftScrollPos;
        private Vector2 mRightScrollPos;
        private Vector2 mHistoryScrollPos;
        private double mLastRefreshTime;
        private bool mClearHistoryOnStop = true;
        private bool mHistoryAutoScroll = true;
        private string mHistoryFilterAction = "All";

        // 运行时数据缓存
        private readonly List<IFSM> mCachedFsms = new(16);
        private IFSM mSelectedFsm;

        [MenuItem("YokiFrame/FsmKit/FSM Viewer")]
        private static void Open()
        {
            var window = GetWindow<FsmKitViewerWindow>(false, WINDOW_TITLE);
            window.minSize = new Vector2(800, 450);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            mClearHistoryOnStop = EditorPrefs.GetBool("FsmKitViewer_ClearHistoryOnStop", true);
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorPrefs.SetBool("FsmKitViewer_ClearHistoryOnStop", mClearHistoryOnStop);
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                mCachedFsms.Clear();
                mSelectedFsm = null;
            }
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying)
                Repaint();
        }

        private void OnGUI()
        {
            DrawMainToolbar();

            switch (mViewMode)
            {
                case ViewMode.Runtime:
                    DrawRuntimeView();
                    break;
                case ViewMode.History:
                    DrawHistoryView();
                    break;
            }
        }

        private void DrawMainToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Toggle(mViewMode == ViewMode.Runtime, "运行时监控", EditorStyles.toolbarButton, GUILayout.Width(80)))
                mViewMode = ViewMode.Runtime;

            if (GUILayout.Toggle(mViewMode == ViewMode.History, "转换历史", EditorStyles.toolbarButton, GUILayout.Width(70)))
                mViewMode = ViewMode.History;

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        #region Runtime View

        private void DrawRuntimeView()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.Space(50);
                EditorGUILayout.LabelField("请进入 Play Mode 查看运行时状态机",
                    new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 14 },
                    GUILayout.ExpandHeight(true));
                return;
            }

            if (EditorApplication.timeSinceStartup - mLastRefreshTime > REFRESH_INTERVAL)
            {
                FsmDebugger.GetActiveFsms(mCachedFsms);
                mLastRefreshTime = EditorApplication.timeSinceStartup;
            }

            EditorGUILayout.BeginHorizontal();
            DrawFsmListPanel();
            DrawFsmDetailPanel();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFsmListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(250));
            EditorGUILayout.LabelField($"活跃状态机 ({mCachedFsms.Count})", EditorStyles.boldLabel);

            mLeftScrollPos = EditorGUILayout.BeginScrollView(mLeftScrollPos, "box", GUILayout.ExpandHeight(true));

            if (mCachedFsms.Count == 0)
            {
                EditorGUILayout.LabelField("暂无活跃状态机", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                foreach (var fsm in mCachedFsms)
                {
                    DrawFsmListItem(fsm);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawFsmListItem(IFSM fsm)
        {
            var isSelected = mSelectedFsm == fsm;
            var rect = EditorGUILayout.BeginHorizontal();

            if (isSelected)
                EditorGUI.DrawRect(rect, new Color(0.24f, 0.49f, 0.91f, 0.5f));

            // 状态指示
            var stateColor = fsm.MachineState switch
            {
                MachineState.Running => Color.green,
                MachineState.Suspend => Color.yellow,
                _ => Color.gray
            };
            var oldColor = GUI.color;
            GUI.color = stateColor;
            EditorGUILayout.LabelField("●", GUILayout.Width(15));
            GUI.color = oldColor;

            if (GUILayout.Button(fsm.Name, EditorStyles.label))
                mSelectedFsm = fsm;

            var stateCount = fsm.GetAllStates().Count;
            EditorGUILayout.LabelField($"[{stateCount}]", GUILayout.Width(30));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFsmDetailPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            if (mSelectedFsm == null)
            {
                EditorGUILayout.LabelField("状态机详情", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("选择左侧状态机查看详情", MessageType.Info);
            }
            else
            {
                DrawFsmDetail(mSelectedFsm);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawFsmDetail(IFSM fsm)
        {
            EditorGUILayout.LabelField($"状态机: {fsm.Name}", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // 基本信息
            EditorGUILayout.BeginVertical("helpBox");
            EditorGUILayout.LabelField($"枚举类型: {fsm.EnumType.Name}");
            EditorGUILayout.LabelField($"机器状态: {fsm.MachineState}");
            
            var currentStateName = fsm.CurrentStateId >= 0 
                ? Enum.GetName(fsm.EnumType, fsm.CurrentStateId) ?? fsm.CurrentStateId.ToString()
                : "None";
            EditorGUILayout.LabelField($"当前状态: {currentStateName}", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // 状态列表
            EditorGUILayout.LabelField("注册状态:", EditorStyles.boldLabel);
            mRightScrollPos = EditorGUILayout.BeginScrollView(mRightScrollPos, "box", GUILayout.ExpandHeight(true));

            var states = fsm.GetAllStates();
            var currentId = fsm.CurrentStateId;

            foreach (var kvp in states)
            {
                var isCurrent = kvp.Key == currentId;
                DrawStateItem(fsm.EnumType, kvp.Key, kvp.Value, isCurrent);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawStateItem(Type enumType, int stateId, IState state, bool isCurrent)
        {
            EditorGUILayout.BeginHorizontal("helpBox");

            // 当前状态高亮
            var oldBg = GUI.backgroundColor;
            if (isCurrent)
                GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);

            var stateName = Enum.GetName(enumType, stateId) ?? stateId.ToString();
            
            if (isCurrent)
                GUILayout.Label("▶", GUILayout.Width(15));
            else
                GUILayout.Space(18);

            EditorGUILayout.LabelField(stateName, isCurrent ? EditorStyles.boldLabel : EditorStyles.label, GUILayout.Width(120));
            EditorGUILayout.LabelField(state.GetType().Name, EditorStyles.miniLabel);

            GUI.backgroundColor = oldBg;
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region History View

        private void DrawHistoryView()
        {
            DrawHistoryToolbar();
            DrawHistoryList();
        }

        private void DrawHistoryToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUILayout.LabelField("操作:", GUILayout.Width(35));
            mHistoryFilterAction = DrawFilterDropdown(mHistoryFilterAction, 
                new[] { "All", "Start", "Change", "Add", "Remove", "Clear", "Dispose" });

            GUILayout.Space(10);
            
            var recordTransitions = GUILayout.Toggle(FsmDebugger.RecordTransitions, "记录转换", EditorStyles.toolbarButton, GUILayout.Width(70));
            if (recordTransitions != FsmDebugger.RecordTransitions)
                FsmDebugger.RecordTransitions = recordTransitions;

            mHistoryAutoScroll = GUILayout.Toggle(mHistoryAutoScroll, "自动滚动", EditorStyles.toolbarButton, GUILayout.Width(70));

            var newClearOnStop = GUILayout.Toggle(mClearHistoryOnStop, "停止时清空", EditorStyles.toolbarButton, GUILayout.Width(75));
            if (newClearOnStop != mClearHistoryOnStop)
            {
                mClearHistoryOnStop = newClearOnStop;
                EditorPrefs.SetBool("FsmKitViewer_ClearHistoryOnStop", mClearHistoryOnStop);
            }

            GUILayout.FlexibleSpace();

            var history = FsmDebugger.TransitionHistory;
            EditorGUILayout.LabelField($"记录: {history.Count}/{FsmDebugger.MAX_HISTORY_COUNT}", GUILayout.Width(100));

            if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(45)))
                FsmDebugger.ClearHistory();

            EditorGUILayout.EndHorizontal();
        }

        private string DrawFilterDropdown(string current, string[] options)
        {
            var index = Array.IndexOf(options, current);
            if (index < 0) index = 0;
            return options[EditorGUILayout.Popup(index, options, EditorStyles.toolbarPopup, GUILayout.Width(70))];
        }

        private void DrawHistoryList()
        {
            var history = FsmDebugger.TransitionHistory;

            mHistoryScrollPos = EditorGUILayout.BeginScrollView(mHistoryScrollPos, "box", GUILayout.ExpandHeight(true));

            if (history.Count == 0)
            {
                EditorGUILayout.HelpBox("暂无转换记录，状态机运行后将在此显示", MessageType.Info);
            }
            else
            {
                for (var i = history.Count - 1; i >= 0; i--)
                {
                    var entry = history[i];
                    if (mHistoryFilterAction != "All" && entry.Action != mHistoryFilterAction)
                        continue;
                    DrawHistoryEntry(entry);
                }
            }

            EditorGUILayout.EndScrollView();

            if (mHistoryAutoScroll && Event.current.type == EventType.Repaint)
            {
                var lastCount = EditorPrefs.GetInt("FsmKitViewer_LastHistoryCount", 0);
                if (history.Count > lastCount)
                {
                    mHistoryScrollPos = Vector2.zero;
                    EditorPrefs.SetInt("FsmKitViewer_LastHistoryCount", history.Count);
                }
            }
        }

        private void DrawHistoryEntry(FsmDebugger.TransitionEntry entry)
        {
            EditorGUILayout.BeginHorizontal("helpBox");

            // 时间
            EditorGUILayout.LabelField($"{entry.Time:F2}s", GUILayout.Width(55));

            // 操作类型颜色
            var actionColor = entry.Action switch
            {
                "Start" => new Color(0.5f, 1f, 0.5f),
                "Change" => new Color(0.5f, 0.8f, 1f),
                "Add" => new Color(0.8f, 1f, 0.8f),
                "Remove" => new Color(1f, 0.7f, 0.7f),
                "Clear" => new Color(1f, 0.8f, 0.4f),
                "Dispose" => new Color(0.7f, 0.7f, 0.7f),
                _ => Color.white
            };

            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = actionColor;
            GUILayout.Label(entry.Action, "CN CountBadge", GUILayout.Width(55));
            GUI.backgroundColor = oldBg;

            // FSM 名称
            EditorGUILayout.LabelField(entry.FsmName, EditorStyles.boldLabel, GUILayout.Width(150));

            // 状态转换
            if (entry.Action == "Change")
            {
                EditorGUILayout.LabelField($"{entry.FromState} → {entry.ToState}", GUILayout.Width(200));
            }
            else if (!string.IsNullOrEmpty(entry.ToState))
            {
                EditorGUILayout.LabelField(entry.ToState, GUILayout.Width(200));
            }
            else if (!string.IsNullOrEmpty(entry.FromState))
            {
                EditorGUILayout.LabelField(entry.FromState, GUILayout.Width(200));
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion
    }
}
