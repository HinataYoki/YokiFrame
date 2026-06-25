namespace YokiFrame
{
    /// <summary>
    /// 输入上下文，用于控制动作映射和动作阻挡规则。
    /// </summary>
    public sealed class InputContext
    {
        /// <summary>
        /// 创建输入上下文。
        /// </summary>
        public InputContext(
            string contextName,
            int priority = 0,
            string[] enabledActionMaps = null,
            string[] blockedActions = null,
            bool blockAllLowerPriority = false)
        {
            ContextName = contextName;
            Priority = priority;
            EnabledActionMaps = CopyArray(enabledActionMaps);
            BlockedActions = CopyArray(blockedActions);
            BlockAllLowerPriority = blockAllLowerPriority;
        }

        /// <summary>上下文名称。</summary>
        public string ContextName { get; private set; }

        /// <summary>上下文优先级。</summary>
        public int Priority { get; private set; }

        /// <summary>该上下文启用的动作映射。</summary>
        public string[] EnabledActionMaps { get; private set; }

        /// <summary>该上下文阻挡的动作名。</summary>
        public string[] BlockedActions { get; private set; }

        /// <summary>是否阻挡所有更低优先级上下文。</summary>
        public bool BlockAllLowerPriority { get; private set; }

        /// <summary>
        /// 判断该上下文是否阻挡指定动作。
        /// </summary>
        public bool BlocksAction(string actionName)
        {
            if (string.IsNullOrEmpty(actionName))
                return false;

            for (var i = 0; i < BlockedActions.Length; i++)
            {
                if (BlockedActions[i] == actionName)
                    return true;
            }

            return false;
        }

        private static string[] CopyArray(string[] source)
        {
            if (source == null || source.Length == 0)
                return new string[0];

            var copy = new string[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }
    }
}
