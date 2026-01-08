#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKitEditorUI - 控制台区块
    /// </summary>
    public partial class TableKitEditorUI
    {
        #region D. 控制台

        private VisualElement BuildConsole()
        {
            var container = new VisualElement();
            container.style.backgroundColor = new StyleColor(Design.LayerCard);
            container.style.borderTopLeftRadius = container.style.borderTopRightRadius = 8;
            container.style.borderBottomLeftRadius = container.style.borderBottomRightRadius = 8;
            container.style.borderLeftWidth = container.style.borderRightWidth = 1;
            container.style.borderTopWidth = container.style.borderBottomWidth = 1;
            container.style.borderLeftColor = container.style.borderRightColor = new StyleColor(Design.BorderDefault);
            container.style.borderTopColor = container.style.borderBottomColor = new StyleColor(Design.BorderDefault);
            container.style.marginBottom = 12;

            // 标题栏
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.paddingTop = 10;
            header.style.paddingBottom = 10;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(Design.BorderDefault);
            container.Add(header);

            var title = new Label("控制台");
            title.style.fontSize = Design.FontSizeSection;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(Design.TextPrimary);
            header.Add(title);

            var clearBtn = new Button(ClearLog) { text = "清除" };
            ApplySmallButtonStyle(clearBtn);
            header.Add(clearBtn);

            // 状态横幅
            mStatusBanner = new VisualElement();
            mStatusBanner.style.flexDirection = FlexDirection.Row;
            mStatusBanner.style.alignItems = Align.Center;
            mStatusBanner.style.paddingLeft = 12;
            mStatusBanner.style.paddingRight = 12;
            mStatusBanner.style.paddingTop = 6;
            mStatusBanner.style.paddingBottom = 6;
            mStatusBanner.style.backgroundColor = new StyleColor(Design.LayerElevated);
            container.Add(mStatusBanner);

            var statusIcon = new Label("●") { name = "status-icon" };
            statusIcon.style.marginRight = 6;
            statusIcon.style.color = new StyleColor(Design.BrandSuccess);
            mStatusBanner.Add(statusIcon);

            mStatusBannerLabel = new Label("就绪");
            mStatusBannerLabel.style.color = new StyleColor(Design.TextPrimary);
            mStatusBannerLabel.style.fontSize = Design.FontSizeBody;
            mStatusBanner.Add(mStatusBannerLabel);

            UpdateStatusBanner(BuildStatus.Ready);

            // 日志区
            mLogContainer = new ScrollView();
            mLogContainer.style.flexGrow = 1;
            mLogContainer.style.minHeight = 120;
            mLogContainer.style.maxHeight = 200;
            mLogContainer.style.backgroundColor = new StyleColor(Design.LayerConsole);
            mLogContainer.style.paddingLeft = 12;
            mLogContainer.style.paddingRight = 12;
            mLogContainer.style.paddingTop = 8;
            mLogContainer.style.paddingBottom = 8;
            container.Add(mLogContainer);

            // 使用 TextField 以支持文本选择和复制
            mLogContent = new TextField();
            mLogContent.multiline = true;
            mLogContent.isReadOnly = true;
            mLogContent.value = "等待操作...";
            mLogContent.style.fontSize = Design.FontSizeSmall;
            mLogContent.style.color = new StyleColor(Design.TextSecondary);
            mLogContent.style.whiteSpace = WhiteSpace.Normal;
            mLogContent.style.flexGrow = 1;
            // 移除 TextField 默认边框和背景
            mLogContent.style.borderLeftWidth = mLogContent.style.borderRightWidth = 0;
            mLogContent.style.borderTopWidth = mLogContent.style.borderBottomWidth = 0;
            mLogContent.style.backgroundColor = new StyleColor(Color.clear);
            mLogContainer.Add(mLogContent);

            return container;
        }

        private void UpdateStatusBanner(BuildStatus status)
        {
            mCurrentStatus = status;
            var icon = mStatusBanner?.Q<Label>("status-icon");

            switch (status)
            {
                case BuildStatus.Ready:
                    mStatusBannerLabel.text = "就绪";
                    mStatusBanner.style.backgroundColor = new StyleColor(Design.LayerElevated);
                    if (icon != null) icon.style.color = new StyleColor(Design.BrandSuccess);
                    break;
                case BuildStatus.Building:
                    mStatusBannerLabel.text = "生成中...";
                    mStatusBanner.style.backgroundColor = new StyleColor(new Color(0.2f, 0.25f, 0.3f));
                    if (icon != null) icon.style.color = new StyleColor(Design.BrandPrimary);
                    break;
                case BuildStatus.Success:
                    mStatusBannerLabel.text = "生成成功";
                    mStatusBanner.style.backgroundColor = new StyleColor(new Color(0.15f, 0.25f, 0.15f));
                    if (icon != null) icon.style.color = new StyleColor(Design.BrandSuccess);
                    break;
                case BuildStatus.Failed:
                    mStatusBannerLabel.text = "生成失败";
                    mStatusBanner.style.backgroundColor = new StyleColor(new Color(0.3f, 0.15f, 0.15f));
                    if (icon != null) icon.style.color = new StyleColor(Design.BrandDanger);
                    break;
            }
        }

        private void ClearLog()
        {
            mLogContent.value = "日志已清除";
            UpdateStatusBanner(BuildStatus.Ready);
        }

        #endregion
    }
}
#endif
