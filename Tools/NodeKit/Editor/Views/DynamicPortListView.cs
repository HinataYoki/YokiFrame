using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.NodeKit.Editor
{
    /// <summary>
    /// 动态端口列表视图（UIToolkit 实现）
    /// </summary>
    public class DynamicPortListView : VisualElement
    {
        private Node mNode;
        private NodeView mNodeView;
        private string mFieldName;
        private Type mValueType;
        private PortIO mDirection;
        private ConnectionType mConnectionType;
        private TypeConstraint mTypeConstraint;
        private SerializedProperty mArrayProperty;
        private List<DynamicPortItemView> mItemViews = new();

        public DynamicPortListView()
        {
            AddToClassList("yoki-dynamic-port-list");
        }

        /// <summary>
        /// 初始化动态端口列表
        /// </summary>
        public void Initialize(
            Node node,
            NodeView nodeView,
            string fieldName,
            Type valueType,
            PortIO direction,
            ConnectionType connectionType,
            TypeConstraint typeConstraint,
            SerializedProperty arrayProperty)
        {
            mNode = node;
            mNodeView = nodeView;
            mFieldName = fieldName;
            mValueType = valueType;
            mDirection = direction;
            mConnectionType = connectionType;
            mTypeConstraint = typeConstraint;
            mArrayProperty = arrayProperty;

            BuildUI();
            SyncPortsWithArray();
        }

        private void BuildUI()
        {
            Clear();

            // 标题栏
            var header = new VisualElement();
            header.AddToClassList("yoki-dynamic-port-list__header");

            var label = new Label(ObjectNames.NicifyVariableName(mFieldName));
            label.AddToClassList("yoki-dynamic-port-list__label");
            header.Add(label);

            var addButton = new Button(OnAddClicked) { text = "+" };
            addButton.AddToClassList("yoki-dynamic-port-list__add-btn");
            header.Add(addButton);

            Add(header);

            // 列表容器
            var listContainer = new VisualElement();
            listContainer.AddToClassList("yoki-dynamic-port-list__items");
            Add(listContainer);
        }

        private void SyncPortsWithArray()
        {
            // 获取当前动态端口
            var dynamicPorts = GetDynamicPorts();

            // 同步数组大小与端口数量
            if (mArrayProperty != default && mArrayProperty.isArray)
            {
                while (mArrayProperty.arraySize < dynamicPorts.Count)
                    mArrayProperty.InsertArrayElementAtIndex(mArrayProperty.arraySize);
                while (mArrayProperty.arraySize > dynamicPorts.Count && mArrayProperty.arraySize > 0)
                    mArrayProperty.DeleteArrayElementAtIndex(mArrayProperty.arraySize - 1);
                mArrayProperty.serializedObject.ApplyModifiedProperties();
            }

            RefreshItems();
        }

        private List<NodePort> GetDynamicPorts()
        {
            var result = new List<NodePort>();
            foreach (var port in mNode.DynamicPorts)
            {
                if (!port.FieldName.StartsWith(mFieldName + " ")) continue;
                var indexStr = port.FieldName.Substring(mFieldName.Length + 1);
                if (int.TryParse(indexStr, out _))
                    result.Add(port);
            }
            // 按索引排序
            result.Sort((a, b) =>
            {
                var aIdx = int.Parse(a.FieldName.Substring(mFieldName.Length + 1));
                var bIdx = int.Parse(b.FieldName.Substring(mFieldName.Length + 1));
                return aIdx.CompareTo(bIdx);
            });
            return result;
        }

        private void RefreshItems()
        {
            var listContainer = this.Q<VisualElement>(className: "yoki-dynamic-port-list__items");
            if (listContainer == default) return;

            listContainer.Clear();
            mItemViews.Clear();

            var dynamicPorts = GetDynamicPorts();
            for (int i = 0; i < dynamicPorts.Count; i++)
            {
                var port = dynamicPorts[i];
                var itemView = new DynamicPortItemView();
                itemView.Initialize(this, port, i, mArrayProperty);
                mItemViews.Add(itemView);
                listContainer.Add(itemView);
            }

            mNodeView.RefreshAllPorts();
        }

        private void OnAddClicked()
        {
            NodeEditorUtility.RecordUndo(mNode, "Add Dynamic Port");

            // 找到下一个可用索引
            int nextIndex = 0;
            while (mNode.HasPort($"{mFieldName} {nextIndex}"))
                nextIndex++;

            var portName = $"{mFieldName} {nextIndex}";
            if (mDirection == PortIO.Output)
                mNode.AddDynamicOutput(mValueType, portName, mConnectionType, mTypeConstraint);
            else
                mNode.AddDynamicInput(mValueType, portName, mConnectionType, mTypeConstraint);

            if (mArrayProperty != default && mArrayProperty.isArray)
            {
                mArrayProperty.InsertArrayElementAtIndex(mArrayProperty.arraySize);
                mArrayProperty.serializedObject.ApplyModifiedProperties();
            }

            NodeEditorUtility.SetDirty(mNode);
            RefreshItems();
        }

        internal void RemoveItem(int index)
        {
            var dynamicPorts = GetDynamicPorts();
            if (index < 0 || index >= dynamicPorts.Count) return;

            NodeEditorUtility.RecordUndo(mNode, "Remove Dynamic Port");

            // 清除被移除端口的连接
            dynamicPorts[index].ClearConnections();

            // 将后续端口的连接前移
            for (int k = index + 1; k < dynamicPorts.Count; k++)
            {
                var currentPort = dynamicPorts[k];
                var prevPort = dynamicPorts[k - 1];

                // 获取当前端口的所有连接
                var connections = new List<NodePort>();
                for (int j = 0; j < currentPort.ConnectionCount; j++)
                {
                    var conn = currentPort.GetConnection(j);
                    if (conn != default) connections.Add(conn);
                }

                // 断开当前端口连接，重新连接到前一个端口
                currentPort.ClearConnections();
                for (int j = 0; j < connections.Count; j++)
                    prevPort.Connect(connections[j]);
            }

            // 移除最后一个端口
            var lastPort = dynamicPorts[dynamicPorts.Count - 1];
            mNode.RemoveDynamicPort(lastPort.FieldName);

            // 同步数组
            if (mArrayProperty != default && mArrayProperty.isArray && mArrayProperty.arraySize > index)
            {
                mArrayProperty.DeleteArrayElementAtIndex(index);
                mArrayProperty.serializedObject.ApplyModifiedProperties();
            }

            NodeEditorUtility.SetDirty(mNode);
            RefreshItems();
        }

        internal void MoveItem(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex) return;
            var dynamicPorts = GetDynamicPorts();
            if (fromIndex < 0 || fromIndex >= dynamicPorts.Count) return;
            if (toIndex < 0 || toIndex >= dynamicPorts.Count) return;

            NodeEditorUtility.RecordUndo(mNode, "Reorder Dynamic Port");

            // 交换连接
            if (fromIndex < toIndex)
            {
                for (int i = fromIndex; i < toIndex; i++)
                    dynamicPorts[i].SwapConnections(dynamicPorts[i + 1]);
            }
            else
            {
                for (int i = fromIndex; i > toIndex; i--)
                    dynamicPorts[i].SwapConnections(dynamicPorts[i - 1]);
            }

            // 同步数组
            if (mArrayProperty != default && mArrayProperty.isArray)
            {
                mArrayProperty.MoveArrayElement(fromIndex, toIndex);
                mArrayProperty.serializedObject.ApplyModifiedProperties();
            }

            NodeEditorUtility.SetDirty(mNode);
            RefreshItems();
        }
    }
}
