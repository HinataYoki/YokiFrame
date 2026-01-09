using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 绑定代码预览生成器 - 生成单个绑定的代码预览
    /// </summary>
    public static class BindCodePreview
    {
        #region 常量

        /// <summary>
        /// 缩进字符串
        /// </summary>
        private const string INDENT = "    ";

        #endregion

        #region 公共方法

        /// <summary>
        /// 生成单个绑定的字段声明代码预览
        /// </summary>
        /// <param name="bind">Bind 组件</param>
        /// <param name="includeAttribute">是否包含 SerializeField 特性</param>
        /// <param name="includeComment">是否包含 XML 注释</param>
        /// <returns>代码预览字符串</returns>
        public static string GenerateFieldPreview(
            AbstractBind bind,
            bool includeAttribute = true,
            bool includeComment = true)
        {
            if (bind == null)
                return string.Empty;

            return GenerateFieldPreview(
                bind.Name,
                bind.Type,
                bind.Comment,
                includeAttribute,
                includeComment);
        }

        /// <summary>
        /// 生成单个绑定的字段声明代码预览
        /// </summary>
        /// <param name="fieldName">字段名称</param>
        /// <param name="typeName">类型名称</param>
        /// <param name="comment">注释</param>
        /// <param name="includeAttribute">是否包含 SerializeField 特性</param>
        /// <param name="includeComment">是否包含 XML 注释</param>
        /// <returns>代码预览字符串</returns>
        public static string GenerateFieldPreview(
            string fieldName,
            string typeName,
            string comment = null,
            bool includeAttribute = true,
            bool includeComment = true)
        {
            if (string.IsNullOrEmpty(fieldName))
                return string.Empty;

            // 处理类型名称
            string shortTypeName = GetShortTypeName(typeName);
            if (string.IsNullOrEmpty(shortTypeName))
                shortTypeName = "GameObject";

            var builder = new StringBuilder(128);

            // XML 注释
            if (includeComment && !string.IsNullOrEmpty(comment))
            {
                builder.AppendLine("/// <summary>");
                builder.Append("/// ").AppendLine(comment);
                builder.AppendLine("/// </summary>");
            }

            // SerializeField 特性
            if (includeAttribute)
            {
                builder.AppendLine("[SerializeField]");
            }

            // 字段声明
            builder.Append("public ").Append(shortTypeName).Append(' ').Append(fieldName).Append(';');

            return builder.ToString();
        }

        /// <summary>
        /// 生成带缩进的字段声明代码预览
        /// </summary>
        /// <param name="bind">Bind 组件</param>
        /// <param name="indentLevel">缩进级别</param>
        /// <returns>带缩进的代码预览</returns>
        public static string GenerateFieldPreviewIndented(AbstractBind bind, int indentLevel = 2)
        {
            if (bind == null)
                return string.Empty;

            string preview = GenerateFieldPreview(bind);
            return AddIndent(preview, indentLevel);
        }

        /// <summary>
        /// 生成绑定树节点的代码预览
        /// </summary>
        /// <param name="node">绑定树节点</param>
        /// <returns>代码预览字符串</returns>
        public static string GenerateNodePreview(BindTreeNode node)
        {
            if (node == null || node.Bind == null)
                return string.Empty;

            return GenerateFieldPreview(node.Bind);
        }

        /// <summary>
        /// 生成 Element 类的代码预览
        /// </summary>
        /// <param name="elementName">Element 名称</param>
        /// <param name="node">绑定树节点</param>
        /// <param name="namespaceName">命名空间</param>
        /// <returns>Element 类代码预览</returns>
        public static string GenerateElementClassPreview(
            string elementName,
            BindTreeNode node,
            string namespaceName)
        {
            if (string.IsNullOrEmpty(elementName) || node == null)
                return string.Empty;

            var builder = new StringBuilder(512);

            // 命名空间
            builder.Append("namespace ").AppendLine(namespaceName);
            builder.AppendLine("{");

            // 类声明
            builder.Append(INDENT).Append("public partial class ").AppendLine(elementName);
            builder.Append(INDENT).AppendLine("{");

            // 成员字段
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    if (child.Type == BindType.Member)
                    {
                        string fieldPreview = GenerateFieldPreview(
                            child.Name,
                            child.ComponentTypeName,
                            null,
                            true,
                            false);
                        builder.Append(INDENT).Append(INDENT).AppendLine(fieldPreview);
                    }
                }
            }

            builder.Append(INDENT).AppendLine("}");
            builder.AppendLine("}");

            return builder.ToString();
        }

        /// <summary>
        /// 生成 Component 类的代码预览
        /// </summary>
        /// <param name="componentName">Component 名称</param>
        /// <param name="node">绑定树节点</param>
        /// <returns>Component 类代码预览</returns>
        public static string GenerateComponentClassPreview(string componentName, BindTreeNode node)
        {
            if (string.IsNullOrEmpty(componentName) || node == null)
                return string.Empty;

            var builder = new StringBuilder(256);

            // 类声明
            builder.Append("public partial class ").AppendLine(componentName);
            builder.AppendLine("{");

            // 成员字段
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    if (child.Type == BindType.Member)
                    {
                        string fieldPreview = GenerateFieldPreview(
                            child.Name,
                            child.ComponentTypeName,
                            null,
                            true,
                            false);
                        builder.Append(INDENT).AppendLine(fieldPreview);
                    }
                }
            }

            builder.AppendLine("}");

            return builder.ToString();
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 获取类型的短名称（去除命名空间）
        /// </summary>
        private static string GetShortTypeName(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
                return null;

            int lastDot = fullTypeName.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < fullTypeName.Length - 1)
            {
                return fullTypeName.Substring(lastDot + 1);
            }

            return fullTypeName;
        }

        /// <summary>
        /// 为代码添加缩进
        /// </summary>
        private static string AddIndent(string code, int level)
        {
            if (string.IsNullOrEmpty(code) || level <= 0)
                return code;

            string indent = new string(' ', level * 4);
            var lines = code.Split('\n');

            var builder = new StringBuilder(code.Length + lines.Length * level * 4);
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                    builder.AppendLine();

                if (!string.IsNullOrWhiteSpace(lines[i]))
                {
                    builder.Append(indent).Append(lines[i].TrimEnd('\r'));
                }
            }

            return builder.ToString();
        }

        #endregion
    }
}
