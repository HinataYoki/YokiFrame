namespace YokiFrame
{
    public interface IBind
    {
        /// <summary>
        /// 绑定类型
        /// </summary>
        BindType Bind { get; }
        /// <summary>
        ///  字段名称 例如: Btn
        /// </summary>
        string Name { get; }
        /// <summary>
        /// 类型名称 例如: UnityEngine.UI.Button
        /// </summary>
        string Type { get; }
        /// <summary>
        /// 字段注释 例如: 按钮
        /// </summary>
        string Comment { get; }

        UnityEngine.Transform Transform { get; }
    }
}