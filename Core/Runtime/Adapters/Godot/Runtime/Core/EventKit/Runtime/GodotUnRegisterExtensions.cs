#if GODOT
using System;
using Godot;
using YokiFrame;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 的 Godot 生命周期注销扩展。
    /// </summary>
    public static class GodotUnRegisterExtensions
    {
        public static T UnRegisterWhenNodeExiting<T>(this T self, Node node) where T : IUnRegister
        {
            if (node == null)
                return self;

            Action handler = null;
            handler = () =>
            {
                node.TreeExiting -= handler;
                self.UnRegister();
            };
            node.TreeExiting += handler;
            return self;
        }

        public static T UnRegisterWhenNodeDestroyed<T>(this T self, Node node) where T : IUnRegister
        {
            return self.UnRegisterWhenNodeExiting(node);
        }
    }
}
#endif
