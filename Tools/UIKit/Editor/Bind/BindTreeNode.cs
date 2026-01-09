using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 绑定树节点 - 用于可视化显示绑定层级结构
    /// </summary>
    public class BindTreeNode
    {
        #region 基本信息

        /// <summary>
        /// 节点名称（字段名）
        /// </summary>
        public string Name;

        /// <summary>
        /// 从 Panel 根节点到此节点的路径
        /// </summary>
        public string Path;

        /// <summary>
        /// 绑定类型
        /// </summary>
        public BindType Type;

        /// <summary>
        /// 绑定的组件类型名称
        /// </summary>
        public string ComponentTypeName;

        #endregion

        #region Unity 引用

        /// <summary>
        /// 关联的 GameObject
        /// </summary>
        public GameObject GameObject;

        /// <summary>
        /// 关联的 Bind 组件
        /// </summary>
        public AbstractBind Bind;

        #endregion

        #region 树结构

        /// <summary>
        /// 父节点
        /// </summary>
        public BindTreeNode Parent;

        /// <summary>
        /// 子节点列表
        /// </summary>
        public List<BindTreeNode> Children;

        /// <summary>
        /// 节点深度（根节点为 0）
        /// </summary>
        public int Depth;

        #endregion

        #region 验证结果

        /// <summary>
        /// 此节点的验证结果列表
        /// </summary>
        public List<BindValidationResult> ValidationResults;

        /// <summary>
        /// 是否存在错误级别的验证问题
        /// </summary>
        public bool HasErrors
        {
            get
            {
                if (ValidationResults == null) return false;
                foreach (var result in ValidationResults)
                {
                    if (result.Level == BindValidationLevel.Error)
                        return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 是否存在警告级别的验证问题
        /// </summary>
        public bool HasWarnings
        {
            get
            {
                if (ValidationResults == null) return false;
                foreach (var result in ValidationResults)
                {
                    if (result.Level == BindValidationLevel.Warning)
                        return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 子树中是否存在错误（包括自身和所有后代）
        /// </summary>
        public bool HasErrorsInSubtree
        {
            get
            {
                if (HasErrors) return true;
                if (Children == null) return false;
                foreach (var child in Children)
                {
                    if (child.HasErrorsInSubtree)
                        return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 子树中是否存在警告（包括自身和所有后代）
        /// </summary>
        public bool HasWarningsInSubtree
        {
            get
            {
                if (HasWarnings) return true;
                if (Children == null) return false;
                foreach (var child in Children)
                {
                    if (child.HasWarningsInSubtree)
                        return true;
                }
                return false;
            }
        }

        #endregion

        #region 构造函数

        public BindTreeNode()
        {
            Children = new List<BindTreeNode>(4);
            ValidationResults = new List<BindValidationResult>(2);
        }

        /// <summary>
        /// 从 Bind 组件创建节点
        /// </summary>
        /// <param name="bind">Bind 组件</param>
        /// <param name="parent">父节点</param>
        public BindTreeNode(AbstractBind bind, BindTreeNode parent = null) : this()
        {
            if (bind == null) return;

            Bind = bind;
            GameObject = bind.gameObject;
            Name = bind.Name;
            Type = bind.Bind;
            ComponentTypeName = bind.Type;
            Parent = parent;
            Depth = parent?.Depth + 1 ?? 0;

            // 计算路径
            Path = CalculatePath();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 添加子节点
        /// </summary>
        /// <param name="child">子节点</param>
        public void AddChild(BindTreeNode child)
        {
            if (child == null) return;
            child.Parent = this;
            child.Depth = Depth + 1;
            Children.Add(child);
        }

        /// <summary>
        /// 添加验证结果
        /// </summary>
        /// <param name="result">验证结果</param>
        public void AddValidationResult(in BindValidationResult result)
        {
            ValidationResults.Add(result);
        }

        /// <summary>
        /// 清除验证结果
        /// </summary>
        public void ClearValidationResults()
        {
            ValidationResults.Clear();
        }

        /// <summary>
        /// 获取子节点数量（不包括后代）
        /// </summary>
        public int ChildCount => Children?.Count ?? 0;

        /// <summary>
        /// 是否为叶子节点
        /// </summary>
        public bool IsLeaf => Children == null || Children.Count == 0;

        /// <summary>
        /// 是否为根节点
        /// </summary>
        public bool IsRoot => Parent == null;

        /// <summary>
        /// 计算从根节点到此节点的路径
        /// </summary>
        private string CalculatePath()
        {
            if (GameObject == null) return string.Empty;

            // 使用 StringBuilder 避免字符串拼接
            var pathParts = new List<string>(8);
            var current = GameObject.transform;

            while (current != null)
            {
                pathParts.Add(current.name);
                current = current.parent;
            }

            // 反转并拼接
            pathParts.Reverse();
            return string.Join("/", pathParts);
        }

        /// <summary>
        /// 递归统计子树中的节点数量
        /// </summary>
        /// <param name="includeThis">是否包含自身</param>
        public int CountNodes(bool includeThis = true)
        {
            int count = includeThis ? 1 : 0;
            if (Children != null)
            {
                foreach (var child in Children)
                {
                    count += child.CountNodes(true);
                }
            }
            return count;
        }

        /// <summary>
        /// 按类型统计子树中的节点数量
        /// </summary>
        /// <param name="type">绑定类型</param>
        /// <param name="includeThis">是否包含自身</param>
        public int CountNodesByType(BindType type, bool includeThis = true)
        {
            int count = 0;
            if (includeThis && Type == type)
                count = 1;

            if (Children != null)
            {
                foreach (var child in Children)
                {
                    count += child.CountNodesByType(type, true);
                }
            }
            return count;
        }

        #endregion

        public override string ToString()
            => $"{Name} ({Type}) @ {Path}";
    }
}
