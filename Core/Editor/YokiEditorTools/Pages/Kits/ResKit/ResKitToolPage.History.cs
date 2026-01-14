#if UNITY_EDITOR
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 工具页面 - 卸载历史记录
    /// 使用 USS 类消除内联样式
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
            if (mHistoryContainer == null) return;
            
            mHistoryContainer.Clear();
            
            // 历史记录头部
            var header = new VisualElement();
            header.AddToClassList("yoki-res-history__header");
            
            var titleIcon = new Image { image = KitIcons.GetTexture(KitIcons.TIMELINE) };
            titleIcon.AddToClassList("yoki-res-history__icon");
            header.Add(titleIcon);
            
            var titleLabel = new Label("卸载历史记录");
            titleLabel.AddToClassList("yoki-res-history__title");
            header.Add(titleLabel);
            
            var history = ResDebugger.GetUnloadHistory();
            mHistoryCountLabel = new Label($"共 {history.Count} 条");
            mHistoryCountLabel.AddToClassList("yoki-res-history__count");
            header.Add(mHistoryCountLabel);
            
            mHistoryContainer.Add(header);
            
            // 历史记录列表
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
            
            // 第一行：时间 + 类型
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
            
            // 第二行：路径
            var pathLabel = new Label(GetAssetName(record.Path));
            pathLabel.AddToClassList("yoki-res-history-item__path");
            pathLabel.tooltip = record.Path;
            item.Add(pathLabel);
            
            // 第三行：堆栈（可折叠）
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
