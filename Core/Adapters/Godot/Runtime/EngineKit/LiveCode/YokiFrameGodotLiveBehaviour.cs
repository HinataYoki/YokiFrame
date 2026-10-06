#if GODOT && TOOLS
using System;
using Godot;

namespace YokiFrame
{
    /// <summary>Managed prototype facade, not a Node. Only the precompiled host enters the scene tree.</summary>
    public abstract class YokiFrameGodotLiveBehaviour
    {
        public Node Node { get; internal set; }
        internal Func<string, string, object[], object> LiveInvoker;
        public T GetNode<T>(NodePath path) where T : Node => Node.GetNode<T>(path);
        public object CallLive(string id, string method, params object[] arguments)
        {
            if (LiveInvoker == null) throw new InvalidOperationException("Live behaviour is detached.");
            return LiveInvoker(id, method, arguments);
        }
    }
}
#endif
