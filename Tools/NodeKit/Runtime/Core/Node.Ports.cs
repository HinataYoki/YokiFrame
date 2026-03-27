using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace YokiFrame.NodeKit
{
    public abstract partial class Node
    {
        /// <summary>
        /// 添加动态输入端口
        /// </summary>
        public NodePort AddDynamicInput(
            Type type,
            string fieldName,
            ConnectionType connectionType = ConnectionType.Multiple,
            TypeConstraint typeConstraint = TypeConstraint.None)
        {
            return AddDynamicPort(type, fieldName, PortIO.Input, connectionType, typeConstraint);
        }

        /// <summary>
        /// 添加动态输出端口
        /// </summary>
        public NodePort AddDynamicOutput(
            Type type,
            string fieldName,
            ConnectionType connectionType = ConnectionType.Multiple,
            TypeConstraint typeConstraint = TypeConstraint.None)
        {
            return AddDynamicPort(type, fieldName, PortIO.Output, connectionType, typeConstraint);
        }

        private NodePort AddDynamicPort(
            Type type,
            string fieldName,
            PortIO direction,
            ConnectionType connectionType,
            TypeConstraint typeConstraint)
        {
            EnsurePortDict();
            if (mPortDict.ContainsKey(fieldName))
            {
                Debug.LogWarning($"[NodeKit] Port '{fieldName}' already exists on {name}");
                return mPortDict[fieldName];
            }

            var port = new NodePort(this, fieldName, direction, type, connectionType, typeConstraint, true);
            mPortDict[fieldName] = port;
            mPortKeys.Add(fieldName);
            mPorts.Add(port);
            return port;
        }

        /// <summary>
        /// 移除动态端口
        /// </summary>
        public void RemoveDynamicPort(string fieldName)
        {
            EnsurePortDict();
            if (!mPortDict.TryGetValue(fieldName, out var port)) return;
            if (!port.IsDynamic)
            {
                Debug.LogWarning($"[NodeKit] Cannot remove static port '{fieldName}'");
                return;
            }

            port.ClearConnections();
            mPortDict.Remove(fieldName);
            var index = mPortKeys.IndexOf(fieldName);
            if (index >= 0)
            {
                mPortKeys.RemoveAt(index);
                mPorts.RemoveAt(index);
            }
        }

        /// <summary>
        /// 清除所有动态端口
        /// </summary>
        public void ClearDynamicPorts()
        {
            EnsurePortDict();
            var toRemove = new List<string>();
            foreach (var kvp in mPortDict)
                if (kvp.Value.IsDynamic) toRemove.Add(kvp.Key);
            for (int i = 0; i < toRemove.Count; i++)
                RemoveDynamicPort(toRemove[i]);
        }

        /// <summary>
        /// 更新端口（从属性扫描）
        /// </summary>
        internal void UpdatePorts()
        {
            EnsurePortDict();
            var type = GetType();
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            for (int i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                var inputAttr = field.GetCustomAttribute<InputAttribute>();
                var outputAttr = field.GetCustomAttribute<OutputAttribute>();
                var typeOverride = field.GetCustomAttribute<PortTypeOverrideAttribute>();

                var valueType = typeOverride != default ? typeOverride.Type : field.FieldType;

                if (inputAttr != default)
                    EnsurePort(field.Name, valueType, PortIO.Input, 
                        inputAttr.ConnectionType, inputAttr.TypeConstraint, false);
                else if (outputAttr != default)
                    EnsurePort(field.Name, valueType, PortIO.Output, 
                        outputAttr.ConnectionType, outputAttr.TypeConstraint, false);
            }
        }

        private void EnsurePort(string fieldName, Type valueType, PortIO direction,
            ConnectionType connectionType, TypeConstraint typeConstraint, bool isDynamic)
        {
            if (mPortDict.ContainsKey(fieldName)) return;
            var port = new NodePort(this, fieldName, direction, valueType, connectionType, typeConstraint, isDynamic);
            mPortDict[fieldName] = port;
            mPortKeys.Add(fieldName);
            mPorts.Add(port);
        }

        /// <summary>
        /// 清除所有连接
        /// </summary>
        internal void ClearAllConnections()
        {
            EnsurePortDict();
            foreach (var port in mPortDict.Values)
                port.ClearConnections();
        }
    }
}
