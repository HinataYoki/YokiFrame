using System;

namespace YokiFrame
{
    /// <summary>
    /// 可选 Tools Kit 的命令处理器注册工具。
    /// Adapter 只声明 handler 类型名，Base 统一完成反射创建和接口校验，避免每个宿主重复写同一段胶水。
    /// </summary>
    public static class OptionalKitCommandHandlerRegistry
    {
        /// <summary>
        /// 通过类型名创建可选 Kit 命令处理器，并注册到指定 dispatcher。
        /// </summary>
        /// <param name="dispatcher">目标命令分发器。</param>
        /// <param name="handlerTypeName">命令处理器类型名，可以是程序集限定名或完整类型名。</param>
        /// <returns>成功创建并注册时返回 true。</returns>
        public static bool TryRegister(KitCommandDispatcher dispatcher, string handlerTypeName)
        {
            if (dispatcher == null)
            {
                throw new ArgumentNullException(nameof(dispatcher));
            }

            IKitCommandHandler handler;
            if (!TryCreate(handlerTypeName, out handler))
            {
                return false;
            }

            dispatcher.Register(handler);
            return true;
        }

        /// <summary>
        /// 通过类型名创建可选 Kit 命令处理器实例。
        /// </summary>
        /// <param name="handlerTypeName">命令处理器类型名，可以是程序集限定名或完整类型名。</param>
        /// <param name="handler">创建成功的命令处理器实例。</param>
        /// <returns>成功创建且类型实现 <see cref="IKitCommandHandler"/> 时返回 true。</returns>
        public static bool TryCreate(string handlerTypeName, out IKitCommandHandler handler)
        {
            handler = null;
            if (string.IsNullOrEmpty(handlerTypeName))
            {
                return false;
            }

            var handlerType = ResolveType(handlerTypeName);
            if (handlerType == null || !typeof(IKitCommandHandler).IsAssignableFrom(handlerType))
            {
                return false;
            }

            try
            {
                handler = Activator.CreateInstance(handlerType, true) as IKitCommandHandler;
                return handler != null;
            }
            catch
            {
                handler = null;
                return false;
            }
        }

        private static Type ResolveType(string handlerTypeName)
        {
            var handlerType = Type.GetType(handlerTypeName);
            if (handlerType != null)
            {
                return handlerType;
            }

            var commaIndex = handlerTypeName.IndexOf(',');
            var typeNameOnly = commaIndex >= 0 ? handlerTypeName.Substring(0, commaIndex).Trim() : handlerTypeName;
            if (string.IsNullOrEmpty(typeNameOnly))
            {
                return null;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                handlerType = assemblies[i].GetType(typeNameOnly, false);
                if (handlerType != null)
                {
                    return handlerType;
                }
            }

            return null;
        }
    }
}
