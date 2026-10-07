#if GODOT && TOOLS
using Godot;

namespace YokiFrame
{
    /// <summary>
    /// GDScript 真实现：在内存里编译并调用，**不落盘**。
    /// </summary>
    /// <remarks>
    /// 本文件是 Godot 原生 API 的唯一聚集点：宿主逻辑（包装源码、令牌校验、卸载）在
    /// <see cref="GodotScriptEvalHost"/> 里并已被单测覆盖，这里只做调用、不做判断。
    /// 用到的 API（Godot 4 .NET）：<c>GDScript.SourceCode</c> / <c>GDScript.Reload()</c> /
    /// <c>GDScript.Call("new")</c> / <c>GodotObject.Call(method)</c> / <c>Json.Stringify(Variant)</c>。
    /// 正常路径需要活的 Godot 进程验证（本机测试只覆盖注入假编译器的分支）。
    /// </remarks>
    public sealed class GodotGdScriptCompiler : IGodotScriptCompiler
    {
        /// <summary>编译脚本。</summary>
        /// <param name="source">完整脚本源码。</param>
        /// <param name="handle">脚本句柄。</param>
        /// <param name="error">失败原因。</param>
        /// <returns>成功时返回 true。</returns>
        public bool TryCompile(string source, out object handle, out string error)
        {
            handle = null;
            error = string.Empty;
            var script = new GDScript();
            script.SourceCode = source;
            Godot.Error reload = script.Reload();
            if (reload != Godot.Error.Ok)
            {
                error = "GDScript compile failed: " + reload + ".";
                script.Dispose();
                return false;
            }

            Godot.Variant instance = script.Call("new");
            Godot.GodotObject target = instance.VariantType == Godot.Variant.Type.Object ? instance.AsGodotObject() : null;
            if (target == null)
            {
                error = "GDScript instance could not be created.";
                script.Dispose();
                return false;
            }

            handle = new ScriptHandle(script, target);
            return true;
        }

        /// <summary>调用零参入口并把返回值转成 JSON。</summary>
        /// <param name="handle">脚本句柄。</param>
        /// <param name="method">方法名。</param>
        /// <param name="resultJson">返回值 JSON。</param>
        /// <param name="error">失败原因。</param>
        /// <returns>成功时返回 true。</returns>
        public bool TryCall(object handle, string method, out string resultJson, out string error)
        {
            resultJson = string.Empty;
            error = string.Empty;
            if (!(handle is ScriptHandle scriptHandle) || !GodotObject.IsInstanceValid(scriptHandle.Instance))
            {
                error = "script instance is no longer valid.";
                return false;
            }

            if (!scriptHandle.Instance.HasMethod(method))
            {
                error = "script does not define " + method + "().";
                return false;
            }

            Godot.Variant result = scriptHandle.Instance.Call(method);
            resultJson = Godot.Json.Stringify(result);
            return true;
        }

        /// <summary>释放实例与脚本资源。</summary>
        /// <param name="handle">脚本句柄。</param>
        public void Free(object handle)
        {
            if (!(handle is ScriptHandle scriptHandle))
            {
                return;
            }

            if (GodotObject.IsInstanceValid(scriptHandle.Instance))
            {
                scriptHandle.Instance.Free();
            }

            scriptHandle.Script.Dispose();
        }

        private sealed class ScriptHandle
        {
            public ScriptHandle(GDScript script, GodotObject instance)
            {
                Script = script;
                Instance = instance;
            }

            public GDScript Script { get; }

            public GodotObject Instance { get; }
        }
    }
}
#endif