#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    /// <summary>
    /// RoslynKit 的命令入口：解析 action、经 Gate 裁决，再交给具体操作执行。
    /// </summary>
    /// <remarks>
    /// Gate 拒绝时不会调用任何操作的 Execute，这是对外契约的一部分。
    /// </remarks>
    public sealed class RoslynCommandHandler : YokiFrameKitCommandHandler
    {
        /// <summary>RoslynKit 的稳定 Kit 标识。</summary>
        public const string KIT_NAME = "RoslynKit";

        private readonly RoslynGate mGate;
        private readonly IRoslynOperation[] mOperations;
        private readonly RoslynExecutionTarget mHostTargets;

        /// <summary>
        /// 创建 RoslynKit handler。
        /// </summary>
        /// <param name="gate">Engine 策略 Gate。</param>
        /// <param name="operations">当前宿主实际支持的操作。</param>
        public RoslynCommandHandler(
            RoslynGate gate,
            IRoslynOperation[] operations)
            : this(gate, operations, RoslynExecutionTargets.ALL)
        {
        }

        /// <summary>
        /// 创建 RoslynKit handler，并声明当前宿主能承载的执行目标。
        /// </summary>
        /// <param name="gate">Engine 策略 Gate。</param>
        /// <param name="operations">当前宿主实际支持的操作。</param>
        /// <param name="hostTargets">当前宿主可承载的执行目标。</param>
        public RoslynCommandHandler(
            RoslynGate gate,
            IRoslynOperation[] operations,
            RoslynExecutionTarget hostTargets)
            : base(KIT_NAME, CreateCommandDescriptors(operations))
        {
            mGate = gate ?? throw new ArgumentNullException(nameof(gate));
            mOperations = operations;
            mHostTargets = hostTargets;
        }

        /// <summary>
        /// 获取当前 handler 暴露的操作数量，供宿主与测试诊断。
        /// </summary>
        public int OperationCount
        {
            get { return mOperations.Length; }
        }

        /// <summary>
        /// 执行 RoslynKit 命令：先裁决，再执行。
        /// </summary>
        /// <param name="request">已匹配 Kit 与 action 的命令请求。</param>
        /// <returns>命令终态结果。</returns>
        protected override YokiFrameCommandResult HandleAction(YokiFrameCommandRequest request)
        {
            IRoslynOperation operation = FindOperation(request.Action);
            if (operation == null)
            {
                return YokiFrameCommandResult.Error(
                    RoslynErrorCodes.UNKNOWN_COMMAND,
                    "RoslynKit operation is not registered: " + request.Action + ".");
            }

            RoslynGateDecision decision = mGate.Evaluate(request, operation.Descriptor, mHostTargets);
            if (!decision.IsAllowed)
            {
                return YokiFrameCommandResult.Error(decision.ErrorCode, decision.ErrorMessage);
            }

            try
            {
                return operation.Execute(request)
                    ?? YokiFrameCommandResult.Error(
                        RoslynErrorCodes.FAILED,
                        "RoslynKit operation returned no result: " + request.Action + ".");
            }
            catch (Exception exception)
            {
                return YokiFrameCommandResult.Error(
                    RoslynErrorCodes.FAILED,
                    "RoslynKit operation failed: " + exception.Message);
            }
        }

        /// <summary>
        /// 按 action 查找操作。
        /// </summary>
        /// <param name="action">action 标识。</param>
        /// <returns>命中的操作；未注册时返回 null。</returns>
        private IRoslynOperation FindOperation(string action)
        {
            for (var index = 0; index < mOperations.Length; index++)
            {
                if (string.Equals(mOperations[index].Descriptor.Action, action, StringComparison.Ordinal))
                {
                    return mOperations[index];
                }
            }

            return null;
        }

        /// <summary>
        /// 由操作声明生成命令描述，并拒绝重复 action。
        /// </summary>
        /// <param name="operations">宿主支持的操作。</param>
        /// <returns>命令描述数组。</returns>
        private static YokiFrameCommandDescriptor[] CreateCommandDescriptors(
            IRoslynOperation[] operations)
        {
            if (operations == null)
            {
                throw new ArgumentNullException(nameof(operations));
            }

            var descriptors = new YokiFrameCommandDescriptor[operations.Length];
            for (var index = 0; index < operations.Length; index++)
            {
                IRoslynOperation operation = operations[index];
                if (operation == null || operation.Descriptor == null)
                {
                    throw new ArgumentException("RoslynKit operation must declare a descriptor.", nameof(operations));
                }

                for (var previous = 0; previous < index; previous++)
                {
                    if (string.Equals(
                            operations[previous].Descriptor.Action,
                            operation.Descriptor.Action,
                            StringComparison.Ordinal))
                    {
                        throw new ArgumentException(
                            "Duplicate RoslynKit operation action: " + operation.Descriptor.Action + ".",
                            nameof(operations));
                    }
                }

                descriptors[index] = operation.Descriptor.ToCommandDescriptor(KIT_NAME);
            }

            return descriptors;
        }
    }
}
#endif
