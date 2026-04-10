using System;
using System.Collections.Generic;
using System.Reflection;

namespace YokiFrame.NodeKit.Editor
{
    public static class NodeReflection
    {
        private static Dictionary<Type, Type> sNodeEditors;
        private static Dictionary<Type, Type> sGraphEditors;
        private static List<NodeTypeInfo> sNodeTypes;
        private static bool sInitialized;

        public sealed class NodeTypeInfo
        {
            public Type Type;
            public string MenuPath;
            public int Order;
            public int MaxCount;
        }

        public static void Initialize()
        {
            if (sInitialized) return;
            sInitialized = true;

            sNodeEditors = new Dictionary<Type, Type>();
            sGraphEditors = new Dictionary<Type, Type>();
            sNodeTypes = new List<NodeTypeInfo>();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    ScanAssembly(assemblies[i]);
                }
                catch
                {
                }
            }
        }

        public static IReadOnlyList<NodeTypeInfo> GetNodeTypes()
        {
            Initialize();
            return sNodeTypes;
        }

        public static Type GetNodeEditorType(Type nodeType)
        {
            Initialize();
            var current = nodeType;
            while (current != default && current != typeof(Node))
            {
                if (sNodeEditors.TryGetValue(current, out var editorType))
                    return editorType;
                current = current.BaseType;
            }

            return typeof(NodeEditorBase);
        }

        public static Type GetGraphEditorType(Type graphType)
        {
            Initialize();
            var current = graphType;
            while (current != default && current != typeof(NodeGraph))
            {
                if (sGraphEditors.TryGetValue(current, out var editorType))
                    return editorType;
                current = current.BaseType;
            }

            return typeof(NodeGraphEditorBase);
        }

        public static void ClearCache()
        {
            sInitialized = false;
            sNodeEditors = null;
            sGraphEditors = null;
            sNodeTypes = null;
        }

        private static void ScanAssembly(Assembly assembly)
        {
            var types = assembly.GetTypes();
            for (int i = 0; i < types.Length; i++)
            {
                var type = types[i];
                if (type.IsAbstract) continue;

                if (typeof(Node).IsAssignableFrom(type) && type != typeof(Node))
                {
                    var menuAttr = type.GetCustomAttribute<CreateNodeMenuAttribute>();
                    var disallowAttr = type.GetCustomAttribute<DisallowMultipleNodesAttribute>();
                    sNodeTypes.Add(new NodeTypeInfo
                    {
                        Type = type,
                        MenuPath = string.IsNullOrWhiteSpace(menuAttr?.MenuName) ? NodeEditorUtility.NodeDefaultPath(type) : menuAttr.MenuName,
                        Order = menuAttr?.Order ?? 0,
                        MaxCount = disallowAttr?.Max ?? -1
                    });
                }

                var nodeEditorAttr = type.GetCustomAttribute<CustomNodeEditorAttribute>();
                if (nodeEditorAttr != default && typeof(NodeEditorBase).IsAssignableFrom(type))
                    sNodeEditors[nodeEditorAttr.InspectedType] = type;

                var graphEditorAttr = type.GetCustomAttribute<CustomNodeGraphEditorAttribute>();
                if (graphEditorAttr != default && typeof(NodeGraphEditorBase).IsAssignableFrom(type))
                    sGraphEditors[graphEditorAttr.InspectedType] = type;
            }
        }
    }
}
