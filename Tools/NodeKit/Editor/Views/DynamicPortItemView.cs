using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace YokiFrame.NodeKit.Editor
{
    /// <summary>
    /// Dynamic port list item view.
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

            var content = new VisualElement();
            content.AddToClassList("yoki-dynamic-port-item__content");
            content.style.flexGrow = 1;

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

            var moveUpButton = new Button(() => mListView.MoveItem(mIndex, mIndex - 1)) { text = "^" };
            moveUpButton.SetEnabled(mIndex > 0);
            Add(moveUpButton);

            var moveDownButton = new Button(() => mListView.MoveItem(mIndex, mIndex + 1)) { text = "v" };
            bool canMoveDown = mArrayProperty == default || !mArrayProperty.isArray || mIndex < mArrayProperty.arraySize - 1;
            moveDownButton.SetEnabled(canMoveDown);
            Add(moveDownButton);

            var removeButton = new Button(() => mListView.RemoveItem(mIndex)) { text = "-" };
            removeButton.AddToClassList("yoki-dynamic-port-item__remove-btn");
            Add(removeButton);
        }
    }
}
