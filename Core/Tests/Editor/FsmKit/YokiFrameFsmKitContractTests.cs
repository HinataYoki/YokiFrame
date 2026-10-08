using System;
using System.Reflection;
using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 锁定 FsmKit 的程序集归属、枚举线值、事件形态和命令注册行为。
    /// </summary>
    public sealed class YokiFrameFsmKitContractTests
    {
        private const string CORE_ASSEMBLY_NAME = "YokiFrame";
        private const string EDITOR_ASSEMBLY_NAME = "YokiFrame.Editor";

        /// <summary>
        /// 清理命令注册表，避免用例之间残留状态机。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            FsmKitCommandHandler.ClearAll();
        }

        /// <summary>
        /// 再次清理命令注册表，保证失败路径也不泄漏全局状态。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            FsmKitCommandHandler.ClearAll();
        }

        /// <summary>
        /// 验证 FsmKit 业务类型位于 Core 主程序集，而不是 Editor 程序集。
        /// </summary>
        [Test]
        public void FsmKitTypes_位于Core程序集_AssemblyBoundary()
        {
            string[] typeNames =
            {
                "YokiFrame.MachineState",
                "YokiFrame.IState",
                "YokiFrame.IState`1",
                "YokiFrame.IFSM",
                "YokiFrame.IFSM`1",
                "YokiFrame.IFSM`2",
                "YokiFrame.FSM`1",
                "YokiFrame.FSM`2",
                "YokiFrame.AbstractState`2",
                "YokiFrame.AbstractState`3"
            };

            for (int index = 0; index < typeNames.Length; index++)
            {
                Assert.IsNotNull(
                    ResolveType(typeNames[index], CORE_ASSEMBLY_NAME),
                    "缺少 FsmKit 公开类型: " + typeNames[index]);
                Assert.IsNull(
                    ResolveType(typeNames[index], EDITOR_ASSEMBLY_NAME),
                    "FsmKit 业务类型不应进入 Editor 程序集: " + typeNames[index]);
            }
        }

        /// <summary>
        /// 验证 Hook 留在 Core，命令处理器留在 Editor 程序集。
        /// </summary>
        [Test]
        public void EditorObservationTypes_归属程序集_AssemblyBoundary()
        {
            Assert.IsNotNull(ResolveType("YokiFrame.FsmEditorHook", CORE_ASSEMBLY_NAME));
            Assert.IsNull(ResolveType("YokiFrame.FsmEditorHook", EDITOR_ASSEMBLY_NAME));
            Assert.IsNotNull(ResolveType("YokiFrame.FsmKitCommandHandler", EDITOR_ASSEMBLY_NAME));
            Assert.IsNull(ResolveType("YokiFrame.FsmKitCommandHandler", CORE_ASSEMBLY_NAME));
        }

        /// <summary>
        /// 验证 MachineState 数值保持既有 wire/debug 语义。
        /// </summary>
        [Test]
        public void MachineState_枚举值_保持End0Suspend1Running2()
        {
            Assert.AreEqual(0, (int)MachineState.End);
            Assert.AreEqual(1, (int)MachineState.Suspend);
            Assert.AreEqual(2, (int)MachineState.Running);
        }

        /// <summary>
        /// 验证调试 Hook 使用 event，外部不能用同名字段整体替换订阅链。
        /// </summary>
        [Test]
        public void FsmEditorHook_事件_不能被同名字段覆盖()
        {
            Type hookType = typeof(FsmEditorHook);
            string[] eventNames =
            {
                "OnFsmCreated",
                "OnFsmDisposed",
                "OnFsmCleared",
                "OnFsmStarted",
                "OnStateChanged",
                "OnStateAdded",
                "OnStateRemoved"
            };

            for (int index = 0; index < eventNames.Length; index++)
            {
                Assert.IsNotNull(hookType.GetEvent(eventNames[index]), "FsmEditorHook 缺少事件: " + eventNames[index]);
                Assert.IsNull(
                    hookType.GetField(eventNames[index]),
                    "FsmEditorHook 事件不能退回可整体覆盖的 Action 字段。");
            }
        }

        /// <summary>
        /// 验证显式注册、反注册和清空会改变 list_all 计数，未知 action 被拒绝。
        /// </summary>
        [Test]
        public void HandleAction_注册清理与未知动作_列表计数和异常符合入口契约()
        {
            FsmKitCommandHandler handler = new();
            CollectionAssert.AreEquivalent(
                new[] { "list_all", "get_state", "get_history", "get_state_events", "get_workbench_snapshot" },
                handler.SupportedActions);

            FSM<ContractStateId> fsm = new("Contract");
            FsmKitCommandHandler.RegisterFsm("Contract", fsm);
            Assert.AreEqual(1, ReadCount(handler, "list_all", "{}"));

            FsmKitCommandHandler.UnregisterFsm("Contract");
            Assert.AreEqual(0, ReadCount(handler, "list_all", "{}"));

            FsmKitCommandHandler.RegisterFsm("Contract", fsm);
            FsmKitCommandHandler.ClearAll();
            Assert.AreEqual(0, ReadCount(handler, "list_all", "{}"));
            Assert.Throws<NotSupportedException>(() => handler.HandleAction("unknown", "{}"));
        }

        /// <summary>契约测试使用的最小状态标识。</summary>
        private enum ContractStateId
        {
            Idle
        }

        /// <summary>执行只读命令并读取 count 字段。</summary>
        /// <param name="handler">命令处理器。</param>
        /// <param name="action">命令名。</param>
        /// <param name="payload">命令载荷。</param>
        /// <returns>结果中的 count。</returns>
        private static int ReadCount(FsmKitCommandHandler handler, string action, string payload)
        {
            using (YokiFrame.Json.JsonDocument document = YokiFrame.Json.JsonDocument.Parse(
                handler.HandleAction(action, payload)))
            {
                Assert.IsTrue(document.RootElement.GetProperty("count").TryGetInt32(out int value), "count");
                return value;
            }
        }

        /// <summary>从指定程序集解析类型，明确区分 Runtime 与共享 Editor 所有权。</summary>
        /// <param name="typeName">类型完整名称。</param>
        /// <param name="assemblyName">类型所属程序集。</param>
        /// <returns>已解析类型；缺失时为空。</returns>
        private static Type ResolveType(string typeName, string assemblyName)
        {
            return Type.GetType(typeName + ", " + assemblyName);
        }
    }
}
