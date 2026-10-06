#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    /// <summary>
    /// 脚本类 eval 的宿主接缝：把"运行时编译一段脚本语言"抽象成三步。
    /// </summary>
    /// <remarks>
    /// 引擎无关：Godot 侧由 GDScript（内存里 <c>SourceCode</c> + <c>Reload()</c> + <c>Call()</c>）实现；
    /// 任何"运行时能编译脚本"的宿主都能复用同一套服务与记录，CLI 侧因此不需要区分语言。
    /// 与 C# eval 的关键差别：编译同步、不落盘、不需要类型探测。
    /// </remarks>
    public interface IYokiFrameEngineScriptEvalHost
    {
        /// <summary>获取脚本语言标识，例如 gdscript。</summary>
        string Language { get; }

        /// <summary>
        /// 编译一段脚本；失败时必须给出可读原因（会被截断后写入记录）。
        /// </summary>
        /// <param name="id">eval 标识。</param>
        /// <param name="token">本次编译令牌；宿主应保证只对本令牌的实例生效。</param>
        /// <param name="code">用户脚本源码。</param>
        /// <param name="error">失败原因。</param>
        /// <returns>编译成功时返回 true。</returns>
        bool TryCompile(string id, string token, string code, out string error);

        /// <summary>调用已编译脚本的入口，并把返回值序列化为 JSON。</summary>
        /// <param name="id">eval 标识。</param>
        /// <param name="token">编译令牌；与 TryCompile 不一致时必须拒绝。</param>
        /// <param name="resultJson">返回值 JSON；无返回值时为空对象。</param>
        /// <param name="error">失败原因。</param>
        /// <returns>调用成功时返回 true。</returns>
        bool TryInvoke(string id, string token, out string resultJson, out string error);

        /// <summary>释放脚本实例与相关资源。</summary>
        /// <param name="id">eval 标识。</param>
        void Unload(string id);
    }
}
#endif