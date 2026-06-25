using System;

namespace YokiFrame
{
    /// <summary>
    /// CodeGenKit 静态门面，提供常用代码输出入口。
    /// </summary>
    public static class CodeGenKit
    {
        public static RootCode Root()
        {
            return new RootCode();
        }

        public static CodeGenLineBuilder Lines(ICodeScope scope)
        {
            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            return new CodeGenLineBuilder(scope);
        }

        public static string GenerateToString(Action<RootCode> build, int initialCapacity = 1024)
        {
            if (build == null)
            {
                throw new ArgumentNullException(nameof(build));
            }

            RootCode root = new RootCode();
            build(root);
            using (StringCodeWriteKit writer = new StringCodeWriteKit(initialCapacity))
            {
                root.Gen(writer);
                return writer.ToString();
            }
        }

        public static void GenerateToFile(string filePath, Action<RootCode> build)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            }

            if (build == null)
            {
                throw new ArgumentNullException(nameof(build));
            }

            RootCode root = new RootCode();
            build(root);
            WriteToFile(filePath, root);
        }

        public static void WriteToFile(string filePath, RootCode root)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            }

            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            using (FileCodeWriteKit writer = new FileCodeWriteKit(filePath))
            {
                root.Gen(writer);
            }
        }
    }
}
