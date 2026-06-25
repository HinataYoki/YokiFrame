#if !GODOT
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace YokiFrame.Unity
{
    /// <summary>
    /// Unity PlayerLoop 驱动，只负责把宿主帧时间喂给纯 C# ActionKitScheduler。
    /// </summary>
    internal static class UnityActionKitPlayerLoopSystem
    {
        private struct ActionKitUpdateSystem { }
        private struct ActionKitRecycleSystem { }

        private static readonly object sLock = new object();
        private static readonly PlayerLoopSystem.UpdateFunction sCachedUpdateDelegate = UpdateActions;
        private static readonly PlayerLoopSystem.UpdateFunction sCachedRecycleDelegate = ProcessRecycle;
        private static bool sInitialized;

#if UNITY_EDITOR
        private static bool sPlayModeHookRegistered;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeSceneLoad()
        {
            Initialize();
        }

        public static void Initialize()
        {
            if (sInitialized)
                return;

            lock (sLock)
            {
                if (sInitialized)
                    return;

                var playerLoop = PlayerLoop.GetCurrentPlayerLoop();

                if (!InsertSystem<Update>(
                    ref playerLoop,
                    typeof(ActionKitUpdateSystem),
                    sCachedUpdateDelegate,
                    typeof(Update.ScriptRunBehaviourUpdate)))
                {
                    LogKit.Warning("[ActionKit] 注册 PlayerLoop Update 失败");
                }

                if (!InsertSystem<PreLateUpdate>(
                    ref playerLoop,
                    typeof(ActionKitRecycleSystem),
                    sCachedRecycleDelegate,
                    null))
                {
                    LogKit.Warning("[ActionKit] 注册 PlayerLoop Recycle 失败");
                }

                PlayerLoop.SetPlayerLoop(playerLoop);
                ActionKitScheduler.Initialize();
                ActionKitRuntimeLog.ErrorHandler = LogError;
                sInitialized = true;

#if UNITY_EDITOR
                if (!sPlayModeHookRegistered)
                {
                    UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                    sPlayModeHookRegistered = true;
                }
#endif
            }
        }

        public static void LogError(string message)
        {
            LogKit.Error(message);
        }

        private static void UpdateActions()
        {
            ActionKitScheduler.Tick(Time.deltaTime, Time.unscaledDeltaTime);
        }

        private static void ProcessRecycle()
        {
            ActionKitScheduler.ProcessRecycle();
        }

        private static bool InsertSystem<T>(
            ref PlayerLoopSystem playerLoop,
            Type systemType,
            PlayerLoopSystem.UpdateFunction updateFunction,
            Type insertAfter)
        {
            if (playerLoop.type != typeof(T))
            {
                if (playerLoop.subSystemList == null)
                    return false;

                for (var i = 0; i < playerLoop.subSystemList.Length; i++)
                {
                    if (InsertSystem<T>(ref playerLoop.subSystemList[i], systemType, updateFunction, insertAfter))
                        return true;
                }

                return false;
            }

            var sourceSubsystems = playerLoop.subSystemList ?? Array.Empty<PlayerLoopSystem>();
            var subsystems = new List<PlayerLoopSystem>(sourceSubsystems);
            for (var i = 0; i < subsystems.Count; i++)
            {
                if (subsystems[i].type == systemType)
                    return true;
            }

            var newSystem = new PlayerLoopSystem { type = systemType, updateDelegate = updateFunction };

            if (insertAfter == null)
            {
                subsystems.Insert(0, newSystem);
            }
            else
            {
                var index = subsystems.FindIndex(s => s.type == insertAfter);
                if (index >= 0)
                    subsystems.Insert(index + 1, newSystem);
                else
                    subsystems.Add(newSystem);
            }

            playerLoop.subSystemList = subsystems.ToArray();
            return true;
        }

#if UNITY_EDITOR
        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                ActionKitScheduler.Cleanup();
            else if (state == UnityEditor.PlayModeStateChange.EnteredEditMode)
                sInitialized = false;
        }
#endif
    }
}
#endif
