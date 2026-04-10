namespace YokiFrame
{
    public class BindCodeInfo
    {
        /// <summary>
        ///  字段名称 例如: Btn
        /// </summary>
        public string Name;
        /// <summary>
        /// 类型名称 例如: UnityEngine.UI.Button
        /// </summary>
        public string Type;
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
        public BindType Bind;
        /// <summary>
        /// 成员字典
        /// </summary>
        public System.Collections.Generic.Dictionary<string, BindCodeInfo> MemberDic = new();
        /// <summary>
        /// 自身引用
        /// </summary>
        public UnityEngine.GameObject Self;
        /// <summary>
        /// 绑定组件
        /// </summary>
        public IBind BindScript;
        /// <summary>
        /// 重复元素
        /// </summary>
        public bool RepeatElement;
        /// <summary>
        /// 顺序
        /// </summary>
        public int order;
    }
}