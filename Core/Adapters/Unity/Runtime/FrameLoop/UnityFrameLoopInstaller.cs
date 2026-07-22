#if UNITY_5_3_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 把 Unity Update 的缩放与非缩放时间投递给纯 C# Core 帧派发器。
    /// </summary>
    internal static class UnityFrameLoopInstaller
    {
        private struct YokiFrameUpdateSystem { }

        private static readonly PlayerLoopSystem.UpdateFunction sDispatchFrame = DispatchFrame;

        /// <summary>
        /// 获取安装到 PlayerLoop 的稳定节点类型，供架构测试验证位置和幂等性。
        /// </summary>
        internal static Type UpdateSystemType => typeof(YokiFrameUpdateSystem);

        /// <summary>
        /// 进入新 Unity 子系统代际前通知全部 Runtime 监听者清理活动状态。
        /// 无 Domain Reload 重进 Play Mode 时监听注册仍保留，下一代可直接继续接收帧。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSubsystemRegistration()
        {
            YokiFrameUpdateDispatcher.ResetListeners();
        }

        /// <summary>
        /// 场景加载前确保 YokiFrame Update 节点存在；重复进入时不会累加节点。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallBeforeSceneLoad()
        {
            Install();
        }

        /// <summary>
        /// 读取当前 PlayerLoop 并按需安装更新节点；找不到 Unity Update 组时记录诊断并保持原树。
        /// </summary>
        internal static void Install()
        {
            PlayerLoopSystem playerLoop = PlayerLoop.GetCurrentPlayerLoop();
            if (!TryInstall(ref playerLoop, out bool changed))
            {
                LogKit.Warning("[FrameLoop] Unity PlayerLoop does not contain an Update system.");
                return;
            }

            if (changed)
            {
                PlayerLoop.SetPlayerLoop(playerLoop);
            }
        }

        /// <summary>
        /// 在给定 PlayerLoop 的 Update 组中安装唯一 YokiFrame 节点，便于无副作用测试安装算法。
        /// </summary>
        /// <param name="playerLoop">需要检查和按需修改的 PlayerLoop 根节点。</param>
        /// <param name="changed">成功新增节点时为 true；节点已存在时为 false。</param>
        /// <returns>找到 Unity Update 组时返回 true。</returns>
        internal static bool TryInstall(ref PlayerLoopSystem playerLoop, out bool changed)
        {
            if (playerLoop.type == typeof(Update))
            {
                changed = AddUpdateSystem(ref playerLoop);
                return true;
            }

            PlayerLoopSystem[] children = playerLoop.subSystemList;
            if (children == null)
            {
                changed = false;
                return false;
            }

            for (var index = 0; index < children.Length; index++)
            {
                if (TryInstall(ref children[index], out changed))
                {
                    return true;
                }
            }

            changed = false;
            return false;
        }

        /// <summary>
        /// 把更新节点插入 ScriptRunBehaviourUpdate 后；目标节点缺失时追加到 Update 组末尾。
        /// </summary>
        /// <param name="updateLoop">已经定位的 Unity Update 组。</param>
        /// <returns>本次实际新增节点时返回 true。</returns>
        private static bool AddUpdateSystem(ref PlayerLoopSystem updateLoop)
        {
            PlayerLoopSystem[] source = updateLoop.subSystemList ?? Array.Empty<PlayerLoopSystem>();
            var insertIndex = source.Length;
            for (var index = 0; index < source.Length; index++)
            {
                if (source[index].type == UpdateSystemType)
                {
                    return false;
                }

                if (source[index].type == typeof(Update.ScriptRunBehaviourUpdate))
                {
                    insertIndex = index + 1;
                }
            }

            PlayerLoopSystem[] replacement = new PlayerLoopSystem[source.Length + 1];
            Array.Copy(source, 0, replacement, 0, insertIndex);
            replacement[insertIndex] = new PlayerLoopSystem
            {
                type = UpdateSystemType,
                updateDelegate = sDispatchFrame
            };
            Array.Copy(source, insertIndex, replacement, insertIndex + 1, source.Length - insertIndex);
            updateLoop.subSystemList = replacement;
            return true;
        }

        /// <summary>
        /// 在 Unity Update 阶段把两个宿主时间源投递给 Core；稳定帧不创建委托或集合快照。
        /// </summary>
        private static void DispatchFrame()
        {
            YokiFrameUpdateDispatcher.Tick(Time.deltaTime, Time.unscaledDeltaTime);
        }
    }
}
#endif
