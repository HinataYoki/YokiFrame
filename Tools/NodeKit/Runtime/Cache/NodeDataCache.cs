using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace YokiFrame.NodeKit
{
    /// <summary>
    /// 节点数据缓存，预缓存反射数据以提升运行时性能
    /// </summary>
    public static class NodeDataCache
    {
        private static Dictionary<Type, Dictionary<string, PortInfo>> sPortCache;
        private static Dictionary<Type, string> sTypeQualifiedNameCache;
        private static bool sInitialized;

        /// <summary>
        /// 端口信息
        /// </summary>
        public class PortInfo
        {
            public string FieldName;
            public Type ValueType;
            public PortIO Direction;
            public ConnectionType ConnectionType;
            public TypeConstraint TypeConstraint;
            public bool DynamicPortList;
        }

        /// <summary>
        /// 获取类型的完全限定名（带缓存）
        /// </summary>
        public static string GetTypeQualifiedName(Type type)
        {
            if (sTypeQualifiedNameCache == default)
                sTypeQualifiedNameCache = new();

            if (sTypeQualifiedNameCache.TryGetValue(type, out var name))
                return name;

            name = type.AssemblyQualifiedName;
            sTypeQualifiedNameCache[type] = name;
            return name;
        }

        /// <summary>
        /// 获取节点类型的端口信息
        /// </summary>
        public static Dictionary<string, PortInfo> GetPortInfo(Type nodeType)
        {
            EnsureInitialized();
            return sPortCache.TryGetValue(nodeType, out var info) ? info : null;
        }

        /// <summary>
        /// 更新节点端口
        /// </summary>
        public static void UpdatePorts(Node node)
        {
            EnsureInitialized();
            node.UpdatePorts();
        }

        private static void EnsureInitialized()
        {
            if (sInitialized) return;
            sInitialized = true;
            sPortCache = new();
            BuildCache();
        }

        private static void BuildCache()
        {
            var baseType = typeof(Node);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (int i = 0; i < assemblies.Length; i++)
            {
                var assembly = assemblies[i];
                var assemblyName = assembly.GetName().Name;

                // 跳过系统程序集
                if (assemblyName.StartsWith("Unity") ||
                    assemblyName.StartsWith("System") ||
                    assemblyName.StartsWith("mscorlib") ||
                    assemblyName.StartsWith("Microsoft"))
                    continue;

                try
                {
                    var types = assembly.GetTypes();
                    for (int j = 0; j < types.Length; j++)
                    {
                        var type = types[j];
                        if (type.IsAbstract || !baseType.IsAssignableFrom(type))
                            continue;
                        CacheNodeType(type);
                    }
                }
                catch { /* 忽略无法加载的程序集 */ }
            }
        }

        private static void CacheNodeType(Type nodeType)
        {
            var portInfo = new Dictionary<string, PortInfo>();
            var fields = GetAllFields(nodeType);

            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                var inputAttr = field.GetCustomAttribute<InputAttribute>();
                var outputAttr = field.GetCustomAttribute<OutputAttribute>();
                var typeOverride = field.GetCustomAttribute<PortTypeOverrideAttribute>();

                if (inputAttr == default && outputAttr == default)
                    continue;

                var valueType = typeOverride != default ? typeOverride.Type : field.FieldType;

                if (inputAttr != default)
                {
                    portInfo[field.Name] = new PortInfo
                    {
                        FieldName = field.Name,
                        ValueType = valueType,
                        Direction = PortIO.Input,
                        ConnectionType = inputAttr.ConnectionType,
                        TypeConstraint = inputAttr.TypeConstraint,
                        DynamicPortList = inputAttr.DynamicPortList
                    };
                }
                else if (outputAttr != default)
                {
                    portInfo[field.Name] = new PortInfo
                    {
                        FieldName = field.Name,
                        ValueType = valueType,
                        Direction = PortIO.Output,
                        ConnectionType = outputAttr.ConnectionType,
                        TypeConstraint = outputAttr.TypeConstraint,
                        DynamicPortList = outputAttr.DynamicPortList
                    };
                }
            }

            if (portInfo.Count > 0)
                sPortCache[nodeType] = portInfo;
        }

        private static List<FieldInfo> GetAllFields(Type type)
        {
            var fields = new List<FieldInfo>();
            fields.AddRange(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));

            // 获取继承的私有字段
            var current = type.BaseType;
            while (current != default && current != typeof(Node))
            {
                var parentFields = current.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
                for (int i = 0; i < parentFields.Length; i++)
                {
                    var pf = parentFields[i];
                    bool exists = false;
                    for (int j = 0; j < fields.Count; j++)
                    {
                        if (fields[j].Name == pf.Name)
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists) fields.Add(pf);
                }
                current = current.BaseType;
            }

            return fields;
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        public static void ClearCache()
        {
            sInitialized = false;
            sPortCache = null;
            sTypeQualifiedNameCache = null;
        }
    }
}
