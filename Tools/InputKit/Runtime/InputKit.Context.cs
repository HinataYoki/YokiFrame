using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// InputKit 的输入上下文栈和动作映射管理能力。
    /// </summary>
    public static partial class InputKit
    {
        private static readonly List<string> sEnabledActionMaps = new();
        private static InputContextStack sContextStack;

        /// <summary>
        /// 当前输入上下文变化事件。
        /// </summary>
        public static event Action<InputContext, InputContext> OnContextChanged;

        /// <summary>
        /// 当前栈顶输入上下文。
        /// </summary>
        public static InputContext CurrentContext => sContextStack != null ? sContextStack.Current : null;

        /// <summary>
        /// 当前输入上下文栈深度。
        /// </summary>
        public static int ContextDepth => sContextStack != null ? sContextStack.Depth : 0;

        /// <summary>
        /// 注册可通过名称入栈的输入上下文。
        /// </summary>
        public static void RegisterContext(InputContext context)
        {
            EnsureContextStackInitialized();
            sContextStack.Register(context);
        }

        /// <summary>
        /// 注销指定名称的输入上下文。
        /// </summary>
        public static void UnregisterContext(string contextName)
        {
            if (sContextStack != null)
                sContextStack.Unregister(contextName);
        }

        /// <summary>
        /// 将输入上下文压入栈顶。
        /// </summary>
        public static void PushContext(InputContext context)
        {
            EnsureContextStackInitialized();
            sContextStack.Push(context);
            ApplyCurrentContext();
        }

        /// <summary>
        /// 将已注册的输入上下文压入栈顶。
        /// </summary>
        public static void PushContext(string contextName)
        {
            EnsureContextStackInitialized();
            sContextStack.Push(contextName);
            ApplyCurrentContext();
        }

        /// <summary>
        /// 弹出当前栈顶输入上下文。
        /// </summary>
        public static InputContext PopContext()
        {
            if (sContextStack == null)
                return null;

            var context = sContextStack.Pop();
            ApplyCurrentContext();
            return context;
        }

        /// <summary>
        /// 弹出输入上下文直到指定上下文位于栈顶。
        /// </summary>
        public static void PopToContext(string contextName)
        {
            if (sContextStack == null)
                return;

            sContextStack.PopTo(contextName);
            ApplyCurrentContext();
        }

        /// <summary>
        /// 清空输入上下文栈。
        /// </summary>
        public static void ClearContextStack()
        {
            if (sContextStack == null)
                return;

            sContextStack.Clear();
            DisableAllActionMaps();
        }

        /// <summary>
        /// 判断当前上下文是否阻挡指定动作。
        /// </summary>
        public static bool IsActionBlocked(string actionName) =>
            sContextStack != null && sContextStack.IsActionBlocked(actionName);

        /// <summary>
        /// 判断上下文栈内是否包含指定上下文。
        /// </summary>
        public static bool HasContext(string contextName) =>
            sContextStack != null && sContextStack.Contains(contextName);

        /// <summary>
        /// 切换到单个动作映射。
        /// </summary>
        public static void SwitchActionMap(string mapName)
        {
            if (string.IsNullOrEmpty(mapName))
            {
                DisableAllActionMaps();
                return;
            }

            EnableActionMaps(mapName);
        }

        /// <summary>
        /// 启用一组动作映射。
        /// </summary>
        public static void EnableActionMaps(params string[] mapNames)
        {
            sEnabledActionMaps.Clear();
            if (mapNames != null)
            {
                for (var i = 0; i < mapNames.Length; i++)
                {
                    if (!string.IsNullOrEmpty(mapNames[i]))
                        sEnabledActionMaps.Add(mapNames[i]);
                }
            }

            ApplyActionMapsToBackend();
        }

        /// <summary>
        /// 禁用全部动作映射。
        /// </summary>
        public static void DisableAllActionMaps()
        {
            sEnabledActionMaps.Clear();
            ApplyActionMapsToBackend();
        }

        /// <summary>
        /// 获取当前启用的动作映射列表。
        /// </summary>
        public static IReadOnlyList<string> GetEnabledActionMaps() => sEnabledActionMaps;

        private static void EnsureContextStackInitialized()
        {
            if (sContextStack != null)
                return;

            sContextStack = new();
            sContextStack.OnContextChanged += HandleContextChanged;
        }

        private static void ApplyCurrentContext()
        {
            var context = CurrentContext;
            if (context == null)
            {
                DisableAllActionMaps();
                return;
            }

            EnableActionMaps(context.EnabledActionMaps);
        }

        private static void ApplyActionMapsToBackend()
        {
            if (sBackend != null)
                sBackend.SetEnabledActionMaps(sEnabledActionMaps);
        }

        private static void HandleContextChanged(InputContext oldContext, InputContext newContext)
        {
            OnContextChanged?.Invoke(oldContext, newContext);
        }
    }
}
