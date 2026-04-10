#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Shared UI helpers for editor tool pages.
    /// </summary>
    public abstract partial class YokiToolPageBase
    {
        #region Toolbar

        protected VisualElement CreateToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("toolbar");
            return toolbar;
        }

        protected Button CreateToolbarPrimaryButton(string text, Action onClick)
            => YokiFrameUIComponents.CreateToolbarPrimaryButton(text, onClick);

        protected Button CreateToolbarButton(string text, Action onClick)
            => YokiFrameUIComponents.CreateToolbarButton(text, onClick);

        protected VisualElement CreateToolbarToggle(string text, bool value, Action<bool> onChanged)
        {
            var container = new VisualElement();
            container.AddToClassList("toolbar-toggle");
            if (value)
            {
                container.AddToClassList("checked");
            }

            var label = new Label(text);
            label.AddToClassList("toolbar-toggle-label");
            container.Add(label);

            container.RegisterCallback<ClickEvent>(_ =>
            {
                bool isChecked = container.ClassListContains("checked");
                if (isChecked)
                {
                    container.RemoveFromClassList("checked");
                }
                else
                {
                    container.AddToClassList("checked");
                }

                onChanged?.Invoke(!isChecked);
            });

            return container;
        }

        protected VisualElement CreateToolbarSpacer()
        {
            var spacer = new VisualElement();
            spacer.AddToClassList("toolbar-spacer");
            return spacer;
        }

        #endregion

        #region Layout

        protected (VisualElement card, VisualElement body) CreateCard(string title = null, string icon = null)
            => YokiFrameUIComponents.CreateCard(title, icon);

        protected YokiFrameUIComponents.KitPageScaffold CreateKitPageScaffold(
            string title,
            string summary,
            string iconId = null,
            string eyebrow = null,
            VisualElement headerActions = null)
            => YokiFrameUIComponents.CreateKitPageScaffold(title, summary, iconId, eyebrow, headerActions);

        protected (VisualElement panel, VisualElement body) CreateKitSectionPanel(
            string title,
            string summary = null,
            string iconId = null,
            VisualElement trailing = null)
            => YokiFrameUIComponents.CreateKitSectionPanel(title, summary, iconId, trailing);

        protected VisualElement CreateKitMetricStrip()
            => YokiFrameUIComponents.CreateKitMetricStrip();

        protected (VisualElement card, Label valueLabel) CreateKitMetricCard(
            string title,
            string value,
            string hint = null,
            Color? accentColor = null)
            => YokiFrameUIComponents.CreateKitMetricCard(title, value, hint, accentColor);

        protected VisualElement CreateContentArea(bool padded = false)
        {
            var content = new VisualElement();
            content.AddToClassList("content-area");
            if (padded)
            {
                content.AddToClassList("content-area--padded");
            }

            content.style.flexGrow = 1;
            return content;
        }

        protected TwoPaneSplitView CreateSplitView(float initialLeftWidth = 280f)
            => CreateSplitView(initialLeftWidth, null);

        protected TwoPaneSplitView CreateSplitView(float initialLeftWidth, string prefsKey)
        {
            float savedWidth = string.IsNullOrEmpty(prefsKey)
                ? initialLeftWidth
                : EditorPrefs.GetFloat(prefsKey, initialLeftWidth);

            return CreatePersistentSplitView(TwoPaneSplitViewOrientation.Horizontal, savedWidth, prefsKey, true);
        }

        protected TwoPaneSplitView CreateVerticalSplitView(float initialTopHeight, string prefsKey = null)
        {
            float savedHeight = string.IsNullOrEmpty(prefsKey)
                ? initialTopHeight
                : EditorPrefs.GetFloat(prefsKey, initialTopHeight);

            return CreatePersistentSplitView(TwoPaneSplitViewOrientation.Vertical, savedHeight, prefsKey, false);
        }

        private static TwoPaneSplitView CreatePersistentSplitView(
            TwoPaneSplitViewOrientation orientation,
            float initialDimension,
            string prefsKey,
            bool isHorizontal)
        {
            var splitView = new TwoPaneSplitView(0, initialDimension, orientation);
            splitView.AddToClassList("split-view");
            splitView.style.flexGrow = 1;

            if (!string.IsNullOrEmpty(prefsKey))
            {
                string capturedKey = prefsKey;
                splitView.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    if (splitView.fixedPane == null)
                    {
                        return;
                    }

                    float currentSize = isHorizontal
                        ? splitView.fixedPane.resolvedStyle.width
                        : splitView.fixedPane.resolvedStyle.height;

                    if (currentSize > 0f)
                    {
                        EditorPrefs.SetFloat(capturedKey, currentSize);
                    }
                });
            }

            return splitView;
        }

        protected VisualElement CreatePanelHeader(string title)
        {
            var header = new VisualElement();
            header.AddToClassList("panel-header");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("panel-title");
            header.Add(titleLabel);

            return header;
        }

        protected VisualElement CreateSectionHeader(string title, string icon = null, VisualElement trailing = null)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            if (!string.IsNullOrEmpty(icon))
            {
                var iconElement = new Image { image = KitIcons.GetTexture(icon) };
                iconElement.style.width = 16;
                iconElement.style.height = 16;
                iconElement.style.marginRight = 6;
                header.Add(iconElement);
            }

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.flexGrow = 1;
            header.Add(titleLabel);

            if (trailing != null)
            {
                header.Add(trailing);
            }

            return header;
        }

        protected VisualElement CreateHeaderRow(VisualElement leading, VisualElement trailing = null)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            if (leading != null)
            {
                leading.style.flexGrow = 1;
                header.Add(leading);
            }

            if (trailing != null)
            {
                header.Add(trailing);
            }

            return header;
        }

        protected VisualElement CreateAccentTitle(string title, Color accentColor, float markerWidth = 4f, float markerHeight = 24f)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var marker = new VisualElement();
            marker.style.width = markerWidth;
            marker.style.height = markerHeight;
            marker.style.backgroundColor = new StyleColor(accentColor);
            marker.style.borderTopLeftRadius = 2;
            marker.style.borderTopRightRadius = 2;
            marker.style.borderBottomLeftRadius = 2;
            marker.style.borderBottomRightRadius = 2;
            marker.style.marginRight = 14;
            row.Add(marker);

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 19;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new StyleColor(new Color(0.95f, 0.95f, 0.95f));
            row.Add(titleLabel);

            return row;
        }

        protected VisualElement CreateTitledPanel(string title, out VisualElement body, string panelClass = null)
        {
            var panel = new VisualElement();
            if (!string.IsNullOrEmpty(panelClass))
            {
                panel.AddToClassList(panelClass);
            }

            panel.style.flexGrow = 1;
            panel.style.flexDirection = FlexDirection.Column;
            panel.Add(CreatePanelHeader(title));

            body = new VisualElement();
            body.style.flexGrow = 1;
            body.style.flexDirection = FlexDirection.Column;
            panel.Add(body);

            return panel;
        }

        protected VisualElement CreateScrollableTitledPanel(string title, out ScrollView scrollView, string panelClass = null)
        {
            var panel = new VisualElement();
            if (!string.IsNullOrEmpty(panelClass))
            {
                panel.AddToClassList(panelClass);
            }

            panel.style.flexGrow = 1;
            panel.style.flexDirection = FlexDirection.Column;
            panel.Add(CreatePanelHeader(title));

            scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            panel.Add(scrollView);

            return panel;
        }

        #endregion

        #region Forms

        protected VisualElement CreateModernToggle(string label, bool value, Action<bool> onChanged)
            => YokiFrameUIComponents.CreateModernToggle(label, value, onChanged);

        protected (VisualElement row, Label valueLabel) CreateInfoRow(string label, string initialValue = "-")
            => YokiFrameUIComponents.CreateInfoRow(label, initialValue);

        protected (VisualElement row, TextField field) CreateIntConfigRow(
            string label,
            int value,
            Action<int> onChanged,
            int minValue = int.MinValue)
            => YokiFrameUIComponents.CreateIntConfigRow(label, value, onChanged, minValue);

        #endregion

        #region Buttons

        protected Button CreatePrimaryButton(string text, Action onClick)
            => YokiFrameUIComponents.CreatePrimaryButton(text, onClick);

        protected Button CreateSecondaryButton(string text, Action onClick)
            => YokiFrameUIComponents.CreateSecondaryButton(text, onClick);

        protected Button CreateDangerButton(string text, Action onClick)
            => YokiFrameUIComponents.CreateDangerButton(text, onClick);

        protected Button CreateToolbarButtonWithIcon(string iconId, string text, Action onClick)
            => YokiFrameUIComponents.CreateToolbarButtonWithIcon(iconId, text, onClick);

        protected Button CreateActionButtonWithIcon(string iconId, string text, Action onClick, bool isDanger = false)
            => YokiFrameUIComponents.CreateActionButtonWithIcon(iconId, text, onClick, isDanger);

        #endregion

        #region State

        protected VisualElement CreateHelpBox(string message)
            => YokiFrameUIComponents.CreateHelpBox(message);

        protected VisualElement CreateHelpBox(string message, YokiFrameUIComponents.HelpBoxType type)
            => YokiFrameUIComponents.CreateHelpBox(message, type);

        protected VisualElement CreateEmptyState(string message)
            => YokiFrameUIComponents.CreateEmptyState(KitIcons.INFO, message);

        protected VisualElement CreateEmptyState(string icon, string message, string hint = null)
            => YokiFrameUIComponents.CreateEmptyState(icon, message, hint);

        protected VisualElement CreateKitStatusBanner(
            string title,
            string message,
            YokiFrameUIComponents.HelpBoxType type = YokiFrameUIComponents.HelpBoxType.Info)
            => YokiFrameUIComponents.CreateKitStatusBanner(title, message, type);

        protected void SetStatusContent(VisualElement statusContainer, VisualElement content)
        {
            if (statusContainer == null)
            {
                return;
            }

            statusContainer.Clear();
            if (content != null)
            {
                statusContainer.Add(content);
            }
        }

        protected void SetStatusBanner(
            VisualElement statusContainer,
            string title,
            string message,
            YokiFrameUIComponents.HelpBoxType type = YokiFrameUIComponents.HelpBoxType.Info)
            => SetStatusContent(statusContainer, CreateKitStatusBanner(title, message, type));

        protected VisualElement CreateDivider()
            => YokiFrameUIComponents.CreateDivider();

        #endregion

        #region Animation

        protected void AddFadeInAnimation(VisualElement element, int delayMs = 0)
            => YokiFrameUIComponents.AddFadeInAnimation(element, delayMs);

        protected void AddSlideInAnimation(
            VisualElement element,
            YokiFrameUIComponents.SlideDirection direction,
            int delayMs = 0)
            => YokiFrameUIComponents.AddSlideInAnimation(element, direction, delayMs);

        #endregion
    }
}
#endif
