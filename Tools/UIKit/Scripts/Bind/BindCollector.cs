using UnityEngine;

namespace YokiFrame
{
    public class BindCollector
    {
        /// <summary>
        /// 递归搜索绑定信息
        /// </summary>
        /// <param name="curTrans">当前Trans</param>
        /// <param name="fullName">当前Trans全路径</param>
        /// <param name="bindCodeInfo">绑定信息</param>
        public static void SearchBinds(Transform curTrans, string fullName, BindCodeInfo bindCodeInfo = null)
        {
            foreach (Transform child in curTrans)
            {
                string nextFullName = $"{fullName}/{child.name}";

                if (child.TryGetComponent<AbstractBind>(out var bind))
                {
                    // 绑定为叶子节点，直接跳过
                    if (bind.Bind is BindType.Leaf) continue;
                    
                    // 获取类型，如果为空则自动推断
                    var bindType = GetBindType(bind);
                    if (string.IsNullOrEmpty(bindType))
                    {
                        Debug.LogError($"Bind 组件的 Type 为空: {nextFullName}", child);
                        continue;
                    }
                    
                    // 获取名称，如果为空则使用 GameObject 名称
                    var bindName = string.IsNullOrEmpty(bind.Name) ? child.name : bind.Name;
                    
                    // 进行成员命名查重检查
                    var repreat = bindCodeInfo.MemberDic.ContainsKey(bindName);
                    if (repreat && bind.Bind is BindType.Member)
                    {
                        Debug.LogError($"Repaet {BindType.Member} Name: {bindName} for {bindCodeInfo.MemberDic[bindName].PathToRoot}", child);
                        continue;
                    }
                    // Component下不能有Element元素，Element必须归属于Panel
                    if (bindCodeInfo.Bind is BindType.Component && bind.Bind is BindType.Element)
                    {
                        Debug.LogWarning("Component组件下不持支定义Element元素 ", bind.Transform);
                    }
                    else
                    {
                        var order = bindCodeInfo.MemberDic.Count + 1;
                        var newBindInfo = new BindCodeInfo()
                        {
                            Type = bindType,
                            Name = bindName,
                            Comment = bind.Comment,
                            PathToRoot = nextFullName,
                            Bind = bind.Bind,
                            Self = child.gameObject,
                            BindScript = bind,
                            RepeatElement = repreat,
                            order = order,
                        };
                        bindCodeInfo.MemberDic.Add(repreat ? $"{bindName}{order}" : bindName, newBindInfo);
                        SearchBinds(child, nextFullName, bind.Bind is BindType.Member ? bindCodeInfo : newBindInfo);
                    }
                }
                else
                {
                    SearchBinds(child, nextFullName, bindCodeInfo);
                }
            }
        }
        
        /// <summary>
        /// 获取绑定类型，如果 Type 为空则自动推断
        /// </summary>
        private static string GetBindType(AbstractBind bind)
        {
            // 优先使用已设置的 Type
            if (!string.IsNullOrEmpty(bind.Type))
            {
                return bind.Type;
            }
            
            // 根据绑定类型自动推断
            switch (bind.Bind)
            {
                case BindType.Member:
                    // Member 类型：查找最后一个非 AbstractBind 组件
                    var components = bind.GetComponents<Component>();
                    for (int i = components.Length - 1; i >= 0; i--)
                    {
                        var comp = components[i];
                        if (comp != null && comp is not AbstractBind)
                        {
                            return comp.GetType().FullName;
                        }
                    }
                    break;
                    
                case BindType.Element:
                case BindType.Component:
                    // Element/Component 类型：使用 GameObject 名称
                    return bind.name;
            }
            
            return null;
        }
    }
}