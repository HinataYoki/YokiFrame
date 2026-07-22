using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace YokiFrame
{
    /// <summary>
    /// 验证 Unity Runtime Adapter 只负责把宿主帧投递给 Core，并保持 PlayerLoop 安装幂等。
    /// </summary>
    public sealed class YokiFrameUnityFrameLoopAdapterTests
    {
        private struct TestRootSystem { }
        private struct TailUpdateSystem { }

        /// <summary>
        /// 验证 YokiFrame 更新节点位于 ScriptRunBehaviourUpdate 后，保持与 2.0-pre 相同的业务 Update 时序。
        /// </summary>
        [Test]
        public void InstallerAddsUpdateSystemAfterScriptRunBehaviourUpdate()
        {
            PlayerLoopSystem playerLoop = CreatePlayerLoop();

            bool foundUpdateLoop = Unity.UnityFrameLoopInstaller.TryInstall(ref playerLoop, out bool changed);

            Assert.IsTrue(foundUpdateLoop);
            Assert.IsTrue(changed);
            PlayerLoopSystem[] updateSystems = playerLoop.subSystemList[0].subSystemList;
            Assert.AreEqual(typeof(Update.ScriptRunBehaviourUpdate), updateSystems[0].type);
            Assert.AreEqual(Unity.UnityFrameLoopInstaller.UpdateSystemType, updateSystems[1].type);
            Assert.AreEqual(typeof(TailUpdateSystem), updateSystems[2].type);
        }

        /// <summary>
        /// 验证重复安装只识别现有节点，不累加第二个委托或改写 PlayerLoop 数组。
        /// </summary>
        [Test]
        public void InstallerKeepsRepeatedInstallationIdempotent()
        {
            PlayerLoopSystem playerLoop = CreatePlayerLoop();
            Assert.IsTrue(Unity.UnityFrameLoopInstaller.TryInstall(ref playerLoop, out bool firstChanged));
            PlayerLoopSystem[] firstSystems = playerLoop.subSystemList[0].subSystemList;

            Assert.IsTrue(Unity.UnityFrameLoopInstaller.TryInstall(ref playerLoop, out bool secondChanged));

            Assert.IsTrue(firstChanged);
            Assert.IsFalse(secondChanged);
            Assert.AreSame(firstSystems, playerLoop.subSystemList[0].subSystemList);
            Assert.AreEqual(1, CountUpdateSystems(playerLoop));
        }

        /// <summary>
        /// 验证宿主入口使用两个 Unity 时间源，并在 SubsystemRegistration 阶段通知 Core 清理上一代状态。
        /// </summary>
        [Test]
        public void InstallerDispatchesUnityTimesAndResetsRuntimeListeners()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "YokiFrame", "Core", "Adapters", "Unity", "Runtime", "FrameLoop",
                "UnityFrameLoopInstaller.cs");
            string source = File.ReadAllText(sourcePath);

            StringAssert.StartsWith("#if UNITY_5_3_OR_NEWER", source.TrimStart());
            StringAssert.Contains(
                "YokiFrameUpdateDispatcher.Tick(Time.deltaTime, Time.unscaledDeltaTime);",
                source);
            StringAssert.Contains(
                "RuntimeInitializeLoadType.SubsystemRegistration",
                source);
            StringAssert.Contains("YokiFrameUpdateDispatcher.ResetListeners();", source);
            StringAssert.EndsWith("#endif", source.TrimEnd());
        }

        /// <summary>
        /// 创建带标准 Unity Update 子系统的最小 PlayerLoop，用于隔离安装算法测试。
        /// </summary>
        /// <returns>包含 ScriptRunBehaviourUpdate 和尾节点的测试 PlayerLoop。</returns>
        private static PlayerLoopSystem CreatePlayerLoop()
        {
            return new PlayerLoopSystem
            {
                type = typeof(TestRootSystem),
                subSystemList = new[]
                {
                    new PlayerLoopSystem
                    {
                        type = typeof(Update),
                        subSystemList = new[]
                        {
                            new PlayerLoopSystem { type = typeof(Update.ScriptRunBehaviourUpdate) },
                            new PlayerLoopSystem { type = typeof(TailUpdateSystem) }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// 递归统计测试树中的 YokiFrame 更新节点，确保安装幂等断言覆盖整个 PlayerLoop。
        /// </summary>
        /// <param name="system">待遍历的 PlayerLoop 节点。</param>
        /// <returns>YokiFrame 更新节点数量。</returns>
        private static int CountUpdateSystems(PlayerLoopSystem system)
        {
            var count = system.type == Unity.UnityFrameLoopInstaller.UpdateSystemType ? 1 : 0;
            PlayerLoopSystem[] children = system.subSystemList;
            if (children == null)
            {
                return count;
            }

            for (var index = 0; index < children.Length; index++)
            {
                count += CountUpdateSystems(children[index]);
            }

            return count;
        }
    }
}
