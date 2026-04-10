using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 绑定策略注册表 - 管理所有 BindType 策略的注册和获取
    /// </summary>
    public static class BindStrategyRegistry
    {
        private static readonly Dictionary<BindType, IBindTypeStrategy> sStrategies = new(4);
        private static bool sInitialized;

        /// <summary>
        /// 确保策略已初始化
        /// </summary>
        private static void EnsureInitialized()
        {
            if (sInitialized) return;

            // 注册内置策略
            Register(new MemberBindStrategy());
            Register(new ElementBindStrategy());
            Register(new ComponentBindStrategy());
            Register(new LeafBindStrategy());

            sInitialized = true;
        }

        /// <summary>
        /// 注册策略
        /// </summary>
        /// <param name="strategy">策略实例</param>
        public static void Register(IBindTypeStrategy strategy)
        {
            if (strategy == null) return;
            sStrategies[strategy.Type] = strategy;
        }

        /// <summary>
        /// 获取指定类型的策略
        /// </summary>
        /// <param name="type">绑定类型</param>
        /// <returns>策略实例，未找到返回 null</returns>
        public static IBindTypeStrategy Get(BindType type)
        {
            EnsureInitialized();
            return sStrategies.TryGetValue(type, out var strategy) ? strategy : null;
        }

        /// <summary>
        /// 尝试获取指定类型的策略
        /// </summary>
        /// <param name="type">绑定类型</param>
        /// <param name="strategy">策略实例（输出）</param>
        /// <returns>是否成功获取</returns>
        public static bool TryGet(BindType type, out IBindTypeStrategy strategy)
        {
            EnsureInitialized();
            return sStrategies.TryGetValue(type, out strategy);
        }

        /// <summary>
        /// 获取所有已注册的策略
        /// </summary>
        public static IEnumerable<IBindTypeStrategy> GetAll()
        {
            EnsureInitialized();
            return sStrategies.Values;
        }

        /// <summary>
        /// 获取显示名称
        /// </summary>
        /// <param name="type">绑定类型</param>
        /// <returns>显示名称</returns>
        public static string GetDisplayName(BindType type)
        {
            var strategy = Get(type);
            return strategy?.DisplayName ?? type.ToString();
        }

        /// <summary>
        /// 检查是否需要生成类文件
        /// </summary>
        /// <param name="type">绑定类型</param>
        /// <returns>是否需要生成类文件</returns>
        public static bool RequiresClassFile(BindType type)
        {
            var strategy = Get(type);
            return strategy?.RequiresClassFile ?? false;
        }

        /// <summary>
        /// 检查是否应该跳过代码生成
        /// </summary>
        /// <param name="type">绑定类型</param>
        /// <returns>是否跳过</returns>
        public static bool ShouldSkipCodeGen(BindType type)
        {
            var strategy = Get(type);
            return strategy?.ShouldSkipCodeGen ?? false;
        }

        /// <summary>
        /// 验证子绑定是否允许
        /// </summary>
        /// <param name="parentType">父绑定类型</param>
        /// <param name="childType">子绑定类型</param>
        /// <param name="reason">不允许的原因（输出）</param>
        /// <returns>是否允许</returns>
        public static bool ValidateChild(BindType parentType, BindType childType, out string reason)
        {
            var strategy = Get(parentType);
            if (strategy == null)
            {
                reason = null;
                return true;
            }
            return strategy.ValidateChild(childType, out reason);
        }
    }
}
