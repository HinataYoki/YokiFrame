using System.Collections.Generic;
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
                string nextFullName = $"{fullName}{child.name}/";
                if (child.TryGetComponent<IBind>(out var bind))
                {
                    // 绑定为叶子节点，直接跳过
                    if (bind.Bind is BindType.Leaf) continue;
                    // 进行成员命名查重检查
                    if (bindCodeInfo.MemberDic.ContainsKey(bind.Name))
                    {
                        Debug.LogError($"Repaet Name: {bind.Name} for {bindCodeInfo.MemberDic[bind.Name].PathToRoot}", child);
                    }
                    else
                    {
                        bindCodeInfo.MemberDic.Add(bind.Name, new BindCodeInfo
                        {
                            TypeName = bind.TypeName,
                            Name = bind.Name,
                            Comment = bind.Comment,
                            PathToRoot = nextFullName,
                            BindType = bind.Bind,
                            BindScript = bind,
                        });
                        SearchBinds(child, nextFullName, bind.Bind is BindType.Member ? bindCodeInfo : bindCodeInfo.MemberDic[bind.Name]);
                    }
                }
                else
                {
                    SearchBinds(child, nextFullName, bindCodeInfo);
                }
            }
        }
    }

    public class BindCodeInfo
    {
        /// <summary>
        /// 类型名称 例如: UnityEngine.UI.Button
        /// </summary>
        public string TypeName;
        /// <summary>
        ///  字段名称 例如: Btn
        /// </summary>
        public string Name;
        /// <summary>
        /// 字段注释 例如: 按钮
        /// </summary>
        public string Comment;
        /// <summary>
        /// 根节到当前节点的路径 例如: Panel/Content/Button
        /// </summary>
        public string PathToRoot;
        /// <summary>
        /// 绑定类型
        /// </summary>
        public BindType BindType;
        /// <summary>
        /// 成员字典
        /// </summary>
        public Dictionary<string, BindCodeInfo> MemberDic = new();
        /// <summary>
        /// 绑定组件
        /// </summary>
        public IBind BindScript;
    }
}