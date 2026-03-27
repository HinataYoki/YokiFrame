using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace YokiFrame.NodeKit.Editor
{
    /// <summary>
    /// 动态端口列表项视图
    /// </summary>
    public class DynamicPortItemView : VisualElement
    {
        private DynamicPortListView mListView;
        private NodePort mPort;
        private int mIndex;
        private SerializedProperty mArrayProperty;

        public DynamicPortItemView()
        {
            AddToClassList("yoki-dynamic-port-item");
        }

        /// <summary>
        /// 初始化列表项
        /// </summary>
        public void Initialize(
            DynamicPortListView listView,
            NodePort port,
            int index,
            SerializedProperty arrayProperty)
        {
            mListView = listView;
            mPort = port;
            mIndex = index;
            mArrayProperty = arrayProperty;

            BuildUI();
        }

        private void BuildUI()
        {
            Clear();

            // 拖拽手柄
            var dragHandle = new VisualElement();
            dragHandle.AddToClassList("yoki-dynamic-port-item__drag-handle");
            dragHandle.RegisterCallback<PointerDownEvent>(OnDragStart);
            Add(dragHandle);

            // 内容区域
            var content = new VisualElement();
            content.AddToClassList("yoki-dynamic-port-item__content");
            content.style.flexGrow = 1;

            // 如果有数组属性，显示属性字段
            if (mArrayProperty != default && mArrayProperty.isArray && mArrayProperty.arraySize > mIndex)
            {
                var itemProperty = mArrayProperty.GetArrayElementAtIndex(mIndex);
                var propertyField = new PropertyField(itemProperty);
                propertyField.Bind(mArrayProperty.serializedObject);
                content.Add(propertyField);
            }
            else
            {
                var label = new Label(mPort.FieldName);
                label.AddToClassList("yoki-dynamic-port-item__label");
                content.Add(label);
            }

            Add(content);

            // 删除按钮
            var removeButton = new Button(() => mListView.RemoveItem(mIndex)) { text = "-" };
            removeButton.AddToClassList("yoki-dynamic-port-item__remove-btn");
            Add(removeButton);
        }

        private void OnDragStart(PointerDownEvent evt)
        {
            // TODO: 实现拖拽排序
            // 当前简化实现，后续可添加完整拖拽功能
        }
    }
}
