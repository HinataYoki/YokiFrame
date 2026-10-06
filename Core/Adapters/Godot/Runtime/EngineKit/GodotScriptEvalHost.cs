#if GODOT && TOOLS
using System;
using System.Collections.Generic;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// Godot 引擎原生调用的接缝：把"编译一段 GDScript 并调用它的入口"隔离成最小面。
    /// </summary>
    /// <remarks>
    /// 为什么单独抽出来：Godot 的原生单例（<c>GDScript</c> / <c>Json</c> 等）在测试进程里可能直接终止进程，
    /// 因此 <see cref="GodotScriptEvalHost"/> 的行为（包装源码、令牌校验、错误归类、卸载）必须在注入的假编译器下可测；
    /// 真实现 <see cref="GodotGdScriptCompiler"/> 只做原生调用，不做任何判断。
    /// </remarks>
    public interface IGodotScriptCompiler
    {
        /// <summary>编译脚本；成功时返回可用于调用的句柄。</summary>
        /// <param name="source">完整脚本源码（已包含令牌与入口函数）。</param>
        /// <param name="handle">脚本句柄。</param>
        /// <param name="error">失败原因。</param>
        /// <returns>成功时返回 true。</returns>
        bool TryCompile(string source, out object handle, out string error);

        /// <summary>调用句柄上的零参方法，并把返回值序列化为 JSON 文本。</summary>
        /// <param name="handle">脚本句柄。</param>
        /// <param name="method">方法名。</param>
        /// <param name="resultJson">返回值 JSON。</param>
        /// <param name="error">失败原因。</param>
        /// <returns>成功时返回 true。</returns>
        bool TryCall(object handle, string method, out string resultJson, out string error);

        /// <summary>释放句柄。</summary>
        /// <param name="handle">脚本句柄。</param>
        void Free(object handle);
    }

    /// <summary>
    /// Godot 脚本 eval 宿主：把用户代码包成带令牌与 <c>run()</c> 入口的 GDScript，编译后立即调用。
    /// </summary>
    /// <remarks>
    /// 与 Unity 的 eval 宿主不同，这里**不落盘**：GDScript 在内存里编译（<c>SourceCode</c> + <c>Reload()</c>），
    /// 因此没有生成目录、没有 prune 源码的问题；<c>Unload</c> 只释放句柄。
    /// 令牌写进脚本头部注释并在调用前校验，避免同名残留被误用（与 C# 路径"类型全名 + 令牌"双匹配同义）。
    /// </remarks>
    public sealed class GodotScriptEvalHost : IYokiFrameEngineScriptEvalHost
    {
        /// <summary>脚本语言标识。</summary>
        public const string LANGUAGE = "gdscript";

        /// <summary>入口函数名。</summary>
        public const string ENTRY_FUNCTION = "run";

        private readonly IGodotScriptCompiler mCompiler;
        private readonly Dictionary<string, Entry> mEntries = new Dictionary<string, Entry>(StringComparer.Ordinal);

        /// <summary>创建宿主。</summary>
        /// <param name="compiler">脚本编译器；缺省使用 GDScript 真实现。</param>
        public GodotScriptEvalHost(IGodotScriptCompiler compiler = null)
        {
            mCompiler = compiler ?? new GodotGdScriptCompiler();
        }

        /// <summary>获取脚本语言标识。</summary>
        public string Language
        {
            get { return LANGUAGE; }
        }

        /// <summary>编译脚本。</summary>
        /// <param name="id">eval 标识。</param>
        /// <param name="token">编译令牌。</param>
        /// <param name="code">用户脚本（<c>run()</c> 的函数体）。</param>
        /// <param name="error">失败原因。</param>
        /// <returns>成功时返回 true。</returns>
        public bool TryCompile(string id, string token, string code, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(token))
            {
                error = "script eval requires both id and token.";
                return false;
            }

            // 同一 id 重复提交视为新一轮：先释放旧句柄，避免残留实例被调用。
            Unload(id);

            string source = Wrap(token, code);
            if (!mCompiler.TryCompile(source, out object handle, out error))
            {
                return false;
            }

            mEntries[id] = new Entry { Token = token, Handle = handle };
            return true;
        }

        /// <summary>调用入口函数。</summary>
        /// <param name="id">eval 标识。</param>
        /// <param name="token">编译令牌；与编译时不一致直接拒绝。</param>
        /// <param name="resultJson">返回值 JSON。</param>
        /// <param name="error">失败原因。</param>
        /// <returns>成功时返回 true。</returns>
        public bool TryInvoke(string id, string token, out string resultJson, out string error)
        {
            resultJson = string.Empty;
            error = string.Empty;
            if (!mEntries.TryGetValue(id ?? string.Empty, out Entry entry))
            {
                error = "script '" + id + "' has not been compiled.";
                return false;
            }

            if (!string.Equals(entry.Token, token, StringComparison.Ordinal))
            {
                error = "script token mismatch; the script was recompiled.";
                return false;
            }

            return mCompiler.TryCall(entry.Handle, ENTRY_FUNCTION, out resultJson, out error);
        }

        /// <summary>释放句柄。</summary>
        /// <param name="id">eval 标识。</param>
        public void Unload(string id)
        {
            if (id == null || !mEntries.TryGetValue(id, out Entry entry))
            {
                return;
            }

            mEntries.Remove(id);
            mCompiler.Free(entry.Handle);
        }

        /// <summary>
        /// 包装用户代码：<c>extends RefCounted</c> + 令牌注释 + <c>run()</c> 入口。
        /// </summary>
        /// <param name="token">编译令牌。</param>
        /// <param name="code">用户代码（函数体）。</param>
        /// <returns>完整脚本。</returns>
        private static string Wrap(string token, string code)
        {
            var builder = new StringBuilder(code == null ? 0 : code.Length + 128);
            builder.Append("# yokiframe-eval-token: ").Append(token).Append('\n');
            builder.Append("extends RefCounted\n\n");
            builder.Append("func ").Append(ENTRY_FUNCTION).Append("():\n");
            string[] lines = (code ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                builder.Append('\t').Append(lines[index]).Append('\n');
            }

            return builder.ToString();
        }

        private sealed class Entry
        {
            public string Token { get; set; } = string.Empty;

            public object Handle { get; set; }
        }
    }
}
#endif