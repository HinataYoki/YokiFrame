#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame
{
    /// <summary>
    /// UIKitToolPage - 绑定检查功能
    /// </summary>
    public partial class UIKitToolPage
    {
        #region 字段 - 绑定检查

        private GameObject mBindTargetRoot;
        private readonly List<BindInfo> mBindInfos = new(32);
        private string mBindSearchFilter = "";
        private BindType? mBindTypeFilter;
        private bool mBindShowHierarchy = true;
        private bool mBindAutoRefresh = true;
        private double mBindLastRefreshTime;
        private const double BIND_REFRESH_INTERVAL = 1.0;
        private VisualElement mBindContent;

        /// <summary>
        /// 绑定信息缓存
        /// </summary>
        private class BindInfo
        {
            public AbstractBind Bind;
            public string Path;
            public int Depth;
            public bool HasWarning;
            public string WarningMessage;
        }

        #endregion

        #region 绑定检查 UI

        private void BuildBindInspectorUI(VisualElement container)
        {
            // 工具栏
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.paddingLeft = Spacing.SM;
            toolbar.style.paddingRight = Spacing.SM;
            toolbar.style.paddingTop = Spacing.XS;
            toolbar.style.paddingBottom = Spacing.XS;
            toolbar.style.backgroundColor = new StyleColor(Colors.LayerToolbar);
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = new StyleColor(Colors.BorderLight);
            container.Add(toolbar);

            // 目标选择
            toolbar.Add(new Label("检查目标:") { style = { unityTextAlign = TextAnchor.MiddleLeft, marginRight = Spacing.XS } });

            var targetField = new ObjectField();
            targetField.objectType = typeof(GameObject);
            targetField.value = mBindTargetRoot;
            targetField.style.width = 200;
            targetField.RegisterValueChangedCallback(evt => SetBindTarget(evt.newValue as GameObject));
            toolbar.Add(targetField);

            var selectBtn = new Button(() => SetBindTarget(Selection.activeGameObject)) { text = "选中" };
            selectBtn.style.height = 24;
            selectBtn.style.marginLeft = Spacing.XS;
            toolbar.Add(selectBtn);

            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

            // 层级视图开关
            var hierarchyToggle = CreateModernToggle("层级视图", mBindShowHierarchy, v =>
            {
                mBindShowHierarchy = v;
                RefreshBindings();
                RefreshBindContent();
            });
            toolbar.Add(hierarchyToggle);

            var refreshBtn = new Button(() => { RefreshBindings(); RefreshBindContent(); }) { text = "刷新" };
            refreshBtn.style.height = 24;
            refreshBtn.style.marginLeft = Spacing.SM;
            toolbar.Add(refreshBtn);

            // 过滤栏
            var filterBar = new VisualElement();
            filterBar.style.flexDirection = FlexDirection.Row;
            filterBar.style.paddingLeft = Spacing.SM;
            filterBar.style.paddingRight = Spacing.SM;
            filterBar.style.paddingTop = Spacing.XS;
            filterBar.style.paddingBottom = Spacing.XS;
            filterBar.style.backgroundColor = new StyleColor(Colors.LayerFilterBar);
            container.Add(filterBar);

            filterBar.Add(new Label("搜索:") { style = { unityTextAlign = TextAnchor.MiddleLeft, marginRight = Spacing.XS } });

            var searchField = new TextField();
            searchField.value = mBindSearchFilter;
            searchField.style.width = 150;
            searchField.RegisterValueChangedCallback(evt =>
            {
                mBindSearchFilter = evt.newValue;
                RefreshBindContent();
            });
            filterBar.Add(searchField);

            filterBar.Add(new Label("类型:") { style = { unityTextAlign = TextAnchor.MiddleLeft, marginLeft = Spacing.MD, marginRight = Spacing.XS } });

            var typeDropdown = new DropdownField();
            typeDropdown.choices = new List<string> { "全部", "Member", "Element", "Component", "Leaf" };
            typeDropdown.index = mBindTypeFilter.HasValue ? (int)mBindTypeFilter.Value + 1 : 0;
            typeDropdown.style.width = 100;
            typeDropdown.RegisterValueChangedCallback(evt =>
            {
                var idx = typeDropdown.choices.IndexOf(evt.newValue);
                mBindTypeFilter = idx == 0 ? null : (BindType?)(idx - 1);
                RefreshBindContent();
            });
            filterBar.Add(typeDropdown);

            // 内容区域
            mBindContent = new ScrollView();
            mBindContent.style.flexGrow = 1;
            mBindContent.style.paddingLeft = Spacing.MD;
            mBindContent.style.paddingRight = Spacing.MD;
            mBindContent.style.paddingTop = Spacing.MD;
            container.Add(mBindContent);

            RefreshBindings();
            RefreshBindContent();
        }

        #endregion
    }
}
#endif
