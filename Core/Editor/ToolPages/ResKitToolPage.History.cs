#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 工具页面 - 卸载历史记录
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
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.paddingTop = 8;
            header.style.paddingBottom = 8;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(new Color(0.22f, 0.22f, 0.24f));
            
            var titleIcon = new Image { image = KitIcons.GetTexture(KitIcons.TIMELINE) };
            titleIcon.style.width = 14;
            titleIcon.style.height = 14;
            titleIcon.style.marginRight = 4;
            header.Add(titleIcon);
            
            var titleLabel = new Label("卸载历史记录");
            titleLabel.style.fontSize = 13;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.92f));
            titleLabel.style.flexGrow = 1;
            header.Add(titleLabel);
            
            var history = ResDebugger.GetUnloadHistory();
            mHistoryCountLabel = new Label($"共 {history.Count} 条");
            mHistoryCountLabel.style.fontSize = 11;
            mHistoryCountLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.65f));
            header.Add(mHistoryCountLabel);
            
            mHistoryContainer.Add(header);
            
            // 历史记录列表
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.style.maxHeight = 300;
            mHistoryContainer.Add(scrollView);
            
            if (history.Count == 0)
            {
                var emptyLabel = new Label("暂无卸载记录");
                emptyLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.55f));
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                emptyLabel.style.paddingTop = 20;
                emptyLabel.style.paddingBottom = 20;
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
            item.style.marginLeft = 8;
            item.style.marginRight = 8;
            item.style.marginTop = 4;
            item.style.marginBottom = 4;
            item.style.paddingLeft = 12;
            item.style.paddingRight = 12;
            item.style.paddingTop = 10;
            item.style.paddingBottom = 10;
            item.style.backgroundColor = new StyleColor(new Color(0.14f, 0.14f, 0.16f));
            item.style.borderTopLeftRadius = item.style.borderTopRightRadius = 6;
            item.style.borderBottomLeftRadius = item.style.borderBottomRightRadius = 6;
            item.style.borderLeftWidth = 3;
            item.style.borderLeftColor = new StyleColor(new Color(0.9f, 0.4f, 0.4f));
            
            // 第一行：时间 + 类型
            var row1 = new VisualElement();
            row1.style.flexDirection = FlexDirection.Row;
            row1.style.alignItems = Align.Center;
            row1.style.marginBottom = 4;
            
            var timeLabel = new Label(record.UnloadTime.ToString("HH:mm:ss.fff"));
            timeLabel.style.fontSize = 10;
            timeLabel.style.color = new StyleColor(new Color(0.55f, 0.55f, 0.6f));
            timeLabel.style.marginRight = 8;
            row1.Add(timeLabel);
            
            var typeColor = GetTypeColor(record.TypeName);
            var typeLabel = new Label(record.TypeName);
            typeLabel.style.fontSize = 10;
            typeLabel.style.color = new StyleColor(typeColor);
            typeLabel.style.backgroundColor = new StyleColor(new Color(typeColor.r, typeColor.g, typeColor.b, 0.15f));
            typeLabel.style.paddingLeft = 6;
            typeLabel.style.paddingRight = 6;
            typeLabel.style.paddingTop = 2;
            typeLabel.style.paddingBottom = 2;
            typeLabel.style.borderTopLeftRadius = typeLabel.style.borderTopRightRadius = 4;
            typeLabel.style.borderBottomLeftRadius = typeLabel.style.borderBottomRightRadius = 4;
            row1.Add(typeLabel);
            
            item.Add(row1);
            
            // 第二行：路径
            var pathLabel = new Label(GetAssetName(record.Path));
            pathLabel.style.fontSize = 12;
            pathLabel.style.color = new StyleColor(new Color(0.85f, 0.85f, 0.88f));
            pathLabel.style.marginBottom = 4;
            pathLabel.tooltip = record.Path;
            item.Add(pathLabel);
            
            // 第三行：堆栈（可折叠）
            if (!string.IsNullOrEmpty(record.StackTrace) && record.StackTrace != "无可用堆栈信息")
            {
                var stackFoldout = new Foldout { text = "调用堆栈", value = false };
                stackFoldout.style.fontSize = 10;
                stackFoldout.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.65f));
                
                var stackLabel = new Label(record.StackTrace);
                stackLabel.style.fontSize = 10;
                stackLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.55f));
#if UNITY_6000_0_OR_NEWER
                stackLabel.style.whiteSpace = WhiteSpace.PreWrap;
#else
                stackLabel.style.whiteSpace = WhiteSpace.Normal;
#endif
                stackLabel.style.paddingLeft = 8;
                stackFoldout.Add(stackLabel);
                
                item.Add(stackFoldout);
            }
            
            return item;
        }
    }
}
#endif
