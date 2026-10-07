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

        /// <summary>
        /// 域加载时订阅播放状态和程序集重载。已经在播放则延迟安装，避免静态构造期间改 PlayerLoop。
        /// </summary>
        static UnityAutomationFrameClock()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Remove;
            if (EditorApplication.isPlaying) EditorApplication.delayCall += Install;
        }

        internal static long Started => EditorApplication.isPlaying && sInstalled ? sStarted : -1;
        internal static long Completed => EditorApplication.isPlaying && sInstalled ? sCompleted : -1;

        /// <summary>进入播放时安装计数，退出播放时卸下。其它状态不改 PlayerLoop。</summary>
        /// <param name="state">播放模式变化。</param>
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) Install();
            else if (state == PlayModeStateChange.ExitingPlayMode) Remove();
        }

        /// <summary>
        /// 把开始/结束回调插入当前 PlayerLoop。两处锚点都找到才重置计数并写回；
        /// 否则保持未安装，不调用 SetPlayerLoop。
        /// </summary>
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

        /// <summary>播放且未暂停时递增已开始的玩家循环次数。</summary>
        private static void Begin()
        {
            if (EditorApplication.isPlaying && !EditorApplication.isPaused) sStarted++;
        }

        /// <summary>播放且未暂停时把完成计数对齐到已开始次数。</summary>
        private static void End()
        {
            if (EditorApplication.isPlaying && !EditorApplication.isPaused) sCompleted = sStarted;
        }

        /// <summary>已安装时从当前 PlayerLoop 去掉计数回调，不清零历史计数。</summary>
        private static void Remove()
        {
            if (!sInstalled) return;
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            Strip(ref loop);
            PlayerLoop.SetPlayerLoop(loop);
            sInstalled = false;
        }

        /// <summary>
        /// 在锚点前或后插入子系统。找不到锚点时递归子列表；改的是传入的 loop 副本字段。
        /// </summary>
        /// <param name="loop">待修改的循环节点。</param>
        /// <param name="anchor">定位用的子系统类型。</param>
        /// <param name="item">要插入的回调。</param>
        /// <param name="after">为 true 时插在锚点之后。</param>
        /// <returns>找到锚点并插入时返回 true。</returns>
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

        /// <summary>递归去掉本时钟插入的开始/结束子系统，并压缩子列表。</summary>
        /// <param name="loop">待清理的循环节点。</param>
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
