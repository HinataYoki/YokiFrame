#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 管理 UIKit Editor 固定 BindType 策略，并向扫描器提供确定性解析。
    /// </summary>
    internal static class UIKitBindStrategyRegistry
    {
        private static readonly Dictionary<BindType, IUIKitBindStrategy> sByLegacyType = new();
        private static bool sInitialized;

        /// <summary>
        /// 按 BindType 解析固定内置策略。
        /// </summary>
        /// <param name="bind">待解析 Bind 组件。</param>
        /// <param name="strategy">成功时输出策略。</param>
        /// <param name="error">失败时输出原因。</param>
        /// <returns>找到策略时返回 true。</returns>
        internal static bool TryGet(
            AbstractBind bind,
            out IUIKitBindStrategy strategy,
            out string error)
        {
            if (bind == default)
            {
                strategy = default;
                error = "Bind 组件为空。";
                return false;
            }

            EnsureInitialized();
            if (sByLegacyType.TryGetValue(bind.Bind, out strategy))
            {
                error = string.Empty;
                return true;
            }

            error = "不支持的 BindType: " + bind.Bind;
            return false;
        }

        /// <summary>
        /// 清空并重新安装内置策略，仅供 Editor 测试隔离静态状态。
        /// </summary>
        internal static void ResetForTests()
        {
            sByLegacyType.Clear();
            sInitialized = false;
            EnsureInitialized();
        }

        /// <summary>按固定 BindType 获取内置策略。</summary>
        internal static bool TryGetBuiltIn(BindType bindType, out IUIKitBindStrategy strategy)
        {
            EnsureInitialized();
            return sByLegacyType.TryGetValue(bindType, out strategy);
        }

        /// <summary>首次访问时先安装全部内置策略。</summary>
        private static void EnsureInitialized()
        {
            if (sInitialized) return;
            sInitialized = true;
            RegisterCore(new BuiltInStrategy(BindType.Member));
            RegisterCore(new BuiltInStrategy(BindType.Element));
            RegisterCore(new BuiltInStrategy(BindType.Component));
            RegisterCore(new BuiltInStrategy(BindType.Leaf));
        }

        /// <summary>把一个框架内置策略加入固定 BindType 表。</summary>
        private static void RegisterCore(IUIKitBindStrategy strategy)
        {
            sByLegacyType.Add(strategy.LegacyType, strategy);
        }

        /// <summary>实现四种兼容 BindType 的确定性解析与层级规则。</summary>
        private sealed class BuiltInStrategy : IUIKitBindStrategy
        {
            /// <summary>创建一个内置兼容策略。</summary>
            internal BuiltInStrategy(BindType type)
            {
                LegacyType = type;
            }

            /// <inheritdoc />
            public BindType LegacyType { get; }

            /// <inheritdoc />
            public UIKitBindOutputKind OutputKind => LegacyType switch
            {
                BindType.Member    => UIKitBindOutputKind.Member,
                BindType.Element   => UIKitBindOutputKind.Element,
                BindType.Component => UIKitBindOutputKind.Component,
                BindType.Leaf      => UIKitBindOutputKind.Marker,
                _                  => throw new InvalidOperationException("不支持的 BindType: " + LegacyType),
            };

            /// <inheritdoc />
            public bool CanContainChildren =>
                LegacyType == BindType.Element || LegacyType == BindType.Component;

            /// <inheritdoc />
            public bool TryResolve(
                AbstractBind bind,
                out string typeName,
                out UnityEngine.Object target,
                out string error)
            {
                if (LegacyType == BindType.Leaf)
                {
                    typeName = string.Empty;
                    target = default;
                    error = string.Empty;
                    return true;
                }

                if (LegacyType == BindType.Member)
                    return TryResolveMember(bind, out typeName, out target, out error);

                typeName = FirstNonEmpty(
                    bind.CustomType,
                    bind.Type,
                    bind.Name,
                    bind.gameObject.name);
                target = default;
                error = string.IsNullOrWhiteSpace(typeName) ? "生成类型名不能为空。" : string.Empty;
                return error.Length == 0;
            }

            /// <inheritdoc />
            public bool TryValidateChild(BindType childType, out string error)
            {
                if (LegacyType == BindType.Component && childType == BindType.Element)
                {
                    error = "Component 下不能定义 Element；Element 必须归属于 Panel。";
                    return false;
                }

                error = string.Empty;
                return true;
            }

            /// <summary>优先显式组件，再按旧类型字段确定性恢复 Member 引用。</summary>
            private static bool TryResolveMember(
                AbstractBind bind,
                out string typeName,
                out UnityEngine.Object target,
                out string error)
            {
                if (bind.Target != default)
                {
                    if (bind.Target.gameObject != bind.gameObject)
                    {
                        typeName = string.Empty;
                        target = default;
                        error = "Member 目标必须位于 Bind 所在 GameObject。";
                        return false;
                    }

                    target = bind.Target;
                    typeName = bind.Target.GetType().FullName;
                    error = string.Empty;
                    return true;
                }

                string configuredType = FirstNonEmpty(bind.Type, bind.AutoType);
                if (IsGameObjectType(configuredType))
                {
                    typeName = typeof(GameObject).FullName;
                    target = bind.gameObject;
                    error = string.Empty;
                    return true;
                }

                Component matched = FindComponent(bind, configuredType);
                if (matched != default)
                {
                    typeName = matched.GetType().FullName;
                    target = matched;
                    error = string.Empty;
                    return true;
                }

                typeName = string.Empty;
                target = default;
                error = string.IsNullOrWhiteSpace(configuredType)
                    ? "Member 必须在 Inspector 中显式选择组件。"
                    : "节点上不存在配置的 Member 组件: " + configuredType;
                return false;
            }

            /// <summary>按完整类型名或短名称查找同节点组件。</summary>
            private static Component FindComponent(AbstractBind bind, string typeName)
            {
                if (string.IsNullOrWhiteSpace(typeName)) return default;
                Component[] components = bind.GetComponents<Component>();
                for (var index = 0; index < components.Length; index++)
                {
                    Component component = components[index];
                    if (component == default || component is AbstractBind) continue;
                    Type componentType = component.GetType();
                    if (string.Equals(componentType.FullName, typeName, StringComparison.Ordinal)
                        || string.Equals(componentType.Name, typeName, StringComparison.Ordinal))
                        return component;
                }

                return default;
            }

            /// <summary>判断旧类型文本是否表示 GameObject。</summary>
            private static bool IsGameObjectType(string typeName)
            {
                return string.Equals(typeName, nameof(GameObject), StringComparison.Ordinal)
                    || string.Equals(typeName, typeof(GameObject).FullName, StringComparison.Ordinal);
            }

        /// <summary>返回两个候选配置中的首个非空值，避免扫描期间创建 params 数组。</summary>
        private static string FirstNonEmpty(string first, string second)
        {
            if (!string.IsNullOrWhiteSpace(first)) return first.Trim();
            return string.IsNullOrWhiteSpace(second) ? string.Empty : second.Trim();
        }

        /// <summary>返回四个候选配置中的首个非空值，保持旧字段恢复优先级。</summary>
        private static string FirstNonEmpty(string first, string second, string third, string fourth)
        {
            string firstPair = FirstNonEmpty(first, second);
            return !string.IsNullOrEmpty(firstPair)
                ? firstPair
                : FirstNonEmpty(third, fourth);
        }
        }
    }
}
#endif
