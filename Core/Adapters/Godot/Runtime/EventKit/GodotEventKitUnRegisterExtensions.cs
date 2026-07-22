#if GODOT
using System;
using Godot;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 的 Godot Node 生命周期注销扩展。
    /// </summary>
    public static class GodotEventKitUnRegisterExtensions
    {
        /// <summary>
        /// 把注销令牌绑定到指定 Node 离开场景树时机。
        /// </summary>
        /// <typeparam name="T">注销令牌类型。</typeparam>
        /// <param name="self">要绑定生命周期的注销令牌。</param>
        /// <param name="node">用于承载生命周期回调的 Godot 节点。</param>
        /// <returns>原注销令牌，便于链式调用。</returns>
        public static T UnRegisterWhenNodeExiting<T>(this T self, Node node) where T : IUnRegister
        {
            if (node == null)
            {
                return self;
            }

            Action handler = null;
            handler = () =>
            {
                node.TreeExiting -= handler;
                self.UnRegister();
            };
            node.TreeExiting += handler;
            return self;
        }

        /// <summary>
        /// 把注销令牌绑定到指定 Node 的销毁语义；Godot 下复用离开场景树时机。
        /// </summary>
        /// <typeparam name="T">注销令牌类型。</typeparam>
        /// <param name="self">要绑定生命周期的注销令牌。</param>
        /// <param name="node">用于承载生命周期回调的 Godot 节点。</param>
        /// <returns>原注销令牌，便于链式调用。</returns>
        public static T UnRegisterWhenNodeDestroyed<T>(this T self, Node node) where T : IUnRegister
        {
            return self.UnRegisterWhenNodeExiting(node);
        }
    }
}
#endif
