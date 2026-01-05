using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Action 监控记录（编辑器专用）
    /// </summary>
    public readonly struct ActionMonitorRecord
    {
        public readonly ulong ActionId;
        public readonly string ActionType;
        public readonly string DebugInfo;
        public readonly ActionStatus Status;
        public readonly float StartTime;
        public readonly string ExecutorName;

        public ActionMonitorRecord(ulong actionId, string actionType, string debugInfo, 
            ActionStatus status, float startTime, string executorName)
        {
            ActionId = actionId;
            ActionType = actionType;
            DebugInfo = debugInfo;
            Status = status;
            StartTime = startTime;
            ExecutorName = executorName;
        }
    }

    /// <summary>
    /// ActionKit 运行时监控服务（编辑器专用）
    /// 通过反射访问运行时数据，对运行时零影响
    /// </summary>
    public static class ActionMonitorService
    {
        // 缓存的反射信息
        private static Type sSequenceType;
        private static Type sParallelType;
        private static Type sRepeatType;
        private static MethodInfo sSequenceGetActions;
        private static MethodInfo sSequenceGetIndex;
        private static MethodInfo sParallelGetActions;
        private static MethodInfo sRepeatGetSequence;
        
        private static bool sReflectionInitialized;

        /// <summary>
        /// 初始化反射缓存
        /// </summary>
        private static void InitReflection()
        {
            if (sReflectionInitialized) return;
            
            var assembly = typeof(IAction).Assembly;
            
            sSequenceType = assembly.GetType("YokiFrame.Sequence");
            sParallelType = assembly.GetType("YokiFrame.Parallel");
            sRepeatType = assembly.GetType("YokiFrame.Repeat");
            
            if (sSequenceType != null)
            {
                sSequenceGetActions = sSequenceType.GetMethod("EditorGetActions", BindingFlags.Instance | BindingFlags.NonPublic);
                sSequenceGetIndex = sSequenceType.GetMethod("EditorGetCurrentIndex", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            
            if (sParallelType != null)
            {
                sParallelGetActions = sParallelType.GetMethod("EditorGetActions", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            
            if (sRepeatType != null)
            {
                sRepeatGetSequence = sRepeatType.GetMethod("EditorGetSequence", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            
            sReflectionInitialized = true;
        }

        /// <summary>
        /// 获取 Action 的子节点数量
        /// </summary>
        public static int GetChildCount(IAction action)
        {
            InitReflection();
            
            var type = action.GetType();
            
            if (type == sSequenceType && sSequenceGetActions != null)
            {
                var actions = sSequenceGetActions.Invoke(action, null) as IReadOnlyList<IAction>;
                return actions?.Count ?? 0;
            }
            
            if (type == sParallelType && sParallelGetActions != null)
            {
                var actions = sParallelGetActions.Invoke(action, null) as IReadOnlyList<IAction>;
                return actions?.Count ?? 0;
            }
            
            if (type == sRepeatType && sRepeatGetSequence != null)
            {
                var sequence = sRepeatGetSequence.Invoke(action, null) as IAction;
                return sequence != null ? GetChildCount(sequence) : 0;
            }
            
            return 0;
        }

        /// <summary>
        /// 获取 Action 的指定子节点
        /// </summary>
        public static IAction GetChild(IAction action, int index)
        {
            InitReflection();
            
            var type = action.GetType();
            
            if (type == sSequenceType && sSequenceGetActions != null)
            {
                var actions = sSequenceGetActions.Invoke(action, null) as IReadOnlyList<IAction>;
                if (actions != null && index >= 0 && index < actions.Count)
                    return actions[index];
            }
            
            if (type == sParallelType && sParallelGetActions != null)
            {
                var actions = sParallelGetActions.Invoke(action, null) as IReadOnlyList<IAction>;
                if (actions != null && index >= 0 && index < actions.Count)
                    return actions[index];
            }
            
            if (type == sRepeatType && sRepeatGetSequence != null)
            {
                var sequence = sRepeatGetSequence.Invoke(action, null) as IAction;
                return sequence != null ? GetChild(sequence, index) : null;
            }
            
            return null;
        }

        /// <summary>
        /// 获取当前执行的子节点索引
        /// </summary>
        public static int GetCurrentChildIndex(IAction action)
        {
            InitReflection();
            
            var type = action.GetType();
            
            if (type == sSequenceType && sSequenceGetIndex != null)
            {
                return (int)sSequenceGetIndex.Invoke(action, null);
            }
            
            if (type == sRepeatType && sRepeatGetSequence != null)
            {
                var sequence = sRepeatGetSequence.Invoke(action, null) as IAction;
                return sequence != null ? GetCurrentChildIndex(sequence) : -1;
            }
            
            return -1; // Parallel 同时执行所有
        }

        /// <summary>
        /// 判断是否为容器类型 Action
        /// </summary>
        public static bool IsContainerAction(IAction action)
        {
            InitReflection();
            var type = action.GetType();
            return type == sSequenceType || type == sParallelType || type == sRepeatType;
        }

        /// <summary>
        /// 获取 Action 类型名（用于显示）
        /// </summary>
        public static string GetTypeName(IAction action)
        {
            return action.GetType().Name;
        }

        /// <summary>
        /// 从场景中收集所有活跃的根 Action
        /// </summary>
        public static void CollectActiveActions(List<IAction> result, List<string> executorNames)
        {
            result.Clear();
            executorNames.Clear();
            
            var executors = UnityEngine.Object.FindObjectsByType<MonoUpdateExecutor>(FindObjectsSortMode.None);
            var tempList = new List<IAction>(16);
            
            foreach (var executor in executors)
            {
                executor.GetExecutingActions(tempList);
                foreach (var action in tempList)
                {
                    result.Add(action);
                    executorNames.Add(executor.gameObject.name);
                }
            }
        }
    }
}
