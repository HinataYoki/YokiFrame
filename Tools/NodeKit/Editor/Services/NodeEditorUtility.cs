using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.NodeKit.Editor
{
    public static class NodeEditorUtility
    {
        private static readonly Texture2D sScriptIcon = EditorGUIUtility.IconContent("cs Script Icon").image as Texture2D;
        private static readonly Dictionary<Type, Color> sTypeColors = new()
        {
            { typeof(float), new Color(0.5f, 0.8f, 0.5f) },
            { typeof(int), new Color(0.5f, 0.7f, 0.9f) },
            { typeof(bool), new Color(0.9f, 0.5f, 0.5f) },
            { typeof(string), new Color(0.9f, 0.7f, 0.5f) },
            { typeof(Vector2), new Color(0.9f, 0.9f, 0.5f) },
            { typeof(Vector3), new Color(0.9f, 0.9f, 0.5f) },
            { typeof(Vector4), new Color(0.9f, 0.9f, 0.5f) },
            { typeof(Color), new Color(0.9f, 0.5f, 0.9f) },
            { typeof(GameObject), new Color(0.5f, 0.9f, 0.9f) },
            { typeof(UnityEngine.Object), new Color(0.7f, 0.7f, 0.7f) },
        };

        public static Color GetTypeColor(Type type)
        {
            if (type == default)
                return Color.gray;

            if (sTypeColors.TryGetValue(type, out var color))
                return ResolveTypeColor(type, color);

            var current = type.BaseType;
            while (current != default)
            {
                if (sTypeColors.TryGetValue(current, out color))
                    return ResolveTypeColor(current, color);

                current = current.BaseType;
            }

            string typeName = PrettyName(type);
            if (NodePreferences.TryGetTypeColor(typeName, out color))
                return color;

            var previousState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(typeName.GetHashCode());
            color = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
            UnityEngine.Random.state = previousState;
            return color;
        }

        public static void RegisterTypeColor(Type type, Color color) => sTypeColors[type] = color;

        public static void RecordUndo(UnityEngine.Object target, string name)
        {
            if (target == default) return;
            Undo.RecordObject(target, name);
        }

        public static void RecordUndo(UnityEngine.Object[] targets, string name)
        {
            if (targets == default || targets.Length == 0) return;
            Undo.RecordObjects(targets, name);
        }

        public static void SetDirty(UnityEngine.Object target)
        {
            if (target == default) return;
            EditorUtility.SetDirty(target);
        }

        public static void SaveAsset(UnityEngine.Object target)
        {
            if (target == default) return;

            if (target is NodeGraph graph)
            {
                EditorUtility.SetDirty(graph);
                var nodes = graph.Nodes;
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i] != default)
                        EditorUtility.SetDirty(nodes[i]);
                }

                AssetDatabase.SaveAssetIfDirty(graph);
                AssetDatabase.SaveAssets();
                return;
            }

            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
        }

        public static string PrettyName(Type type)
        {
            if (type == default) return "null";
            if (type == typeof(object)) return "object";
            if (type == typeof(float)) return "float";
            if (type == typeof(int)) return "int";
            if (type == typeof(long)) return "long";
            if (type == typeof(double)) return "double";
            if (type == typeof(string)) return "string";
            if (type == typeof(bool)) return "bool";
            if (type.IsGenericType)
            {
                var genericArgs = type.GetGenericArguments().Select(PrettyName);
                return $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(", ", genericArgs)}>";
            }

            if (type.IsArray)
                return $"{PrettyName(type.GetElementType())}[]";

            return type.Name;
        }

        public static string NodeDefaultName(Type type)
        {
            var typeName = type.Name;
            if (typeName.EndsWith("Node"))
                typeName = typeName[..^4];
            return ObjectNames.NicifyVariableName(typeName);
        }

        public static string NodeDefaultPath(Type type)
        {
            var path = type.FullName?.Replace('.', '/') ?? type.Name;
            if (path.EndsWith("Node"))
                path = path[..^4];
            return ObjectNames.NicifyVariableName(path);
        }

        public static FieldInfo GetFieldInfo(Type type, string fieldName)
        {
            return type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public static bool HasCompatiblePortType(Type nodeType, Type compatibleType, PortIO direction = PortIO.Input)
        {
            if (nodeType == default || compatibleType == default) return false;
            var portInfo = NodeDataCache.GetPortInfo(nodeType);
            if (portInfo == default) return false;

            foreach (var info in portInfo.Values)
            {
                if (info.Direction != direction) continue;
                if (info.ValueType == default) continue;
                if (compatibleType.IsAssignableFrom(info.ValueType) || info.ValueType.IsAssignableFrom(compatibleType))
                    return true;
            }

            return false;
        }

        public static KeyValuePair<ContextMenu, MethodInfo>[] GetContextMenuMethods(object obj)
        {
            var type = obj.GetType();
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var items = new List<KeyValuePair<ContextMenu, MethodInfo>>();
            for (int i = 0; i < methods.Length; i++)
            {
                var attribs = methods[i].GetCustomAttributes(typeof(ContextMenu), true);
                for (int j = 0; j < attribs.Length; j++)
                {
                    if (attribs[j] is not ContextMenu menu) continue;
                    if (methods[i].GetParameters().Length != 0 || methods[i].IsStatic) continue;
                    items.Add(new KeyValuePair<ContextMenu, MethodInfo>(menu, methods[i]));
                }
            }

            items.Sort((a, b) => a.Key.priority.CompareTo(b.Key.priority));
            return items.ToArray();
        }

        [MenuItem("Assets/Create/YokiFrame/NodeKit/Node C# Script", false, 89)]
        private static void CreateNodeScript()
        {
            CreateFromTemplate(
                "NewNode.cs",
                "Assets/YokiFrame/Tools/NodeKit/Editor/Resources/ScriptTemplates/NodeKit_NodeTemplate.cs.txt");
        }

        [MenuItem("Assets/Create/YokiFrame/NodeKit/NodeGraph C# Script", false, 90)]
        private static void CreateNodeGraphScript()
        {
            CreateFromTemplate(
                "NewNodeGraph.cs",
                "Assets/YokiFrame/Tools/NodeKit/Editor/Resources/ScriptTemplates/NodeKit_NodeGraphTemplate.cs.txt");
        }

public static void CreateFromTemplate(string initialName, string templatePath)
{
#if UNITY_6000_6_OR_NEWER
    ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
        default(UnityEngine.EntityId),
        ScriptableObject.CreateInstance<CreateCodeFileAction>(),
        initialName,
        sScriptIcon,
        templatePath);
#else
    ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
        0,
        ScriptableObject.CreateInstance<CreateCodeFileAction>(),
        initialName,
        sScriptIcon,
        templatePath);
#endif
}

        private static Color ResolveTypeColor(Type type, Color fallback)
        {
            return NodePreferences.TryGetTypeColor(PrettyName(type), out var color) ? color : fallback;
        }

#if UNITY_6000_6_OR_NEWER
private sealed class CreateCodeFileAction : UnityEditor.ProjectWindowCallback.AssetCreationEndAction
{
    public override void Action(UnityEngine.EntityId entityId, string pathName, string resourceFile)
    {
        var asset = CreateScript(pathName, resourceFile);
        ProjectWindowUtil.ShowCreatedAsset(asset);
    }
}
#else
private sealed class CreateCodeFileAction : UnityEditor.ProjectWindowCallback.EndNameEditAction
{
    public override void Action(int instanceId, string pathName, string resourceFile)
    {
        var asset = CreateScript(pathName, resourceFile);
        ProjectWindowUtil.ShowCreatedAsset(asset);
    }
}
#endif

        private static UnityEngine.Object CreateScript(string pathName, string templatePath)
        {
            string className = Path.GetFileNameWithoutExtension(pathName).Replace(" ", string.Empty);
            string fullTemplatePath = Path.GetFullPath(templatePath);
            if (!File.Exists(fullTemplatePath))
            {
                Debug.LogError($"[NodeKit] Template file not found: {templatePath}");
                return null;
            }

            string templateText = File.ReadAllText(fullTemplatePath, Encoding.UTF8);
            templateText = templateText.Replace("#SCRIPTNAME#", className);
            templateText = templateText.Replace("#NOTRIM#", string.Empty);

            File.WriteAllText(Path.GetFullPath(pathName), templateText, new UTF8Encoding(true, false));
            AssetDatabase.ImportAsset(pathName);
            return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(pathName);
        }
    }
}
