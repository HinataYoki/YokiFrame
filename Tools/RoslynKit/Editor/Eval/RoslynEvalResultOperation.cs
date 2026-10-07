#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// eval_result：**纯读**编译阶段状态与运行结论，永不调用用户代码（§10.2 规则 5）。
    /// </summary>
    public sealed class RoslynEvalResultOperation : IRoslynOperation
    {
        private readonly IRoslynScriptEvalService mScriptService;

        /// <summary>创建 eval_result 操作。</summary>
        /// <param name="scriptService">脚本 eval 服务；宿主只有脚本 eval 时也要能读。</param>
        public RoslynEvalResultOperation(
            IRoslynScriptEvalService scriptService)
        {
            mScriptService = scriptService;
            Descriptor = new RoslynOperationDescriptor(
                "eval_result",
                YokiFrameCommandKind.ReadOnly,
                isDiagnostic: true,
                isCancellation: false,
                targets: RoslynExecutionTargets.ALL,
                isTargetAgnostic: true);
        }

        /// <summary>获取操作描述。</summary>
        public RoslynOperationDescriptor Descriptor { get; }

        /// <summary>读取 eval 状态。</summary>
        /// <param name="request">命令请求。</param>
        /// <returns>命令结果。</returns>
        public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
        {
            if (mScriptService == null)
            {
                return YokiFrameCommandResult.Error(
                    RoslynErrorCodes.UNAVAILABLE,
                    "This host does not wire the eval subsystem.");
            }

            if (!RoslynRunPayload.TryReadId(request.PayloadJson, "id", out string id, out string error))
            {
                return YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, error);
            }

            bool found = mScriptService.TryRead(id, out RoslynEvalRecord record);

            if (!found || record == null)
            {
                return YokiFrameCommandResult.Error(
                    RoslynErrorCodes.RUN_NOT_FOUND,
                    "eval '" + id + "' does not exist.");
            }

            return YokiFrameCommandResult.Success(new RoslynJsonBuilder()
                .StartObject()
                .Property("operation", "eval_result")
                .Property("evalId", record.Id)
                .Property("state", record.State)
                .Property("terminal", record.IsTerminal)
                .Property("sourcePath", record.SourcePath)
                .Property("codeHash", record.CodeHash)
                .Property("codeLength", record.CodeLength)
                .Property("runId", record.RunId)
                .Property("attempts", record.Attempts)
                .Property("compilerErrors", record.CompilerErrors)
                .Property("note", record.Note)
                .Property("observeAction", "eval_result")
                .EndObject()
                .ToString());
        }
    }
}
#endif
