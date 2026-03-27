using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame.NodeKit
{
    /// <summary>
    /// 节点端口，管理连接和数据传递
    /// </summary>
    [Serializable]
    public class NodePort : ISerializationCallbackReceiver
    {
        [SerializeField] private string mFieldName;
        [SerializeField] private Node mNode;
        [SerializeField] private PortIO mDirection;
        [SerializeField] private ConnectionType mConnectionType;
        [SerializeField] private TypeConstraint mTypeConstraint;
        [SerializeField] private bool mIsDynamic;
        [SerializeField] private string mValueTypeName;
        [SerializeField] private List<PortConnection> mConnections = new();

        private Type mValueType;

        public string FieldName => mFieldName;
        public Node Node => mNode;
        public PortIO Direction => mDirection;
        public ConnectionType ConnectionType => mConnectionType;
        public TypeConstraint TypeConstraint => mTypeConstraint;
        public bool IsDynamic => mIsDynamic;
        public bool IsInput => mDirection == PortIO.Input;
        public bool IsOutput => mDirection == PortIO.Output;
        public bool IsConnected => mConnections.Count > 0;
        public int ConnectionCount => mConnections.Count;

        public Type ValueType
        {
            get => mValueType;
            set
            {
                mValueType = value;
                mValueTypeName = value?.AssemblyQualifiedName;
            }
        }

        public IReadOnlyList<PortConnection> Connections => mConnections;

        public NodePort() { }

        public NodePort(Node node, string fieldName, PortIO direction, Type valueType,
            ConnectionType connectionType, TypeConstraint typeConstraint, bool isDynamic)
        {
            mNode = node;
            mFieldName = fieldName;
            mDirection = direction;
            mValueType = valueType;
            mValueTypeName = valueType?.AssemblyQualifiedName;
            mConnectionType = connectionType;
            mTypeConstraint = typeConstraint;
            mIsDynamic = isDynamic;
        }

        /// <summary>
        /// 获取连接的端口
        /// </summary>
        public NodePort GetConnection(int index)
        {
            if (index < 0 || index >= mConnections.Count) return null;
            var conn = mConnections[index];
            return conn.Node == default ? null : conn.Node.GetPort(conn.FieldName);
        }

        /// <summary>
        /// 检查是否可以连接到目标端口
        /// </summary>
        public bool CanConnectTo(NodePort target)
        {
            if (target == default) return false;
            if (target.Node == mNode) return false;
            if (target.Direction == mDirection) return false;
            return CheckTypeConstraint(this, target) && CheckTypeConstraint(target, this);
        }

        private static bool CheckTypeConstraint(NodePort from, NodePort to)
        {
            var fromType = from.ValueType;
            var toType = to.ValueType;
            if (fromType == default || toType == default) return true;

            return from.TypeConstraint switch
            {
                TypeConstraint.None => true,
                TypeConstraint.Strict => fromType == toType,
                TypeConstraint.Inherited => from.IsInput 
                    ? toType.IsAssignableFrom(fromType) 
                    : fromType.IsAssignableFrom(toType),
                TypeConstraint.InheritedInverse => from.IsInput 
                    ? fromType.IsAssignableFrom(toType) 
                    : toType.IsAssignableFrom(fromType),
                TypeConstraint.InheritedAny => fromType.IsAssignableFrom(toType) 
                    || toType.IsAssignableFrom(fromType),
                _ => true
            };
        }

        /// <summary>
        /// 连接到目标端口
        /// </summary>
        public void Connect(NodePort target)
        {
            if (!CanConnectTo(target)) return;
            if (mConnectionType == ConnectionType.Override) ClearConnections();
            if (target.ConnectionType == ConnectionType.Override) target.ClearConnections();

            mConnections.Add(new PortConnection { FieldName = target.FieldName, Node = target.Node });
            target.mConnections.Add(new PortConnection { FieldName = mFieldName, Node = mNode });
        }

        /// <summary>
        /// 断开与目标端口的连接
        /// </summary>
        public void Disconnect(NodePort target)
        {
            if (target == default) return;
            for (int i = mConnections.Count - 1; i >= 0; i--)
            {
                var conn = mConnections[i];
                if (conn.Node == target.Node && conn.FieldName == target.FieldName)
                {
                    mConnections.RemoveAt(i);
                    break;
                }
            }
            for (int i = target.mConnections.Count - 1; i >= 0; i--)
            {
                var conn = target.mConnections[i];
                if (conn.Node == mNode && conn.FieldName == mFieldName)
                {
                    target.mConnections.RemoveAt(i);
                    break;
                }
            }
        }

        /// <summary>
        /// 清除所有连接
        /// </summary>
        public void ClearConnections()
        {
            for (int i = mConnections.Count - 1; i >= 0; i--)
            {
                var conn = mConnections[i];
                if (conn.Node == default) continue;
                var targetPort = conn.Node.GetPort(conn.FieldName);
                if (targetPort == default) continue;
                for (int j = targetPort.mConnections.Count - 1; j >= 0; j--)
                {
                    var tc = targetPort.mConnections[j];
                    if (tc.Node == mNode && tc.FieldName == mFieldName)
                    {
                        targetPort.mConnections.RemoveAt(j);
                        break;
                    }
                }
            }
            mConnections.Clear();
        }

        /// <summary>
        /// 获取输入值
        /// </summary>
        public T GetInputValue<T>(T fallback = default)
        {
            if (!IsInput || mConnections.Count == 0) return fallback;
            var conn = mConnections[0];
            if (conn.Node == default) return fallback;
            var outputPort = conn.Node.GetPort(conn.FieldName);
            if (outputPort == default) return fallback;
            var value = conn.Node.GetValue(outputPort);
            return value is T t ? t : fallback;
        }

        /// <summary>
        /// 获取所有输入值
        /// </summary>
        public T[] GetInputValues<T>(params T[] fallback)
        {
            if (!IsInput || mConnections.Count == 0) return fallback;
            var result = new T[mConnections.Count];
            for (int i = 0; i < mConnections.Count; i++)
            {
                var conn = mConnections[i];
                if (conn.Node == default) continue;
                var outputPort = conn.Node.GetPort(conn.FieldName);
                if (outputPort == default) continue;
                var value = conn.Node.GetValue(outputPort);
                result[i] = value is T t ? t : default;
            }
            return result;
        }

        /// <summary>
        /// 获取输出值
        /// </summary>
        public object GetOutputValue() => IsOutput && mNode != default ? mNode.GetValue(this) : null;

        /// <summary>
        /// 尝试获取输入值
        /// </summary>
        public bool TryGetInputValue<T>(out T value)
        {
            var obj = GetInputValue<object>();
            if (obj is T t)
            {
                value = t;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// 获取所有输入的和（float）
        /// </summary>
        public float GetInputSum(float fallback)
        {
            if (!IsInput || mConnections.Count == 0) return fallback;
            float result = 0;
            for (int i = 0; i < mConnections.Count; i++)
            {
                var conn = mConnections[i];
                if (conn.Node == default) continue;
                var outputPort = conn.Node.GetPort(conn.FieldName);
                if (outputPort == default) continue;
                var value = conn.Node.GetValue(outputPort);
                if (value is float f) result += f;
            }
            return result;
        }

        /// <summary>
        /// 获取所有输入的和（int）
        /// </summary>
        public int GetInputSum(int fallback)
        {
            if (!IsInput || mConnections.Count == 0) return fallback;
            int result = 0;
            for (int i = 0; i < mConnections.Count; i++)
            {
                var conn = mConnections[i];
                if (conn.Node == default) continue;
                var outputPort = conn.Node.GetPort(conn.FieldName);
                if (outputPort == default) continue;
                var value = conn.Node.GetValue(outputPort);
                if (value is int n) result += n;
            }
            return result;
        }

        /// <summary>
        /// 检查是否已连接到指定端口
        /// </summary>
        public bool IsConnectedTo(NodePort port)
        {
            if (port == default) return false;
            for (int i = 0; i < mConnections.Count; i++)
            {
                var conn = mConnections[i];
                if (conn.Node == port.Node && conn.FieldName == port.FieldName)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 获取与指定端口的连接索引
        /// </summary>
        public int GetConnectionIndex(NodePort port)
        {
            if (port == default) return -1;
            for (int i = 0; i < mConnections.Count; i++)
            {
                var conn = mConnections[i];
                if (conn.Node == port.Node && conn.FieldName == port.FieldName)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// 获取重路由点
        /// </summary>
        public List<Vector2> GetReroutePoints(int index)
        {
            if (index < 0 || index >= mConnections.Count) return null;
            return mConnections[index].ReroutePoints;
        }

        /// <summary>
        /// 验证连接有效性，移除无效连接
        /// </summary>
        public void VerifyConnections()
        {
            for (int i = mConnections.Count - 1; i >= 0; i--)
            {
                var conn = mConnections[i];
                if (conn.Node == default || string.IsNullOrEmpty(conn.FieldName))
                {
                    mConnections.RemoveAt(i);
                    continue;
                }
                var port = conn.Node.GetPort(conn.FieldName);
                if (port == default)
                    mConnections.RemoveAt(i);
            }
        }

        /// <summary>
        /// 交换连接
        /// </summary>
        public void SwapConnections(NodePort targetPort)
        {
            if (targetPort == default) return;
            
            var myConnections = new List<NodePort>();
            var targetConnections = new List<NodePort>();

            for (int i = 0; i < mConnections.Count; i++)
            {
                var port = GetConnection(i);
                if (port != default) myConnections.Add(port);
            }

            for (int i = 0; i < targetPort.mConnections.Count; i++)
            {
                var port = targetPort.GetConnection(i);
                if (port != default) targetConnections.Add(port);
            }

            ClearConnections();
            targetPort.ClearConnections();

            for (int i = 0; i < myConnections.Count; i++)
                targetPort.Connect(myConnections[i]);

            for (int i = 0; i < targetConnections.Count; i++)
                Connect(targetConnections[i]);
        }

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            if (!string.IsNullOrEmpty(mValueTypeName))
                mValueType = Type.GetType(mValueTypeName);
        }
    }

    /// <summary>
    /// 端口连接数据
    /// </summary>
    [Serializable]
    public class PortConnection
    {
        public string FieldName;
        public Node Node;
        public List<Vector2> ReroutePoints = new();
    }
}
