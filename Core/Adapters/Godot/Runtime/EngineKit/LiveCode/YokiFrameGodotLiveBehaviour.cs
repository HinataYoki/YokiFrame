#if GODOT && TOOLS
using System;
using Godot;

namespace YokiFrame
{
    /// <summary>Managed prototype facade, not a Node. Only the precompiled host enters the scene tree.</summary>
    public abstract class YokiFrameGodotLiveBehaviour
    {
        public Node Node { get; internal set; }

        /// <summary>当前原型的预编译宿主；原型已分离时为 null，用于延迟调用等需要节点身份的能力。</summary>
        public YokiFrameGodotLiveBehaviourHost Host { get; internal set; }

        internal Func<string, string, object[], object> LiveInvoker;
        public T GetNode<T>(NodePath path) where T : Node => Node.GetNode<T>(path);

        /// <summary>延迟调用一次；宿主停止或故障后不再触发。</summary>
        /// <param name="delaySeconds">延迟秒数；非有限值按 0 处理。</param>
        /// <param name="callback">延迟结束后调用的委托。</param>
        public void Delay(float delaySeconds, Action callback) => Host?.Delay(delaySeconds, callback);

        /// <summary>取消尚未触发的延迟调用。</summary>
        public void CancelDelay() => Host?.CancelDelay();

        public object CallLive(string id, string method, params object[] arguments)
        {
            if (LiveInvoker == null) throw new InvalidOperationException("Live behaviour is detached.");
            return LiveInvoker(id, method, arguments);
        }
    }
}
#endif
