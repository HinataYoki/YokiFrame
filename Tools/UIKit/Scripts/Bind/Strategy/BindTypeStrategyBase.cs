using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 绑定类型策略基类 - 提供默认实现
    /// </summary>
    public abstract class BindTypeStrategyBase : IBindTypeStrategy
    {
        public abstract BindType Type { get; }
        public abstract string DisplayName { get; }
        public abstract bool RequiresClassFile { get; }
        public abstract bool CanContainChildren { get; }
        public virtual bool SupportsConversion => true;
        public virtual bool ShouldSkipCodeGen => false;

        public virtual string InferTypeName(AbstractBind bind)
        {
            // 默认使用 GameObject 名称
            return bind != null ? bind.name : null;
        }

        public virtual bool ValidateChild(BindType childType, out string reason)
        {
            reason = null;
            return true;
        }

        public virtual string GetFullTypeName(BindCodeInfo bindInfo, IBindCodeGenContext context)
        {
            return bindInfo.Type;
        }

        public virtual string GetScriptPath(BindCodeInfo bindInfo, IBindCodeGenContext context, bool isDesigner)
        {
            return null;
        }

        public virtual string GetNamespace(IBindCodeGenContext context)
        {
            return context.ScriptNamespace;
        }

        public virtual string GetBaseClassName()
        {
            return null;
        }

        /// <summary>
        /// 从组件列表中推断类型（查找最后一个非 AbstractBind 组件）
        /// </summary>
        protected string InferTypeFromComponents(AbstractBind bind)
        {
            if (bind == null) return null;

            var components = bind.GetComponents<Component>();
            for (int i = components.Length - 1; i >= 0; i--)
            {
                var comp = components[i];
                if (comp != null && comp is not AbstractBind)
                {
                    return comp.GetType().FullName;
                }
            }
            return null;
        }
    }
}
