using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.NodeKit.Editor
{
    /// <summary>
    /// 节点编辑器基类
    /// </summary>
    public class NodeEditorBase
    {
        private Node mTarget;
        private SerializedObject mSerializedObject;
        private NodeGraphView mGraphView;
        private NodeView mNodeView;

        public Node Target => mTarget;
        public SerializedObject SerializedObject => mSerializedObject;
        public NodeGraphView GraphView => mGraphView;
        public NodeView NodeView => mNodeView;

        /// <summary>
        /// 初始化编辑器
        /// </summary>
        internal void Initialize(Node target, NodeGraphView graphView)
        {
            mTarget = target;
            mGraphView = graphView;
            mSerializedObject = new SerializedObject(target);
            OnEnable();
        }

        /// <summary>
        /// 设置节点视图引用
        /// </summary>
        internal void SetNodeView(NodeView nodeView) => mNodeView = nodeView;

        /// <summary>
        /// 编辑器启用回调
        /// </summary>
        protected virtual void OnEnable() { }

        /// <summary>
        /// 构建头部 UI
        /// </summary>
        public virtual void OnHeaderGUI(VisualElement container)
        {
            var label = new Label(mTarget.name);
            label.AddToClassList("yoki-node__title");
            container.Add(label);
        }

        /// <summary>
        /// 构建主体 UI
        /// </summary>
        public virtual void OnBodyGUI(VisualElement container)
        {
            mSerializedObject.Update();
            var iterator = mSerializedObject.GetIterator();
            iterator.NextVisible(true); // 跳过 m_Script

            while (iterator.NextVisible(false))
            {
                // 跳过内部字段
                if (iterator.name is "mGraph" or "mPosition" or "mPorts" or "mPortKeys")
                    continue;

                // 检查是否为动态端口列表
                if (IsDynamicPortListField(iterator.name, out var portInfo))
                {
                    AddDynamicPortList(container, iterator.name, portInfo);
                    continue;
                }

                var field = new PropertyField(iterator.Copy());
                field.Bind(mSerializedObject);
                container.Add(field);
            }
        }

        /// <summary>
        /// 添加动态端口列表
        /// </summary>
        protected void AddDynamicPortList(VisualElement container, string fieldName, PortFieldInfo portInfo)
        {
            var arrayProperty = mSerializedObject.FindProperty(fieldName);
            var listView = new DynamicPortListView();
            listView.Initialize(
                mTarget,
                mNodeView,
                fieldName,
                portInfo.ValueType,
                portInfo.Direction,
                portInfo.ConnectionType,
                portInfo.TypeConstraint,
                arrayProperty);
            container.Add(listView);
        }

        private bool IsDynamicPortListField(string fieldName, out PortFieldInfo info)
        {
            info = default;
            var field = mTarget.GetType().GetField(fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == default) return false;

            var inputAttr = field.GetCustomAttribute<InputAttribute>();
            if (inputAttr != default && inputAttr.DynamicPortList)
            {
                var typeOverride = field.GetCustomAttribute<PortTypeOverrideAttribute>();
                info = new PortFieldInfo
                {
                    ValueType = GetElementType(typeOverride != default ? typeOverride.Type : field.FieldType),
                    Direction = PortIO.Input,
                    ConnectionType = inputAttr.ConnectionType,
                    TypeConstraint = inputAttr.TypeConstraint
                };
                return true;
            }

            var outputAttr = field.GetCustomAttribute<OutputAttribute>();
            if (outputAttr != default && outputAttr.DynamicPortList)
            {
                var typeOverride = field.GetCustomAttribute<PortTypeOverrideAttribute>();
                info = new PortFieldInfo
                {
                    ValueType = GetElementType(typeOverride != default ? typeOverride.Type : field.FieldType),
                    Direction = PortIO.Output,
                    ConnectionType = outputAttr.ConnectionType,
                    TypeConstraint = outputAttr.TypeConstraint
                };
                return true;
            }

            return false;
        }

        private static Type GetElementType(Type type)
        {
            if (type.IsArray)
                return type.GetElementType();
            if (type.IsGenericType)
                return type.GetGenericArguments()[0];
            return type;
        }

        /// <summary>
        /// 构建右键菜单
        /// </summary>
        public virtual void BuildContextMenu(ContextualMenuPopulateEvent evt) { }

        /// <summary>
        /// 获取节点着色
        /// </summary>
        public virtual Color GetTint()
        {
            var attr = mTarget.GetType().GetCustomAttributes(typeof(NodeTintAttribute), true);
            if (attr.Length > 0 && attr[0] is NodeTintAttribute tint)
                return new Color(tint.R, tint.G, tint.B);
            return new Color(0.35f, 0.35f, 0.35f);
        }

        /// <summary>
        /// 获取节点宽度
        /// </summary>
        public virtual int GetWidth()
        {
            var attr = mTarget.GetType().GetCustomAttributes(typeof(NodeWidthAttribute), true);
            if (attr.Length > 0 && attr[0] is NodeWidthAttribute width)
                return width.Width;
            return 200;
        }

        /// <summary>
        /// 重命名回调
        /// </summary>
        public virtual void OnRename() { }

        /// <summary>
        /// 端口字段信息
        /// </summary>
        protected struct PortFieldInfo
        {
            public Type ValueType;
            public PortIO Direction;
            public ConnectionType ConnectionType;
            public TypeConstraint TypeConstraint;
        }
    }
}
