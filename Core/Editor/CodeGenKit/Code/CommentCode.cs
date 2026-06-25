using System;

namespace YokiFrame
{
    public enum CommentType
    {
        SingleLine,
        XmlSummary,
        XmlParam,
        XmlReturns
    }

    public sealed class CommentCode : ICode
    {
        private readonly string content;
        private readonly CommentType type;
        private readonly string paramName;

        public CommentCode(string content, CommentType type = CommentType.SingleLine, string paramName = null)
        {
            this.content = content ?? string.Empty;
            this.type = type;
            this.paramName = paramName;
        }

        public void Gen(ICodeWriteKit writer)
        {
            switch (type)
            {
                case CommentType.XmlSummary:
                    writer.WriteLine("/// <summary>");
                    writer.WriteLine("/// " + content);
                    writer.WriteLine("/// </summary>");
                    break;
                case CommentType.XmlParam:
                    writer.WriteFormatLine("/// <param name=\"{0}\">{1}</param>", paramName, content);
                    break;
                case CommentType.XmlReturns:
                    writer.WriteLine("/// <returns>" + content + "</returns>");
                    break;
                default:
                    writer.WriteLine("// " + content);
                    break;
            }
        }
    }

    public static partial class ICodeScopeExtensions
    {
        public static ICodeScope Comment(this ICodeScope self, string content)
        {
            self.Codes.Add(new CommentCode(content));
            return self;
        }

        public static ICodeScope Summary(this ICodeScope self, string content)
        {
            self.Codes.Add(new CommentCode(content, CommentType.XmlSummary));
            return self;
        }

        public static ICodeScope Param(this ICodeScope self, string paramName, string description)
        {
            self.Codes.Add(new CommentCode(description, CommentType.XmlParam, paramName));
            return self;
        }

        public static ICodeScope Returns(this ICodeScope self, string description)
        {
            self.Codes.Add(new CommentCode(description, CommentType.XmlReturns));
            return self;
        }

        public static ICodeScope Region(this ICodeScope self, string regionName, Action<ICodeScope> content)
        {
            self.Codes.Add(new CustomCode("#region " + regionName));
            self.EmptyLine();
            if (content != null)
            {
                content(self);
            }
            self.EmptyLine();
            self.Codes.Add(new CustomCode("#endregion"));
            return self;
        }
    }
}
