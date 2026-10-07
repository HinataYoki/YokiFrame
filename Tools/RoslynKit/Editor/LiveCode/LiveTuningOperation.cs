#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using YokiFrame.Json;

namespace YokiFrame
{
    public sealed class LiveTuningOperation : IRoslynOperation
    {
        private readonly LiveTuningBinder mBinder;
        public RoslynOperationDescriptor Descriptor { get; }
        /// <summary>按动作注册调参命令。未知动作在构造时拒绝。状态和解绑是只读或取消，绑定与刷新沿用调用方目标和许可。</summary>
        /// <param name="binder">会话内调参绑定器。</param>
        /// <param name="action">live_tuning_bind、status、unbind 或 refresh。</param>
        /// <param name="permission">绑定和刷新所需的执行许可；只读与解绑不使用。</param>
        /// <param name="targets">绑定和刷新允许的执行目标，默认 Play。</param>
        public LiveTuningOperation(LiveTuningBinder binder, string action, Func<bool> permission = null,
            RoslynExecutionTarget targets = RoslynExecutionTarget.Play)
        {
            if (action != "live_tuning_bind" && action != "live_tuning_status"
                && action != "live_tuning_unbind" && action != "live_tuning_refresh")
                throw new ArgumentException("Unknown live tuning action.");
            mBinder = binder;
            bool read = action == "live_tuning_status";
            bool cancel = action == "live_tuning_unbind";
            Descriptor = new RoslynOperationDescriptor(action,
                read ? YokiFrameCommandKind.ReadOnly : cancel ? YokiFrameCommandKind.UserAction : YokiFrameCommandKind.Dangerous,
                isDiagnostic: read, isCancellation: cancel,
                targets: read || cancel ? RoslynExecutionTargets.ALL : targets,
                isTargetAgnostic: read || cancel, executionPermission: read || cancel ? null : permission);
        }
        /// <summary>执行调参命令。参数、JSON、状态和文件类拒绝返回 LiveTuningRejected，其他异常继续抛出。</summary>
        /// <param name="request">含动作载荷的命令。</param>
        /// <returns>成功 JSON，或带 LiveTuningRejected 的失败结果。</returns>
        public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
        {
            try
            {
                var json = new RoslynJsonBuilder().StartObject().Property("operation", Descriptor.Action);
                if (Descriptor.Action == "live_tuning_status")
                {
                    json.Name("bindings").StartArray();
                    foreach (var status in mBinder.List()) Write(json, status);
                    return YokiFrameCommandResult.Success(json.EndArray().EndObject().ToString());
                }
                using var document = JsonDocument.Parse(request.PayloadJson);
                var root = document.RootElement;
                string id = LiveFieldRequest.RequiredText(root, "id");
                if (Descriptor.Action == "live_tuning_unbind")
                    return YokiFrameCommandResult.Success(json.Property("removed", mBinder.Unbind(id)).EndObject().ToString());
                if (!root.TryGetProperty("confirmed", out var confirmed) || confirmed.ValueKind != JsonValueKind.True)
                    throw new ArgumentException("Tuning bind/refresh requires confirmed:true.");
                var result = Descriptor.Action == "live_tuning_bind"
                    ? mBinder.Bind(id, LiveFieldRequest.RequiredText(root, "path")) : mBinder.Refresh(id);
                json.Name("binding");
                Write(json, result);
                return YokiFrameCommandResult.Success(json.EndObject().ToString());
            }
            catch (Exception error) when (error is ArgumentException || error is JsonException
                || error is InvalidOperationException || error is System.IO.IOException || error is UnauthorizedAccessException)
            {
                return YokiFrameCommandResult.Error("LiveTuningRejected", error.Message);
            }
        }
        /// <summary>把一条调参状态按固定字段写入当前 JSON 对象。</summary>
        /// <param name="json">调用方已开始的 JSON 构造器。</param>
        /// <param name="status">绑定状态。</param>
        private static void Write(RoslynJsonBuilder json, LiveTuningStatus status)
        {
            json.StartObject().Property("id", status.Id).Property("path", status.Path)
                .Property("state", status.State).Property("error", status.Error)
                .Property("applyCount", status.ApplyCount).Property("appliedFields", status.AppliedFields)
                .Property("contentHash", status.ContentHash).EndObject();
        }
    }
}
#endif
