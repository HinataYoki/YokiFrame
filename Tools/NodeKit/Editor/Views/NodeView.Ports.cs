using System.Collections.Generic;

namespace YokiFrame.NodeKit.Editor
{
    public partial class NodeView
    {
        /// <summary>
        /// 构建端口
        /// </summary>
        private void BuildPorts()
        {
            inputContainer.Clear();
            outputContainer.Clear();
            mPortViews.Clear();

            foreach (var port in mTarget.Ports)
            {
                var portView = PortView.Create(port, this);
                mPortViews[port.FieldName] = portView;

                if (port.IsInput)
                    inputContainer.Add(portView);
                else
                    outputContainer.Add(portView);
            }
        }

        /// <summary>
        /// 刷新端口
        /// </summary>
        public void RefreshAllPorts()
        {
            // 收集需要移除的端口
            var toRemove = new List<string>();
            foreach (var kvp in mPortViews)
            {
                var port = mTarget.GetPort(kvp.Key);
                if (port == default)
                    toRemove.Add(kvp.Key);
            }

            // 移除不存在的端口视图
            for (int i = 0; i < toRemove.Count; i++)
            {
                var fieldName = toRemove[i];
                var portView = mPortViews[fieldName];
                
                if (portView.direction == UnityEditor.Experimental.GraphView.Direction.Input)
                    inputContainer.Remove(portView);
                else
                    outputContainer.Remove(portView);
                
                mPortViews.Remove(fieldName);
            }

            // 添加新端口
            foreach (var port in mTarget.Ports)
            {
                if (mPortViews.ContainsKey(port.FieldName)) continue;

                var portView = PortView.Create(port, this);
                mPortViews[port.FieldName] = portView;

                if (port.IsInput)
                    inputContainer.Add(portView);
                else
                    outputContainer.Add(portView);
            }

            RefreshPorts();
        }

        /// <summary>
        /// 刷新端口颜色
        /// </summary>
        public void RefreshPortColors()
        {
            foreach (var portView in mPortViews.Values)
                portView.RefreshColor();
        }
    }
}
