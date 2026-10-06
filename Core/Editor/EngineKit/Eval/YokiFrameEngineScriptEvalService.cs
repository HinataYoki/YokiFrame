#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    /// <summary>脚本类 eval 的服务接口。</summary>
    public interface IYokiFrameEngineScriptEvalService
    {
        /// <summary>获取脚本语言标识；宿主未接入时为空。</summary>
        string Language { get; }

        /// <summary>提交一次脚本 eval；同步返回终态记录。</summary>
        /// <param name="id">eval 标识。</param>
        /// <param name="code">脚本源码。</param>
        /// <param name="errorCode">请求级失败错误码（编译失败不算请求失败）。</param>
        /// <param name="errorMessage">请求级失败说明。</param>
        /// <returns>eval 记录；请求级失败时为 null。</returns>
        YokiFrameEngineEvalRecord Submit(string id, string code, out string errorCode, out string errorMessage);

        /// <summary>读取记录。</summary>
        /// <param name="id">eval 标识。</param>
        /// <param name="record">记录。</param>
        /// <returns>存在时返回 true。</returns>
        bool TryRead(string id, out YokiFrameEngineEvalRecord record);

        /// <summary>回收脚本实例与记录（含未完成记录时需显式要求）。</summary>
        /// <param name="includeNonTerminal">是否包含非终态记录。</param>
        /// <returns>回收数量。</returns>
        int Prune(bool includeNonTerminal);
    }

    /// <summary>宿主声明自己提供脚本 eval 能力；与 C# eval 的 Provider 同构。</summary>
    public interface IYokiFrameEngineScriptEvalProvider
    {
        /// <summary>获取脚本 eval 服务；未接入时返回 null。</summary>
        IYokiFrameEngineScriptEvalService ScriptEval { get; }
    }

    /// <summary>
    /// 脚本类 eval 服务：同步编译 + 立即调用，结论落在记录的 Note 上。
    /// </summary>
    /// <remarks>
    /// 宿主脚本的编译模型：
    /// ① 脚本编译同步完成，Submit 返回时已是终态（Ready / CompileFailed），没有轮询阶段；
    /// ② 不写盘、不做类型探测、不受域重载影响——源码与实例都在宿主内存里，因此 SourcePath 记为内存标记；
    /// ③ 调用结果直接写 Note（C# 路径的结论来自运行调度器）。
    /// 记录复用 <see cref="YokiFrameEngineEvalStore"/>，所以 eval_result / eval_prune 对两类 eval 行为一致。
    /// </remarks>
    public sealed class YokiFrameEngineScriptEvalService : IYokiFrameEngineScriptEvalService
    {
        /// <summary>错误摘要最大长度。</summary>
        public const int MAX_ERROR_CHARS = 2000;

        /// <summary>内存脚本的 SourcePath 标记；用于 Prune 区分脚本记录与 C# 记录。</summary>
        public const string IN_MEMORY_PATH = "memory://script";

        private readonly IYokiFrameEngineScriptEvalHost mHost;
        private readonly YokiFrameEngineEvalStore mStore;

        /// <summary>创建脚本 eval 服务。</summary>
        /// <param name="host">脚本宿主接缝。</param>
        /// <param name="store">eval 记录存储。</param>
        public YokiFrameEngineScriptEvalService(IYokiFrameEngineScriptEvalHost host, YokiFrameEngineEvalStore store)
        {
            mHost = host;
            mStore = store;
        }

        /// <summary>获取脚本语言标识；宿主未接入时为空。</summary>
        public string Language
        {
            get { return mHost == null ? string.Empty : mHost.Language; }
        }

        /// <summary>提交一次脚本 eval；同步返回终态记录。</summary>
        /// <param name="id">eval 标识。</param>
        /// <param name="code">脚本源码。</param>
        /// <param name="errorCode">请求级失败错误码。</param>
        /// <param name="errorMessage">请求级失败说明。</param>
        /// <returns>eval 记录。</returns>
        public YokiFrameEngineEvalRecord Submit(string id, string code, out string errorCode, out string errorMessage)
        {
            errorCode = string.Empty;
            errorMessage = string.Empty;
            if (!YokiFrameEngineEvalRequest.IsSafeId(id) || string.IsNullOrWhiteSpace(code))
            {
                errorCode = YokiFrameEngineErrorCodes.INVALID_PAYLOAD;
                errorMessage = "script eval requires both id and code.";
                return null;
            }

            if (mHost == null || mStore == null)
            {
                errorCode = YokiFrameEngineErrorCodes.UNAVAILABLE;
                errorMessage = "This host does not wire the script eval subsystem.";
                return null;
            }

            DateTime now = DateTime.UtcNow;
            var record = new YokiFrameEngineEvalRecord
            {
                Id = id,
                Token = Guid.NewGuid().ToString("N"),
                State = YokiFrameEngineEvalStates.COMPILING,
                SourcePath = IN_MEMORY_PATH,
                CodeHash = YokiFrameEngineEvalRequest.ComputeSha256(code),
                CodeLength = code.Length,
                SubmittedAtUtc = now,
                UpdatedAtUtc = now
            };

            // 编译失败是"提交成功但结论失败"，用状态表达而不是错误码（与 C# 路径一致）。
            if (!mHost.TryCompile(id, record.Token, code, out string compileError))
            {
                record.State = YokiFrameEngineEvalStates.COMPILE_FAILED;
                record.CompilerErrors = Trim(compileError);
                record.UpdatedAtUtc = DateTime.UtcNow;
                mStore.Save(record);
                return record;
            }

            record.State = YokiFrameEngineEvalStates.READY;
            record.Note = mHost.TryInvoke(id, record.Token, out string resultJson, out string invokeError)
                ? (string.IsNullOrWhiteSpace(resultJson) ? "{}" : resultJson)
                : "invoke failed: " + Trim(invokeError);
            record.UpdatedAtUtc = DateTime.UtcNow;
            mStore.Save(record);
            return record;
        }

        /// <summary>读取记录。</summary>
        /// <param name="id">eval 标识。</param>
        /// <param name="record">记录。</param>
        /// <returns>存在时返回 true。</returns>
        public bool TryRead(string id, out YokiFrameEngineEvalRecord record)
        {
            record = null;
            return mStore != null && !string.IsNullOrWhiteSpace(id) && mStore.TryRead(id, out record);
        }

        /// <summary>回收脚本实例与记录。</summary>
        /// <param name="includeNonTerminal">是否包含非终态记录。</param>
        /// <returns>回收数量。</returns>
        public int Prune(bool includeNonTerminal)
        {
            if (mStore == null)
            {
                return 0;
            }

            int removed = 0;
            var records = mStore.ReadAll();
            for (var index = 0; index < records.Count; index++)
            {
                YokiFrameEngineEvalRecord record = records[index];
                if (!string.Equals(record.SourcePath, IN_MEMORY_PATH, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!includeNonTerminal && !record.IsTerminal)
                {
                    continue;
                }

                if (mHost != null)
                {
                    mHost.Unload(record.Id);
                }

                if (mStore.TryDelete(record.Id))
                {
                    removed++;
                }
            }

            return removed;
        }

        private static string Trim(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "unknown";
            }

            return text.Length <= MAX_ERROR_CHARS ? text : text.Substring(0, MAX_ERROR_CHARS);
        }
    }
}
#endif
