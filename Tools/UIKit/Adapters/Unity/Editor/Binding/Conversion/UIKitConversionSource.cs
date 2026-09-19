#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;

namespace YokiFrame
{
    /// <summary>仅对生成 partial 的声明及类型引用做保留原文本的转换；注释与字符串不参与名称替换。</summary>
    internal static class UIKitConversionSource
    {
        internal readonly struct Token
        {
            /// <summary>保存代码 token 在原文本中的位置，转换时保持未修改片段逐字不变。</summary>
            internal Token(string text, int start, int length) { Text = text; Start = start; Length = length; }
            internal string Text { get; }
            internal int Start { get; }
            internal int Length { get; }
        }

        internal sealed class TypeMove
        {
            internal string OldName;
            internal string NewName;
            internal string NewBase;
            internal string OldPath;
            internal string NewPath;
            internal string Name => OldName.Substring(OldName.LastIndexOf('.') + 1);
            internal string OldNamespace => OldName.Substring(0, OldName.LastIndexOf('.'));
            internal string NewNamespace => NewName.Substring(0, NewName.LastIndexOf('.'));
        }

        /// <summary>读取单 namespace 生成文件的类型声明；非生成文件允许只处理完整类型引用。</summary>
        internal static string GetNamespace(List<Token> tokens)
        {
            for (var index = 0; index < tokens.Count; index++)
                if (tokens[index].Text == "namespace") return ReadName(tokens, index + 1, out _);
            return string.Empty;
        }

        /// <summary>按 token 而非任意文本改写 namespace、基类和完整引用，并用别名保留原来的短类型引用。</summary>
        internal static string Rewrite(string source, List<TypeMove> moves, TypeMove declaration,
            Func<string, bool> namespaceRemains)
        {
            List<Token> tokens = Tokenize(source);
            string originalNamespace = GetNamespace(tokens);
            Dictionary<int, (int Length, string Text)> edits = new();
            HashSet<string> imports = GetImports(tokens);
            if (declaration != null) RewriteDeclaration(tokens, declaration, edits);
            RewriteQualifiedNames(tokens, moves, edits);
            StringBuilder aliases = new();
            for (var index = 0; index < moves.Count; index++)
            {
                TypeMove move = moves[index];
                if (move.OldName == move.NewName || move == declaration) continue;
                if (originalNamespace != move.OldNamespace && !imports.Contains(move.OldNamespace)) continue;
                if (HasAlias(tokens, move.Name)) continue;
                aliases.Append("using ").Append(move.Name).Append(" = global::").Append(move.NewName).AppendLine(";");
            }
            RemoveEmptyImports(tokens, imports, namespaceRemains, edits);
            if (declaration != null && declaration.OldNamespace != declaration.NewNamespace
                && namespaceRemains(declaration.OldNamespace) && !imports.Contains(declaration.OldNamespace))
                aliases.Append("using ").Append(declaration.OldNamespace).AppendLine(";");
            List<int> positions = new(edits.Keys);
            positions.Sort();
            StringBuilder result = new(source);
            for (var index = positions.Count - 1; index >= 0; index--)
            {
                int position = positions[index];
                var edit = edits[position];
                result.Remove(position, edit.Length).Insert(position, edit.Text);
            }
            // 别名进入 namespace 内，保持 extern alias、顶层 using 与文件头指令的合法顺序。
            if (aliases.Length > 0)
            {
                string rewritten = result.ToString();
                List<Token> updated = Tokenize(rewritten);
                int insertion = FindNamespaceBody(updated);
                result.Insert(insertion, "\n" + aliases);
            }
            return result.ToString();
        }

        /// <summary>复用已有别名，避免反向转换后出现重复 using alias；别名目标由完整类型替换更新。</summary>
        private static bool HasAlias(List<Token> tokens, string name)
        {
            for (var index = 0; index + 2 < tokens.Count; index++)
                if (tokens[index].Text == "using" && tokens[index + 1].Text == name && tokens[index + 2].Text == "=") return true;
            return false;
        }

        /// <summary>查找 namespace 的正文起点，无法安全插入别名时在写入前拒绝转换。</summary>
        private static int FindNamespaceBody(List<Token> tokens)
        {
            for (var index = 0; index < tokens.Count; index++)
            {
                if (tokens[index].Text != "namespace") continue;
                ReadName(tokens, index + 1, out int end);
                return tokens[end].Start + tokens[end].Length;
            }
            return 0;
        }

        /// <summary>只修改目标 partial 的 namespace 和直接 UI 基类，保留全部业务方法、字段和格式。</summary>
        private static void RewriteDeclaration(List<Token> tokens, TypeMove move,
            Dictionary<int, (int Length, string Text)> edits)
        {
            int classes = 0;
            int namespaces = 0;
            for (var index = 0; index < tokens.Count - 1; index++)
            {
                if (tokens[index].Text == "namespace")
                {
                    namespaces++;
                    string name = ReadName(tokens, index + 1, out int end);
                    if (name != move.OldNamespace) throw new InvalidOperationException("源码命名空间与旧类型不一致: " + move.OldPath);
                    SetEdit(tokens, index + 1, end, move.NewNamespace, edits);
                }
                if (tokens[index].Text != "class") continue;
                classes++;
                if (tokens[index + 1].Text != move.Name)
                    throw new InvalidOperationException("转换脚本包含其它类，请先拆分类型: " + move.OldPath);
                if (index + 2 >= tokens.Count || tokens[index + 2].Text != ":") continue;
                string baseType = ReadName(tokens, index + 3, out int baseEnd);
                if (baseType != "UIElement" && baseType != "UIComponent"
                    && baseType != "YokiFrame.UIElement" && baseType != "YokiFrame.UIComponent")
                    throw new InvalidOperationException("转换只支持直接继承 UIElement/UIComponent 的生成类型: " + move.OldPath);
                SetEdit(tokens, index + 3, baseEnd, "YokiFrame." + move.NewBase, edits);
            }
            if (classes != 1 || namespaces != 1)
                throw new InvalidOperationException("转换要求单 namespace、单类的 partial 文件: " + move.OldPath);
        }

        /// <summary>转换明确完整类型引用，绝不修改同名局部变量、注释或字符串。</summary>
        private static void RewriteQualifiedNames(List<Token> tokens, List<TypeMove> moves,
            Dictionary<int, (int Length, string Text)> edits)
        {
            for (var index = 0; index < tokens.Count; index++)
            {
                if (index > 0 && (tokens[index - 1].Text == "." || tokens[index - 1].Text == "namespace")) continue;
                string name = ReadName(tokens, index, out int end);
                for (var moveIndex = 0; moveIndex < moves.Count; moveIndex++)
                {
                    TypeMove move = moves[moveIndex];
                    if (move.OldName == move.NewName) continue;
                    if (name == move.OldName || name.StartsWith(move.OldName + ".", StringComparison.Ordinal))
                        SetEdit(tokens, index, end, move.NewName + name.Substring(move.OldName.Length), edits);
                }
            }
        }

        /// <summary>收集普通 using 命名空间；别名与 static using 不冒充命名空间导入。</summary>
        private static HashSet<string> GetImports(List<Token> tokens)
        {
            HashSet<string> imports = new(StringComparer.Ordinal);
            for (var index = 0; index < tokens.Count - 1; index++)
            {
                if (tokens[index].Text != "using") continue;
                string name = ReadName(tokens, index + 1, out int end);
                if (end < tokens.Count && tokens[end].Text == ";") imports.Add(name);
            }
            return imports;
        }

        /// <summary>仅移除本次迁移后确实不存在的旧命名空间导入，防止剩余脚本产生编译错误。</summary>
        private static void RemoveEmptyImports(List<Token> tokens, HashSet<string> imports,
            Func<string, bool> namespaceRemains, Dictionary<int, (int Length, string Text)> edits)
        {
            for (var index = 0; index < tokens.Count - 1; index++)
            {
                if (tokens[index].Text != "using") continue;
                string name = ReadName(tokens, index + 1, out int end);
                if (imports.Contains(name) && !namespaceRemains(name) && end < tokens.Count && tokens[end].Text == ";")
                    SetEdit(tokens, index, end + 1, string.Empty, edits);
            }
        }

        /// <summary>记录不包含尾部分隔符的替换范围，保持其它源码的原始字节布局。</summary>
        private static void SetEdit(List<Token> tokens, int start, int end, string text,
            Dictionary<int, (int Length, string Text)> edits)
        {
            Token last = tokens[end - 1];
            edits[tokens[start].Start] = (last.Start + last.Length - tokens[start].Start, text);
        }

        /// <summary>读取连续标识符和点号组成的限定名，不跨越其它 C# 运算符。</summary>
        private static string ReadName(List<Token> tokens, int start, out int end)
        {
            end = start;
            if (start >= tokens.Count) return string.Empty;
            StringBuilder name = new(tokens[start].Text);
            end++;
            while (end + 1 < tokens.Count && tokens[end].Text == ".")
            {
                name.Append('.').Append(tokens[end + 1].Text);
                end += 2;
            }
            return name.ToString();
        }

        /// <summary>切分生成器支持的 C# 9 声明；忽略注释、字符及字符串，插值字符串内类型表达式交给编译门禁校验。</summary>
        internal static List<Token> Tokenize(string source)
        {
            List<Token> tokens = new();
            for (var index = 0; index < source.Length;)
            {
                char current = source[index];
                if (char.IsWhiteSpace(current)) { index++; continue; }
                if (current == '/' && index + 1 < source.Length && source[index + 1] == '/')
                { while (index < source.Length && source[index] != '\n') index++; continue; }
                if (current == '/' && index + 1 < source.Length && source[index + 1] == '*')
                {
                    int close = source.IndexOf("*/", index + 2, StringComparison.Ordinal);
                    index = close < 0 ? source.Length : close + 2;
                    continue;
                }
                if (current == '"' || current == '\'')
                {
                    bool verbatim = index > 0 && source[index - 1] == '@';
                    index = SkipLiteral(source, index, current, verbatim);
                    continue;
                }
                int start = index++;
                if (char.IsLetter(current) || current == '_' || current == '@')
                    while (index < source.Length && (char.IsLetterOrDigit(source[index]) || source[index] == '_')) index++;
                tokens.Add(new Token(source.Substring(start, index - start), start, index - start));
            }
            return tokens;
        }

        /// <summary>跳过 C# 常规或逐字字符串，处理转义引号，避免把业务文本识别为声明。</summary>
        private static int SkipLiteral(string source, int start, char quote, bool verbatim)
        {
            int index = start + 1;
            while (index < source.Length)
            {
                char current = source[index++];
                if (!verbatim && current == '\\') { index = Math.Min(index + 1, source.Length); continue; }
                if (current != quote) continue;
                if (verbatim && index < source.Length && source[index] == quote) { index++; continue; }
                return index;
            }
            return index;
        }
    }
}
#endif
