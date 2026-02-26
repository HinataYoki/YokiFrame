namespace YokiFrame
{
    /// <summary>
    /// 注释类型
    /// </summary>
    public enum CommentType
    {
        /// <summary>
        /// 单行注释 // xxx
        /// </summary>
        SingleLine,
        /// <summary>
        /// XML 摘要注释
        /// </summary>
        XmlSummary,
        /// <summary>
        /// XML param 注释
        /// </summary>
        XmlParam,
        /// <summary>
        /// XML returns 注释
        /// </summary>
        XmlReturns
    }

    /// <summary>
    /// 注释代码生成
    /// </summary>
    public class CommentCode : ICode
    {
        private readonly string mContent;
        private readonly CommentType mType;
        private readonly string mParamName;

        /// <summary>
        /// 创建注释代码
        /// </summary>
        /// <param name="content">注释内容</param>
        /// <param name="type">注释类型</param>
        /// <param name="paramName">参数名（仅 XmlParam 类型使用）</param>
        public CommentCode(string content, CommentType type = CommentType.SingleLine, string paramName = null)
        {
            mContent = content ?? string.Empty;
            mType = type;
            mParamName = paramName;
        }

        public void Gen(ICodeWriteKit writer)
        {
            switch (mType)
            {
                case CommentType.SingleLine:
                    writer.WriteLine($"// {mContent}");
                    break;

                case CommentType.XmlSummary:
                    writer.WriteLine("/// <summary>");
                    writer.WriteLine($"/// {mContent}");
                    writer.WriteLine("/// </summary>");
                    break;

                case CommentType.XmlParam:
                    writer.WriteLine($"/// <param name=\"{mParamName}\">{mContent}</param>");
                    break;

                case CommentType.XmlReturns:
                    writer.WriteLine($"/// <returns>{mContent}</returns>");
                    break;
            }
        }
    }

    public static partial class ICodeScopeExtensions
    {
        /// <summary>
        /// 添加单行注释
        /// </summary>
        public static ICodeScope Comment(this ICodeScope self, string content)
        {
            self.Codes.Add(new CommentCode(content, CommentType.SingleLine));
            return self;
        }

        /// <summary>
        /// 添加 XML 摘要注释
        /// </summary>
        public static ICodeScope Summary(this ICodeScope self, string content)
        {
            self.Codes.Add(new CommentCode(content, CommentType.XmlSummary));
            return self;
        }

        /// <summary>
        /// 添加 XML param 注释
        /// </summary>
        public static ICodeScope Param(this ICodeScope self, string paramName, string description)
        {
            self.Codes.Add(new CommentCode(description, CommentType.XmlParam, paramName));
            return self;
        }

        /// <summary>
        /// 添加 XML returns 注释
        /// </summary>
        public static ICodeScope Returns(this ICodeScope self, string description)
        {
            self.Codes.Add(new CommentCode(description, CommentType.XmlReturns));
            return self;
        }

        /// <summary>
        /// 添加 region 块
        /// </summary>
        public static ICodeScope Region(this ICodeScope self, string regionName, System.Action<ICodeScope> content)
        {
            self.Codes.Add(new CommentCode($"#region {regionName}"));
            self.EmptyLine();
            content?.Invoke(self);
            self.EmptyLine();
            self.Codes.Add(new CommentCode("#endregion"));
            return self;
        }
    }
}
