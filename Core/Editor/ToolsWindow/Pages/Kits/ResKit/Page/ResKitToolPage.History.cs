#if UNITY_EDITOR
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 工具页面 - 卸载历史记录。
    /// </summary>
    public partial class ResKitToolPage
    {
        private void ClearHistory()
        {
            ResDebugger.ClearUnloadHistory();
            RefreshHistoryDisplay();
        }

        private void RefreshHistoryDisplay()
        {
            if (mHistoryContainer == null)
            {
                return;
            }

            mHistoryContainer.Clear();

            var history = ResDebugger.GetUnloadHistory();
            mHistoryCountLabel = new Label($"共 {history.Count} 条");
            mHistoryCountLabel.AddToClassList("yoki-res-history__count");

            var header = CreateSectionHeader("卸载历史记录", KitIcons.TIMELINE, mHistoryCountLabel);
            header.AddToClassList("yoki-res-history__header");

            mHistoryContainer.Add(header);

            var scrollView = new ScrollView();
            scrollView.AddToClassList("yoki-res-history__list");
            mHistoryContainer.Add(scrollView);

            if (history.Count == 0)
            {
                var emptyLabel = new Label("暂无卸载记录");
                emptyLabel.AddToClassList("yoki-res-history__empty");
                scrollView.Add(emptyLabel);
                return;
            }

            foreach (var record in history)
            {
                var item = CreateHistoryItem(record);
                scrollView.Add(item);
            }
        }

        private VisualElement CreateHistoryItem(ResDebugger.UnloadRecord record)
        {
            var item = new VisualElement();
            item.AddToClassList("yoki-res-history-item");

            var row1 = new VisualElement();
            row1.AddToClassList("yoki-res-history-item__row");

            var timeLabel = new Label(record.UnloadTime.ToString("HH:mm:ss.fff"));
            timeLabel.AddToClassList("yoki-res-history-item__time");
            row1.Add(timeLabel);

            var typeLabel = new Label(record.TypeName);
            typeLabel.AddToClassList("yoki-res-history-item__type");
            typeLabel.AddToClassList(GetTypeClass(record.TypeName));
            row1.Add(typeLabel);

            item.Add(row1);

            var pathLabel = new Label(GetAssetName(record.Path));
            pathLabel.AddToClassList("yoki-res-history-item__path");
            pathLabel.tooltip = record.Path;
            item.Add(pathLabel);

            if (!string.IsNullOrEmpty(record.StackTrace) && record.StackTrace != "无可用堆栈信息")
            {
                var stackFoldout = new Foldout { text = "调用堆栈", value = false };
                stackFoldout.AddToClassList("yoki-res-history-item__stack-foldout");

                var stackLabel = new Label(record.StackTrace);
                stackLabel.AddToClassList("yoki-res-history-item__stack-content");
                stackFoldout.Add(stackLabel);

                item.Add(stackFoldout);
            }

            return item;
        }
    }
}
#endif
