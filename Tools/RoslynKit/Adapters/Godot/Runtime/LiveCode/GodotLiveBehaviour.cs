#if GODOT && TOOLS
using System;
using Godot;

namespace YokiFrame
{
    /// <summary>Managed prototype facade, not a Node. Only the precompiled host enters the scene tree.</summary>
    public abstract class GodotLiveBehaviour
    {
        public Node Node { get; internal set; }

        /// <summary>当前原型的预编译宿主；原型已分离时为 null，用于延迟调用等需要节点身份的能力。</summary>
        public GodotLiveBehaviourHost Host { get; internal set; }

        internal Func<string, string, object[], object> LiveInvoker;
        /// <summary>从附着节点查找子节点。原型尚未附着时由 Godot 抛出，不在这里伪造节点。</summary>
        /// <typeparam name="T">期望的节点类型。</typeparam>
        /// <param name="path">相对附着节点的路径。</param>
        /// <returns>命中的节点。</returns>
        public T GetNode<T>(NodePath path) where T : Node => Node.GetNode<T>(path);

        /// <summary>延迟调用一次；宿主未绑定、已释放或已停止时不再触发。</summary>
        /// <param name="delaySeconds">延迟秒数；非有限值按 0 处理。</param>
        /// <param name="callback">延迟结束后调用的委托。</param>
        public void Delay(float delaySeconds, Action callback)
        {
            if (!GodotObject.IsInstanceValid(Host)) return;
            Host.Delay(delaySeconds, callback);
        }

        /// <summary>取消尚未触发的延迟调用；宿主已释放时无操作。</summary>
        public void CancelDelay()
        {
            if (!GodotObject.IsInstanceValid(Host)) return;
            Host.CancelDelay();
        }

        /// <summary>调用另一个仍附着的原型。当前原型已分离时抛出，不把调用转到已释放宿主。</summary>
        /// <param name="id">目标原型标识。</param>
        /// <param name="method">目标方法名。</param>
        /// <param name="arguments">方法参数。</param>
        /// <returns>目标方法返回值。</returns>
        public object CallLive(string id, string method, params object[] arguments)
        {
            if (LiveInvoker == null) throw new InvalidOperationException("Live behaviour is detached.");
            return LiveInvoker(id, method, arguments);
        }
    }
}
#endif
