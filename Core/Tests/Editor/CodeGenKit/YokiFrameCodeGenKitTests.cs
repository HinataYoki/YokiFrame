using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 锁定 CodeGenKit 的结构化生成、确定性输出和事务文件提交契约。
    /// </summary>
    public sealed class YokiFrameCodeGenKitTests
    {
        /// <summary>
        /// 验证结构化 DSL 使用固定 LF 和 Tab 生成稳定源码。
        /// </summary>
        [Test]
        public void StructuredBuilderGeneratesDeterministicSource()
        {
            string source = CodeGenKit.GenerateToString(root =>
            {
                root.Using("System")
                    .EmptyLine()
                    .Namespace("Game.Generated", namespaceScope =>
                    {
                        namespaceScope.Class("PlayerView", null, true, false, classScope =>
                        {
                            classScope.PrivateField("int", "mScore", "0");
                            classScope.ReadonlyProperty("int", "Score", "mScore");
                        });
                    });
            });

            Assert.AreEqual(
                "using System;\n\nnamespace Game.Generated\n{\n\tpublic partial class PlayerView\n\t{\n\t\tprivate int mScore = 0;\n\t\tpublic int Score => mScore;\n\t}\n}\n",
                source);
        }

        /// <summary>
        /// 验证逐行构建器的尾部内容无需显式 Flush 也会进入结果。
        /// </summary>
        [Test]
        public void LineBuilderKeepsPendingTailWithoutExplicitFlush()
        {
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                string source = CodeGenKit.GenerateToString(root =>
                {
                    CodeGenLineBuilder lines = CodeGenKit.Lines(root);
                    lines.Append("public const double Value = ").Append(1.5d).Append(';');
                });

                Assert.AreEqual("public const double Value = 1.5;\n", source);
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }

        /// <summary>
        /// 验证 CodeGenKit 统一归属共享 Editor 程序集，没有恢复独立 Core Kit asmdef。
        /// </summary>
        [Test]
        public void PublicFacadeBelongsToSharedEditorAssembly()
        {
            Assert.AreEqual("YokiFrame.Editor", typeof(CodeGenKit).Assembly.GetName().Name);
        }

        /// <summary>
        /// 验证 XML 文档文本会转义特殊字符并保持有效 XML。
        /// </summary>
        [Test]
        public void XmlCommentsEscapeSpecialCharacters()
        {
            string source = CodeGenKit.GenerateToString(root => root
                .Summary("A < B & C > D")
                .Field("int", "Value", field => field.WithComment("A < B & C > D")));

            Assert.That(source, Does.Contain("/// A &lt; B &amp; C &gt; D"));
        }

        /// <summary>
        /// 验证非法 C# 标识符在构建阶段被明确拒绝。
        /// </summary>
        [Test]
        public void InvalidIdentifierIsRejected()
        {
            Assert.Throws<ArgumentException>(() =>
                CodeGenKit.GenerateToString(root => root.Namespace("Game.Invalid-Name", _ => { })));
        }

        /// <summary>
        /// 验证字段修饰符不会组合出无效 C# 声明。
        /// </summary>
        [Test]
        public void InvalidFieldModifierCombinationIsRejected()
        {
            Assert.Throws<InvalidOperationException>(() => CodeGenKit.GenerateToString(root => root
                .Field("int", "Value", field => field.WithModifiers(MemberModifier.Const | MemberModifier.Readonly))));
        }

        /// <summary>
        /// 验证表达式属性不能因后续 setter 配置而静默改变 getter 语义。
        /// </summary>
        [Test]
        public void ExpressionPropertyRejectsSetter()
        {
            Assert.Throws<InvalidOperationException>(() => new PropertyCode("int", "Value")
                .WithExpressionBody("mValue")
                .WithSetter(body => body.Custom("mValue = value;")));
        }

        /// <summary>
        /// 验证 SerializeField 快捷入口生成私有字段而不是公开字段。
        /// </summary>
        [Test]
        public void SerializeFieldShortcutGeneratesPrivateField()
        {
            string source = CodeGenKit.GenerateToString(root => root.SerializeField("int", "mValue"));

            Assert.AreEqual("[SerializeField]\nprivate int mValue;\n", source);
        }

        /// <summary>
        /// 验证文件提交区分创建、无变化和更新，并且无变化时不触碰时间戳。
        /// </summary>
        [Test]
        public void FileGenerationReportsCreatedUnchangedAndUpdated()
        {
            string directory = CreateTempDirectory();
            string path = Path.Combine(directory, "Generated.cs");

            try
            {
                Assert.AreEqual(CodeGenerationFileResult.Created, GenerateSingleLine(path, "// first"));
                DateTime timestamp = File.GetLastWriteTimeUtc(path);
                Assert.AreEqual(CodeGenerationFileResult.Unchanged, GenerateSingleLine(path, "// first"));
                Assert.AreEqual(timestamp, File.GetLastWriteTimeUtc(path));
                Assert.AreEqual(CodeGenerationFileResult.Updated, GenerateSingleLine(path, "// second"));
                Assert.AreEqual("// second\n", File.ReadAllText(path));
                Assert.IsEmpty(Directory.GetFiles(directory, "*.tmp", SearchOption.TopDirectoryOnly));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// 验证构建回调抛出异常时不会截断或覆盖已有正式文件。
        /// </summary>
        [Test]
        public void BuildFailurePreservesExistingFile()
        {
            string directory = CreateTempDirectory();
            string path = Path.Combine(directory, "Generated.cs");
            File.WriteAllText(path, "original");

            try
            {
                Assert.Throws<InvalidOperationException>(() => CodeGenKit.GenerateToFile(path, _ =>
                {
                    throw new InvalidOperationException("expected");
                }));
                Assert.AreEqual("original", File.ReadAllText(path));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// 使用单行内容调用正式文件生成入口，简化文件状态断言。
        /// </summary>
        /// <param name="path">目标文件路径。</param>
        /// <param name="line">要写入的单行源码。</param>
        /// <returns>本次文件提交结果。</returns>
        private static CodeGenerationFileResult GenerateSingleLine(string path, string line)
        {
            return CodeGenKit.GenerateToFile(path, root => root.Custom(line));
        }

        /// <summary>
        /// 创建当前测试独占的临时目录，避免并行测试共享输出文件。
        /// </summary>
        /// <returns>已创建的临时目录。</returns>
        private static string CreateTempDirectory()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "YokiFrame_CodeGenKit_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(directory);
            return directory;
        }
    }
}
