#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// Luban 命名空间自动检测器
    /// 通过反射检测 SimpleJSON 类型的实际命名空间，无需手动宏定义
    /// </summary>
    internal static class LubanNamespaceDetector
    {
        private static bool? sCachedResult;
        private static MethodInfo sParseMethod;
        private static Type sJSONNodeType;

        /// <summary>
        /// 检测是否使用新命名空间 Luban.SimpleJson
        /// </summary>
        public static bool IsNewNamespace()
        {
            if (sCachedResult.HasValue) return sCachedResult.Value;

            try
            {
                // 尝试查找 Luban.SimpleJson.JSONNode 类型
                var newType = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic)
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return Type.EmptyTypes; }
                    })
                    .FirstOrDefault(t => t.FullName == "Luban.SimpleJson.JSONNode");

                if (newType != null)
                {
                    sCachedResult = true;
                    return true;
                }

                // 尝试查找旧的 SimpleJSON.JSONNode 类型
                var oldType = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic)
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return Type.EmptyTypes; }
                    })
                    .FirstOrDefault(t => t.FullName == "SimpleJSON.JSONNode");

                sCachedResult = oldType == null; // 如果旧类型也不存在，假定为新版本
                return sCachedResult.Value;
            }
            catch
            {
                sCachedResult = false;
                return false;
            }
        }

        /// <summary>
        /// 获取 JSONNode 类型（自动适配新旧版本）
        /// </summary>
        public static Type GetJSONNodeType()
        {
            if (sJSONNodeType != null) return sJSONNodeType;

            var typeName = IsNewNamespace() ? "Luban.SimpleJson.JSONNode" : "SimpleJSON.JSONNode";
            
            sJSONNodeType = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Type.EmptyTypes; }
                })
                .FirstOrDefault(t => t.FullName == typeName);

            return sJSONNodeType;
        }

        /// <summary>
        /// 获取 JSON 类型（自动适配新旧版本）
        /// </summary>
        public static Type GetJSONType()
        {
            var typeName = IsNewNamespace() ? "Luban.SimpleJson.JSON" : "SimpleJSON.JSON";
            
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Type.EmptyTypes; }
                })
                .FirstOrDefault(t => t.FullName == typeName);
        }

        /// <summary>
        /// 通过反射调用 JSON.Parse（自动适配新旧版本）
        /// </summary>
        public static object ParseJson(string jsonText)
        {
            if (sParseMethod == null)
            {
                var jsonType = GetJSONType();
                if (jsonType == null)
                    throw new InvalidOperationException("无法找到 JSON 类型，请确认 Luban 已正确安装");

                sParseMethod = jsonType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(string) }, null);

                if (sParseMethod == null)
                    throw new InvalidOperationException("无法找到 JSON.Parse 方法");
            }

            return sParseMethod.Invoke(null, new object[] { jsonText });
        }

        /// <summary>
        /// JSONNode 反射包装器（用于编辑器代码）
        /// </summary>
        public class JSONNodeWrapper
        {
            private readonly object mNode;
            private readonly Type mNodeType;

            public JSONNodeWrapper(object node)
            {
                mNode = node;
                mNodeType = node?.GetType() ?? GetJSONNodeType();
            }

            public bool IsArray => (bool)mNodeType.GetProperty("IsArray")?.GetValue(mNode);
            public bool IsObject => (bool)mNodeType.GetProperty("IsObject")?.GetValue(mNode);
            public bool IsNumber => (bool)mNodeType.GetProperty("IsNumber")?.GetValue(mNode);
            public bool IsBoolean => (bool)mNodeType.GetProperty("IsBoolean")?.GetValue(mNode);
            public bool IsNull => (bool)mNodeType.GetProperty("IsNull")?.GetValue(mNode);
            public int Count => (int)mNodeType.GetProperty("Count")?.GetValue(mNode);
            public string Value => mNodeType.GetProperty("Value")?.GetValue(mNode)?.ToString() ?? "";

            public System.Collections.IEnumerable Children
            {
                get
                {
                    var children = mNodeType.GetProperty("Children")?.GetValue(mNode) as System.Collections.IEnumerable;
                    if (children == null) yield break;

                    foreach (var child in children)
                        yield return new JSONNodeWrapper(child);
                }
            }

            public System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, JSONNodeWrapper>> AsObject
            {
                get
                {
                    var asObjectProp = mNodeType.GetProperty("AsObject")?.GetValue(mNode);
                    if (asObjectProp == null) yield break;

                    var enumerator = asObjectProp.GetType().GetMethod("GetEnumerator")?.Invoke(asObjectProp, null);
                    if (enumerator == null) yield break;

                    var enumeratorType = enumerator.GetType();
                    var moveNext = enumeratorType.GetMethod("MoveNext");
                    var current = enumeratorType.GetProperty("Current");

                    while ((bool)moveNext.Invoke(enumerator, null))
                    {
                        var kvp = current.GetValue(enumerator);
                        var kvpType = kvp.GetType();
                        var key = (string)kvpType.GetProperty("Key")?.GetValue(kvp);
                        var value = kvpType.GetProperty("Value")?.GetValue(kvp);

                        yield return new System.Collections.Generic.KeyValuePair<string, JSONNodeWrapper>(
                            key, new JSONNodeWrapper(value));
                    }
                }
            }
        }
    }
}
#endif
