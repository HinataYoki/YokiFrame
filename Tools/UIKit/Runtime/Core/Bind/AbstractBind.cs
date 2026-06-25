#if !GODOT
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// UIKit 绑定组件的抽象基类，用于标记 GameObject 上需要暴露给 UI 面板的字段信息。
    /// 所有具体绑定组件（如 Bind、BindChild）均继承此类。
    /// </summary>
    public abstract class AbstractBind : MonoBehaviour, IBind
    {
        /// <summary>
        /// 绑定类型，决定该字段以成员、元素、组件还是叶子方式绑定。
        /// </summary>
        public BindType Bind = BindType.Member;
        /// <summary>
        /// 字段名称，用于代码生成时的变量名，例如 "BtnStart"。
        /// </summary>
        public string Name;
        /// <summary>
        /// 自动检测的组件类型全名，由 Inspector 从 GameObject 上挂载的组件列表中选取。
        /// </summary>
        public string AutoType;
        /// <summary>
        /// 自定义类型全名，当绑定类型为 Element 或 Component 时，用于手动指定目标类型。
        /// </summary>
        public string CustomType;
        /// <summary>
        /// 最终解析的类型全名，根据绑定类型自动从 AutoType 或 CustomType 中选取。
        /// </summary>
        public string Type;
        /// <summary>
        /// 字段注释，用于代码生成时的注释说明，例如 "开始按钮"。
        /// </summary>
        public string Comment;

        // 显式接口实现
        BindType IBind.Bind => Bind;
        string IBind.Name => Name;
        string IBind.Type => Type;
        string IBind.Comment => Comment;

        public Transform Transform => transform;
    }
}
#endif
