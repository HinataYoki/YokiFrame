using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 绑定收集器 - 递归搜索并收集绑定信息
    /// </summary>
    public static class BindCollector
    {
        /// <summary>
        /// 递归搜索绑定信息
        /// </summary>
        /// <param name="curTrans">当前 Transform</param>
        /// <param name="fullName">当前 Transform 全路径</param>
        /// <param name="bindCodeInfo">绑定信息</param>
        public static void SearchBinds(Transform curTrans, string fullName, BindCodeInfo bindCodeInfo = null)
        {
            foreach (Transform child in curTrans)
            {
                string nextFullName = $"{fullName}/{child.name}";

                if (child.TryGetComponent<AbstractBind>(out var bind))
                {
                    ProcessBind(bind, child, nextFullName, bindCodeInfo);
                }
                else
                {
                    SearchBinds(child, nextFullName, bindCodeInfo);
                }
            }
        }

        /// <summary>
        /// 处理单个绑定组件
        /// </summary>
        private static void ProcessBind(AbstractBind bind, Transform child, string nextFullName, BindCodeInfo parentInfo)
        {
            var strategy = BindStrategyRegistry.Get(bind.Bind);
            if (strategy == null) return;

            // 跳过不需要代码生成的类型
            if (strategy.ShouldSkipCodeGen) return;

            // 获取类型，如果为空则使用策略推断
            var bindType = GetBindType(bind, strategy);
            if (string.IsNullOrEmpty(bindType))
            {
                Debug.LogError($"Bind 组件的 Type 为空: {nextFullName}", child);
                return;
            }

            // 获取名称，如果为空则使用 GameObject 名称
            var bindName = string.IsNullOrEmpty(bind.Name) ? child.name : bind.Name;

            // 进行成员命名查重检查
            var repeat = parentInfo.MemberDic.ContainsKey(bindName);
            if (repeat && bind.Bind is BindType.Member)
            {
                Debug.LogError($"重复的 {BindType.Member} 名称: {bindName}，已存在于 {parentInfo.MemberDic[bindName].PathToRoot}", child);
                return;
            }

            // 使用策略验证子绑定是否允许
            var parentStrategy = BindStrategyRegistry.Get(parentInfo.Bind);
            if (parentStrategy != null && !parentStrategy.ValidateChild(bind.Bind, out string reason))
            {
                Debug.LogWarning(reason, bind.Transform);
                return;
            }

            // 创建绑定信息
            var order = parentInfo.MemberDic.Count + 1;
            var newBindInfo = new BindCodeInfo
            {
                Type = bindType,
                Name = bindName,
                Comment = bind.Comment,
                PathToRoot = nextFullName,
                Bind = bind.Bind,
                Self = child.gameObject,
                BindScript = bind,
                RepeatElement = repeat,
                order = order,
            };

            // 添加到父级字典
            var key = repeat ? $"{bindName}{order}" : bindName;
            parentInfo.MemberDic.Add(key, newBindInfo);

            // 递归搜索子节点（Member 类型不创建新的作用域）
            var nextParent = strategy.CanContainChildren ? newBindInfo : parentInfo;
            SearchBinds(child, nextFullName, nextParent);
        }

        /// <summary>
        /// 获取绑定类型，如果 Type 为空则使用策略推断
        /// </summary>
        private static string GetBindType(AbstractBind bind, IBindTypeStrategy strategy)
        {
            // 优先使用已设置的 Type
            if (!string.IsNullOrEmpty(bind.Type))
            {
                return bind.Type;
            }

            // 使用策略推断类型
            return strategy.InferTypeName(bind);
        }
    }
}