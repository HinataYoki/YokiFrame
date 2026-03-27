using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace YokiFrame.NodeKit.Editor
{
    /// <summary>
    /// 节点反射服务
    /// </summary>
    public static class NodeReflection
    {
        private static Dictionary<Type, Type> sNodeEditors;
        private static Dictionary<Type, Type> sGraphEditors;
        private static List<NodeTypeInfo> sNodeTypes;
        private static bool sInitialized;

        /// <summary>
        /// 节点类型信息
        /// </summary>
        public class NodeTypeInfo
        {
            public Type Type;
            public string MenuPath;
            public int Order;
            public int MaxCount;
        }

        /// <summary>
        /// 初始化反射缓存
        /// </summary>
        public static void Initialize()
        {
            if (sInitialized) return;
            sInitialized = true;

            sNodeEditors = new();
            sGraphEditors = new();
            sNodeTypes = new();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try { ScanAssembly(assemblies[i]); }
                catch { /* 忽略无法加载的程序集 */ }
            }
        }

        private static void ScanAssembly(Assembly assembly)
        {
            var types = assembly.GetTypes();
            for (int i = 0; i < types.Length; i++)
            {
                var type = types[i];
                if (type.IsAbstract) continue;

                // 扫描节点类型
                if (typeof(Node).IsAssignableFrom(type))
                {
                    var menuAttr = type.GetCustomAttribute<CreateNodeMenuAttribute>();
                    var disallowAttr = type.GetCustomAttribute<DisallowMultipleNodesAttribute>();
                    sNodeTypes.Add(new NodeTypeInfo
                    {
                        Type = type,
                        MenuPath = menuAttr == default ? type.Name : menuAttr.MenuName,
                        Order = menuAttr == default ? 0 : menuAttr.Order,
                        MaxCount = disallowAttr == default ? -1 : disallowAttr.Max
                    });
                }

                // 扫描节点编辑器
                var nodeEditorAttr = type.GetCustomAttribute<CustomNodeEditorAttribute>();
                if (nodeEditorAttr != default && typeof(NodeEditorBase).IsAssignableFrom(type))
                    sNodeEditors[nodeEditorAttr.InspectedType] = type;

                // 扫描图编辑器
                var graphEditorAttr = type.GetCustomAttribute<CustomNodeGraphEditorAttribute>();
                if (graphEditorAttr != default && typeof(NodeGraphEditorBase).IsAssignableFrom(type))
                    sGraphEditors[graphEditorAttr.InspectedType] = type;
            }
        }

        /// <summary>
        /// 获取所有节点类型
        /// </summary>
        public static IReadOnlyList<NodeTypeInfo> GetNodeTypes()
        {
            Initialize();
            return sNodeTypes;
        }

        /// <summary>
        /// 获取节点编辑器类型
        /// </summary>
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

        /// <summary>
        /// 获取图编辑器类型
        /// </summary>
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

        /// <summary>
        /// 清除缓存
        /// </summary>
        public static void ClearCache()
        {
            sInitialized = false;
            sNodeEditors = null;
            sGraphEditors = null;
            sNodeTypes = null;
        }
    }
}
