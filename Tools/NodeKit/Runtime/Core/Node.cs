using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace YokiFrame.NodeKit
{
    /// <summary>
    /// 节点基类
    /// </summary>
    public abstract partial class Node : ScriptableObject
    {
        [SerializeField] private NodeGraph mGraph;
        [SerializeField] private Vector2 mPosition;
        [SerializeField] private List<NodePort> mPorts = new();
        [SerializeField] private List<string> mPortKeys = new();

        private Dictionary<string, NodePort> mPortDict;

        public NodeGraph Graph
        {
            get => mGraph;
            internal set => mGraph = value;
        }

        public Vector2 Position
        {
            get => mPosition;
            set => mPosition = value;
        }

        /// <summary>
        /// 所有端口
        /// </summary>
        public IEnumerable<NodePort> Ports
        {
            get
            {
                EnsurePortDict();
                return mPortDict.Values;
            }
        }

        /// <summary>
        /// 所有输入端口
        /// </summary>
        public IEnumerable<NodePort> Inputs
        {
            get
            {
                EnsurePortDict();
                foreach (var port in mPortDict.Values)
                    if (port.IsInput) yield return port;
            }
        }

        /// <summary>
        /// 所有输出端口
        /// </summary>
        public IEnumerable<NodePort> Outputs
        {
            get
            {
                EnsurePortDict();
                foreach (var port in mPortDict.Values)
                    if (port.IsOutput) yield return port;
            }
        }

        /// <summary>
        /// 所有动态端口
        /// </summary>
        public IEnumerable<NodePort> DynamicPorts
        {
            get
            {
                EnsurePortDict();
                foreach (var port in mPortDict.Values)
                    if (port.IsDynamic) yield return port;
            }
        }

        private void EnsurePortDict()
        {
            if (mPortDict != default) return;
            mPortDict = new();
            for (int i = 0; i < mPorts.Count && i < mPortKeys.Count; i++)
                mPortDict[mPortKeys[i]] = mPorts[i];
        }

        /// <summary>
        /// 获取端口
        /// </summary>
        public NodePort GetPort(string fieldName)
        {
            EnsurePortDict();
            return mPortDict.TryGetValue(fieldName, out var port) ? port : null;
        }

        /// <summary>
        /// 获取输入端口
        /// </summary>
        public NodePort GetInputPort(string fieldName)
        {
            var port = GetPort(fieldName);
            return port is { IsInput: true } ? port : null;
        }

        /// <summary>
        /// 获取输出端口
        /// </summary>
        public NodePort GetOutputPort(string fieldName)
        {
            var port = GetPort(fieldName);
            return port is { IsOutput: true } ? port : null;
        }

        /// <summary>
        /// 检查是否存在指定端口
        /// </summary>
        public bool HasPort(string fieldName)
        {
            EnsurePortDict();
            return mPortDict.ContainsKey(fieldName);
        }

        /// <summary>
        /// 验证所有连接
        /// </summary>
        public void VerifyConnections()
        {
            EnsurePortDict();
            foreach (var port in mPortDict.Values)
                port.VerifyConnections();
        }

        /// <summary>
        /// 获取输入值
        /// </summary>
        public T GetInputValue<T>(string fieldName, T fallback = default)
        {
            var port = GetInputPort(fieldName);
            if (port == default) return fallback;
            if (!port.IsConnected) return GetBackingValue(fieldName, fallback);
            return port.GetInputValue(fallback);
        }

        /// <summary>
        /// 获取所有输入值
        /// </summary>
        public T[] GetInputValues<T>(string fieldName, params T[] fallback)
        {
            var port = GetInputPort(fieldName);
            return port == default ? fallback : port.GetInputValues(fallback);
        }

        /// <summary>
        /// 获取后备值
        /// </summary>
        protected T GetBackingValue<T>(string fieldName, T fallback = default)
        {
            var field = GetType().GetField(fieldName, 
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == default) return fallback;
            var value = field.GetValue(this);
            return value is T t ? t : fallback;
        }

        /// <summary>
        /// 计算输出值（子类重写）
        /// </summary>
        public virtual object GetValue(NodePort port) => null;

        /// <summary>
        /// 节点初始化（子类重写）
        /// </summary>
        protected virtual void Init() { }

        /// <summary>
        /// 连接创建回调
        /// </summary>
        public virtual void OnCreateConnection(NodePort from, NodePort to) { }

        /// <summary>
        /// 连接移除回调
        /// </summary>
        public virtual void OnRemoveConnection(NodePort port) { }
    }
}
