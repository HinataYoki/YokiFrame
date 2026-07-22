using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 锁定 FsmKit 的业务 Runtime API，以及仅在 Editor/Tools 构建中存在的观察入口。
    /// </summary>
    public sealed class YokiFrameFsmKitContractTests
    {
        private const string CORE_ASSEMBLY_NAME = "YokiFrame";
        private const string EDITOR_ASSEMBLY_NAME = "YokiFrame.Editor";

        /// <summary>
        /// 验证 FsmKit 业务契约和状态实现由 Core 主程序集提供。
        /// </summary>
        [Test]
        public void RequiredPublicTypesExistInCoreAssembly()
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

            for (var index = 0; index < typeNames.Length; index++)
            {
                Assert.IsNotNull(ResolveType(typeNames[index]), "缺少 FsmKit 公开类型: " + typeNames[index]);
            }
        }

        /// <summary>
        /// 验证普通和带参 FSM 始终保留可选名称构造签名，使同一调用代码可以同时编译到 Editor 与 Player。
        /// </summary>
        [Test]
        public void FsmConstructorsKeepOptionalNameParameter()
        {
            Type genericFsm = RequireType("YokiFrame.FSM`1");
            Type argumentFsm = RequireType("YokiFrame.FSM`2");

            Assert.IsNotNull(genericFsm.GetConstructor(new[] { typeof(string) }));
            Assert.IsNotNull(argumentFsm.GetConstructor(new[] { typeof(string) }));
        }

        /// <summary>
        /// 验证 Editor 下仍编译 FsmKit Hook 和命令处理器，保证 Workbench 观察能力没有被边界调整破坏。
        /// </summary>
        [Test]
        public void EditorObservationTypesUseTheirOwningAssemblies()
        {
            Assert.IsNotNull(ResolveType("YokiFrame.FsmEditorHook"));
            Assert.IsNotNull(ResolveType("YokiFrame.FsmKitCommandHandler", EDITOR_ASSEMBLY_NAME));
        }

        /// <summary>
        /// 验证 MachineState 的数值与 2.0-pre wire/debug 语义保持一致。
        /// </summary>
        [Test]
        public void MachineStateValuesRemainStable()
        {
            Type machineStateType = RequireType("YokiFrame.MachineState");

            Assert.AreEqual(0, Convert.ToInt32(Enum.Parse(machineStateType, "End")));
            Assert.AreEqual(1, Convert.ToInt32(Enum.Parse(machineStateType, "Suspend")));
            Assert.AreEqual(2, Convert.ToInt32(Enum.Parse(machineStateType, "Running")));
        }

        /// <summary>
        /// 验证 IState 保留同步生命周期、Condition 和泛型消息入口。
        /// </summary>
        [Test]
        public void StateContractKeepsSynchronousLifecycleShape()
        {
            Type stateType = RequireType("YokiFrame.IState");
            string[] methods =
            {
                "Condition",
                "Start",
                "Suspend",
                "Update",
                "FixedUpdate",
                "CustomUpdate",
                "End",
                "Dispose",
                "SendMessage"
            };

            for (var index = 0; index < methods.Length; index++)
            {
                Assert.IsTrue(
                    stateType.GetMethods().Any(method => method.Name == methods[index]),
                    "IState 缺少方法: " + methods[index]);
            }

            MethodInfo messageMethod = stateType.GetMethods().Single(method => method.Name == "SendMessage");
            Assert.AreEqual(1, messageMethod.GetGenericArguments().Length);
        }

        /// <summary>
        /// 验证 IFSM 的业务状态查询成员保持稳定；这些按需查询本身不采集历史或注册 Workbench Provider。
        /// </summary>
        [Test]
        public void FsmStateInspectionContractRemainsAvailable()
        {
            Type fsmType = RequireType("YokiFrame.IFSM");
            string[] properties = { "MachineState", "Name", "EnumType", "CurrentState", "CurrentStateId" };

            for (var index = 0; index < properties.Length; index++)
            {
                Assert.IsNotNull(fsmType.GetProperty(properties[index]), "IFSM 缺少状态查询属性: " + properties[index]);
            }

            Assert.IsNotNull(fsmType.GetMethod("GetAllStates"));
            Assert.IsNotNull(fsmType.GetMethod("GetStateOrderIndex"));
        }

        /// <summary>
        /// 验证调试 Hook 保留七个事件名，并使用 event 防止外部覆盖整个订阅链。
        /// </summary>
        [Test]
        public void FsmEditorHookUsesControlledEvents()
        {
            Type hookType = RequireType("YokiFrame.FsmEditorHook");
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

            for (var index = 0; index < eventNames.Length; index++)
            {
                Assert.IsNotNull(hookType.GetEvent(eventNames[index]), "FsmEditorHook 缺少事件: " + eventNames[index]);
                Assert.IsNull(hookType.GetField(eventNames[index]), "FsmEditorHook 事件不能退回可整体覆盖的 Action 字段。");
            }
        }

        /// <summary>
        /// 验证 FsmKit 命令入口保留五个 2.0-pre 只读 action 和显式注册 API。
        /// </summary>
        [Test]
        public void CommandHandlerKeepsReadOnlyActionsAndRegistrationEntrypoints()
        {
            Type handlerType = RequireType("YokiFrame.FsmKitCommandHandler", EDITOR_ASSEMBLY_NAME);
            object handler = Activator.CreateInstance(handlerType);
            string[] actions = (string[])handlerType.GetProperty("SupportedActions")?.GetValue(handler);

            CollectionAssert.AreEquivalent(
                new[] { "list_all", "get_state", "get_history", "get_state_events", "get_workbench_snapshot" },
                actions);
            Assert.IsNotNull(handlerType.GetMethod("RegisterFsm", BindingFlags.Public | BindingFlags.Static));
            Assert.IsNotNull(handlerType.GetMethod("UnregisterFsm", BindingFlags.Public | BindingFlags.Static));
            Assert.IsNotNull(handlerType.GetMethod("ClearAll", BindingFlags.Public | BindingFlags.Static));
            Assert.IsNotNull(handlerType.GetMethod("HandleAction", new[] { typeof(string), typeof(string) }));
        }

        /// <summary>
        /// 从 Core 主程序集解析指定完整类型名；类型缺失时返回空供测试输出精确名称。
        /// </summary>
        /// <param name="typeName">类型完整名称，泛型类型需包含反引号参数数量。</param>
        /// <returns>已解析类型；缺失时为空。</returns>
        private static Type ResolveType(string typeName)
        {
            return ResolveType(typeName, CORE_ASSEMBLY_NAME);
        }

        /// <summary>从指定程序集解析类型，明确区分 Runtime 与共享 Editor 所有权。</summary>
        /// <param name="typeName">类型完整名称。</param>
        /// <param name="assemblyName">类型所属程序集。</param>
        /// <returns>已解析类型；缺失时为空。</returns>
        private static Type ResolveType(string typeName, string assemblyName)
        {
            return Type.GetType(typeName + ", " + assemblyName);
        }

        /// <summary>
        /// 获取必需类型，缺失时立即给出明确迁移断言。
        /// </summary>
        /// <param name="typeName">类型完整名称。</param>
        /// <returns>已解析的 Core 类型。</returns>
        private static Type RequireType(string typeName)
        {
            return RequireType(typeName, CORE_ASSEMBLY_NAME);
        }

        /// <summary>从指定程序集获取必需类型，缺失时输出准确迁移边界。</summary>
        /// <param name="typeName">类型完整名称。</param>
        /// <param name="assemblyName">类型所属程序集。</param>
        /// <returns>已确认存在的类型。</returns>
        private static Type RequireType(string typeName, string assemblyName)
        {
            Type type = ResolveType(typeName, assemblyName);
            Assert.IsNotNull(type, "缺少 FsmKit 类型: " + typeName);
            return type;
        }
    }
}
