#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace YokiFrame
{
    /// <summary>Counts actual player-loop passes, not Time.frameCount sampled by editor ticks.</summary>
    [InitializeOnLoad]
    internal static class UnityAutomationFrameClock
    {
        private struct BeginAutomationFrame { }
        private struct EndAutomationFrame { }
        private static long sStarted;
        private static long sCompleted;
        private static bool sInstalled;

        static UnityAutomationFrameClock()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Remove;
            if (EditorApplication.isPlaying) EditorApplication.delayCall += Install;
        }

        internal static long Started => EditorApplication.isPlaying && sInstalled ? sStarted : -1;
        internal static long Completed => EditorApplication.isPlaying && sInstalled ? sCompleted : -1;

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) Install();
            else if (state == PlayModeStateChange.ExitingPlayMode) Remove();
        }

        private static void Install()
        {
            if (!EditorApplication.isPlaying) return;
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            Strip(ref loop);
            bool begin = Insert(ref loop, typeof(Update.ScriptRunBehaviourUpdate),
                new PlayerLoopSystem { type = typeof(BeginAutomationFrame), updateDelegate = Begin }, false);
            bool end = Insert(ref loop, typeof(PreLateUpdate.ScriptRunBehaviourLateUpdate),
                new PlayerLoopSystem { type = typeof(EndAutomationFrame), updateDelegate = End }, true);
            sInstalled = begin && end;
            if (!sInstalled) return;
            sStarted = 0;
            sCompleted = 0;
            PlayerLoop.SetPlayerLoop(loop);
        }

        private static void Begin()
        {
            if (EditorApplication.isPlaying && !EditorApplication.isPaused) sStarted++;
        }

        private static void End()
        {
            if (EditorApplication.isPlaying && !EditorApplication.isPaused) sCompleted = sStarted;
        }

        private static void Remove()
        {
            if (!sInstalled) return;
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            Strip(ref loop);
            PlayerLoop.SetPlayerLoop(loop);
            sInstalled = false;
        }

        private static bool Insert(ref PlayerLoopSystem loop, Type anchor, PlayerLoopSystem item, bool after)
        {
            var children = loop.subSystemList;
            if (children == null) return false;
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].type == anchor)
                {
                    int position = after ? i + 1 : i;
                    var expanded = new PlayerLoopSystem[children.Length + 1];
                    Array.Copy(children, 0, expanded, 0, position);
                    expanded[position] = item;
                    Array.Copy(children, position, expanded, position + 1, children.Length - position);
                    loop.subSystemList = expanded;
                    return true;
                }
                if (Insert(ref children[i], anchor, item, after)) return true;
            }
            return false;
        }

        private static void Strip(ref PlayerLoopSystem loop)
        {
            if (loop.subSystemList == null) return;
            var children = new List<PlayerLoopSystem>(loop.subSystemList.Length);
            foreach (var original in loop.subSystemList)
            {
                if (original.type == typeof(BeginAutomationFrame) || original.type == typeof(EndAutomationFrame)) continue;
                var child = original;
                Strip(ref child);
                children.Add(child);
            }
            loop.subSystemList = children.ToArray();
        }
    }
}
#endif
