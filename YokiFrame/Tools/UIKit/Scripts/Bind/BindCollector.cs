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
                if (child.TryGetComponent<IBind>(out var bind))
                {
                    // 绑定为叶子节点，直接跳过
                    if (bind.Bind is BindType.Leaf) continue;
                    // 进行成员命名查重检查
                    var repreat = bindCodeInfo.MemberDic.ContainsKey(bind.Name);
                    if (repreat && bind.Bind is BindType.Member)
                    {
                        Debug.LogError($"Repaet {BindType.Member} Name: {bind.Name} for {bindCodeInfo.MemberDic[bind.Name].PathToRoot}", child);
                        continue;
                    }
                    else
                    {
                        if (bindCodeInfo.BindType is BindType.Component && bind.Bind is BindType.Element)
                        {
                            Debug.LogWarning("Component组件下不持支定义Element元素 ", bind.Transform);
                        }
                        else
                        {
                            var newBindInfo = new BindCodeInfo()
                            {
                                TypeName = bind.TypeName,
                                Name = bind.Name,
                                Comment = bind.Comment,
                                PathToRoot = nextFullName,
                                BindType = bind.Bind,
                                Self = child.gameObject,
                                BindScript = bind,
                                RepeatElement = repreat,
                                order = bindCodeInfo.MemberDic.Count + 1,
                            };
                            bindCodeInfo.MemberDic.Add(repreat ? $"{bind.Name}{bindCodeInfo.MemberDic.Count + 1}" : bind.Name, newBindInfo);
                            SearchBinds(child, nextFullName, bind.Bind is BindType.Member ? bindCodeInfo : newBindInfo);
                        }
                    }
                }
                else
                {
                    SearchBinds(child, nextFullName, bindCodeInfo);
                }
            }
        }
    }
}