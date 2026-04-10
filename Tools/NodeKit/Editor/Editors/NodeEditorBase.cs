using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.NodeKit.Editor
{
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

        internal void Initialize(Node target, NodeGraphView graphView)
        {
            mTarget = target;
            mGraphView = graphView;
            mSerializedObject = new SerializedObject(target);
            OnEnable();
        }

        internal void SetNodeView(NodeView nodeView) => mNodeView = nodeView;

        protected virtual void OnEnable() { }

        public virtual void OnHeaderGUI(VisualElement container)
        {
            var label = new Label(mTarget.name);
            label.AddToClassList("yoki-node__title");
            container.Add(label);
        }

        public virtual void OnBodyGUI(VisualElement container)
        {
            mSerializedObject.Update();
            var iterator = mSerializedObject.GetIterator();
            iterator.NextVisible(true);

            while (iterator.NextVisible(false))
            {
                if (iterator.name is "mGraph" or "mPosition" or "mPorts" or "mPortKeys")
                    continue;

                if (IsDynamicPortListField(iterator.name, out var portInfo))
                {
                    AddDynamicPortList(container, iterator.name, portInfo);
                    continue;
                }

                if (!ShouldDrawBackingField(iterator.name))
                {
                    AddPortLabel(container, iterator.Copy());
                    continue;
                }

                var field = new PropertyField(iterator.Copy());
                field.Bind(mSerializedObject);
                field.AddToClassList("yoki-node__property");
                container.Add(field);
            }

            foreach (var dynamicPort in mTarget.DynamicPorts)
            {
                if (IsDynamicPortListPort(dynamicPort))
                    continue;

                AddDynamicPortLabel(container, dynamicPort);
            }
        }

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

        public virtual void BuildContextMenu(ContextualMenuPopulateEvent evt) { }

        public virtual Color GetTint()
        {
            var attr = mTarget.GetType().GetCustomAttributes(typeof(NodeTintAttribute), true);
            if (attr.Length > 0 && attr[0] is NodeTintAttribute tint)
                return new Color(tint.R, tint.G, tint.B);
            return NodePreferences.TintColor;
        }

        public virtual int GetWidth()
        {
            var attr = mTarget.GetType().GetCustomAttributes(typeof(NodeWidthAttribute), true);
            if (attr.Length > 0 && attr[0] is NodeWidthAttribute width)
                return width.Width;
            return 200;
        }

        public virtual string GetHeaderTooltip() => mTarget?.GetType().FullName;

        public virtual void OnRename() { }

        protected virtual void AddPortLabel(VisualElement container, SerializedProperty property)
        {
            var label = new Label(property.displayName);
            label.AddToClassList("yoki-node__port-label");
            container.Add(label);
        }

        protected virtual void AddDynamicPortLabel(VisualElement container, NodePort dynamicPort)
        {
            var label = new Label(ObjectNames.NicifyVariableName(dynamicPort.FieldName));
            label.AddToClassList("yoki-node__port-label");
            container.Add(label);
        }

        private bool IsDynamicPortListPort(NodePort dynamicPort)
        {
            if (dynamicPort == default || string.IsNullOrWhiteSpace(dynamicPort.FieldName))
                return false;

            int separatorIndex = dynamicPort.FieldName.LastIndexOf(' ');
            if (separatorIndex <= 0 || separatorIndex >= dynamicPort.FieldName.Length - 1)
                return false;

            string fieldName = dynamicPort.FieldName[..separatorIndex];
            string suffix = dynamicPort.FieldName[(separatorIndex + 1)..];
            if (!int.TryParse(suffix, out _))
                return false;

            return IsDynamicPortListField(fieldName, out _);
        }

        private bool IsDynamicPortListField(string fieldName, out PortFieldInfo info)
        {
            info = default;
            var field = mTarget.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
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

        private bool ShouldDrawBackingField(string fieldName)
        {
            var field = mTarget.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == default) return true;

            var inputAttr = field.GetCustomAttribute<InputAttribute>();
            if (inputAttr != default)
                return ShouldShowBackingValue(fieldName, inputAttr.BackingValue);

            var outputAttr = field.GetCustomAttribute<OutputAttribute>();
            if (outputAttr != default)
                return ShouldShowBackingValue(fieldName, outputAttr.BackingValue);

            return true;
        }

        private bool ShouldShowBackingValue(string fieldName, ShowBackingValue backingValue)
        {
            return backingValue switch
            {
                ShowBackingValue.Always => true,
                ShowBackingValue.Never => false,
                ShowBackingValue.Unconnected => !(mTarget.GetPort(fieldName)?.IsConnected ?? false),
                _ => true
            };
        }

        private static Type GetElementType(Type type)
        {
            if (type.IsArray)
                return type.GetElementType();
            if (type.IsGenericType)
                return type.GetGenericArguments()[0];
            return type;
        }

        protected struct PortFieldInfo
        {
            public Type ValueType;
            public PortIO Direction;
            public ConnectionType ConnectionType;
            public TypeConstraint TypeConstraint;
        }
    }
}
