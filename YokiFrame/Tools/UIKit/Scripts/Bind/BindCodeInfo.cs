using System.Collections.Generic;

namespace YokiFrame
{
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