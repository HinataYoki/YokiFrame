using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

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
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.paddingTop = 4;
            toolbar.style.paddingBottom = 4;
            toolbar.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            container.Add(toolbar);

            // 目标选择
            toolbar.Add(new Label("检查目标:") { style = { unityTextAlign = TextAnchor.MiddleLeft, marginRight = 4 } });

            var targetField = new ObjectField();
            targetField.objectType = typeof(GameObject);
            targetField.value = mBindTargetRoot;
            targetField.style.width = 200;
            targetField.RegisterValueChangedCallback(evt => SetBindTarget(evt.newValue as GameObject));
            toolbar.Add(targetField);

            var selectBtn = new Button(() => SetBindTarget(Selection.activeGameObject)) { text = "选中" };
            selectBtn.style.height = 24;
            selectBtn.style.marginLeft = 4;
            toolbar.Add(selectBtn);

            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

            // 层级视图开关
            var hierarchyToggle = YokiFrameUIComponents.CreateModernToggle("层级视图", mBindShowHierarchy, v =>
            {
                mBindShowHierarchy = v;
                RefreshBindings();
                RefreshBindContent();
            });
            toolbar.Add(hierarchyToggle);

            var refreshBtn = new Button(() => { RefreshBindings(); RefreshBindContent(); }) { text = "刷新" };
            refreshBtn.style.height = 24;
            refreshBtn.style.marginLeft = 8;
            toolbar.Add(refreshBtn);

            // 过滤栏
            var filterBar = new VisualElement();
            filterBar.style.flexDirection = FlexDirection.Row;
            filterBar.style.paddingLeft = 8;
            filterBar.style.paddingRight = 8;
            filterBar.style.paddingTop = 4;
            filterBar.style.paddingBottom = 4;
            filterBar.style.backgroundColor = new StyleColor(new Color(0.13f, 0.13f, 0.13f));
            container.Add(filterBar);

            filterBar.Add(new Label("搜索:") { style = { unityTextAlign = TextAnchor.MiddleLeft, marginRight = 4 } });

            var searchField = new TextField();
            searchField.value = mBindSearchFilter;
            searchField.style.width = 150;
            searchField.RegisterValueChangedCallback(evt =>
            {
                mBindSearchFilter = evt.newValue;
                RefreshBindContent();
            });
            filterBar.Add(searchField);

            filterBar.Add(new Label("类型:") { style = { unityTextAlign = TextAnchor.MiddleLeft, marginLeft = 12, marginRight = 4 } });

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
            mBindContent.style.paddingLeft = 12;
            mBindContent.style.paddingRight = 12;
            mBindContent.style.paddingTop = 12;
            container.Add(mBindContent);

            RefreshBindings();
            RefreshBindContent();
        }

        #endregion

        #region 绑定检查逻辑

        private void SetBindTarget(GameObject target)
        {
            if (target != null)
            {
                var panel = target.GetComponent<UIPanel>();
                if (panel == null)
                {
                    panel = target.GetComponentInParent<UIPanel>();
                }

                mBindTargetRoot = panel != null ? panel.gameObject : target;
            }
            else
            {
                mBindTargetRoot = null;
            }

            RefreshBindings();
            RefreshBindContent();
        }

        private void RefreshBindings()
        {
            mBindInfos.Clear();

            if (mBindTargetRoot == null) return;

            var binds = mBindTargetRoot.GetComponentsInChildren<AbstractBind>(true);

            for (int i = 0; i < binds.Length; i++)
            {
                var bind = binds[i];
                var info = new BindInfo
                {
                    Bind = bind,
                    Path = GetBindRelativePath(bind.transform),
                    Depth = GetBindDepth(bind.transform)
                };

                ValidateBinding(info);
                mBindInfos.Add(info);
            }

            if (mBindShowHierarchy)
            {
                mBindInfos.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal));
            }
        }

        private void RefreshBindContent()
        {
            if (mBindContent == null) return;
            mBindContent.Clear();

            if (mBindTargetRoot == null)
            {
                var helpBox = YokiFrameUIComponents.CreateHelpBox("请选择一个 GameObject 或 UIPanel 作为检查目标");
                mBindContent.Add(helpBox);
                return;
            }

            DrawBindStatistics();

            if (mBindInfos.Count == 0)
            {
                mBindContent.Add(new Label("未找到 Bind 组件") { style = { color = new StyleColor(new Color(0.5f, 0.5f, 0.5f)), marginTop = 12 } });
                return;
            }

            for (int i = 0; i < mBindInfos.Count; i++)
            {
                var info = mBindInfos[i];
                if (!PassBindFilter(info)) continue;
                DrawBindItem(info);
            }
        }

        private void DrawBindStatistics()
        {
            var totalCount = mBindInfos.Count;
            int warningCount = 0, memberCount = 0, elementCount = 0, componentCount = 0;

            for (int i = 0; i < mBindInfos.Count; i++)
            {
                var info = mBindInfos[i];
                if (info.HasWarning) warningCount++;

                switch (info.Bind.Bind)
                {
                    case BindType.Member: memberCount++; break;
                    case BindType.Element: elementCount++; break;
                    case BindType.Component: componentCount++; break;
                }
            }

            var statsRow = new VisualElement();
            statsRow.style.flexDirection = FlexDirection.Row;
            statsRow.style.paddingTop = 8;
            statsRow.style.paddingBottom = 8;
            statsRow.style.paddingLeft = 8;
            statsRow.style.paddingRight = 8;
            statsRow.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
            statsRow.style.borderTopLeftRadius = statsRow.style.borderTopRightRadius = 4;
            statsRow.style.borderBottomLeftRadius = statsRow.style.borderBottomRightRadius = 4;
            statsRow.style.marginBottom = 12;

            statsRow.Add(new Label($"总计: {totalCount}") { style = { marginRight = 16 } });
            statsRow.Add(new Label($"Member: {memberCount}") { style = { marginRight = 16, color = new StyleColor(new Color(0.5f, 0.8f, 1f)) } });
            statsRow.Add(new Label($"Element: {elementCount}") { style = { marginRight = 16, color = new StyleColor(new Color(0.5f, 1f, 0.5f)) } });
            statsRow.Add(new Label($"Component: {componentCount}") { style = { marginRight = 16, color = new StyleColor(new Color(1f, 0.8f, 0.5f)) } });

            if (warningCount > 0)
            {
                var warningBadge = new VisualElement();
                warningBadge.style.flexDirection = FlexDirection.Row;
                warningBadge.style.alignItems = Align.Center;
                
                var warningIcon = new Image { image = KitIcons.GetTexture(KitIcons.WARNING) };
                warningIcon.style.width = 14;
                warningIcon.style.height = 14;
                warningIcon.style.marginRight = 4;
                warningIcon.tintColor = new Color(1f, 0.7f, 0.3f);
                warningBadge.Add(warningIcon);
                
                var warningLabel = new Label(warningCount.ToString());
                warningLabel.style.color = new StyleColor(new Color(1f, 0.7f, 0.3f));
                warningBadge.Add(warningLabel);
                
                statsRow.Add(warningBadge);
            }

            mBindContent.Add(statsRow);
        }

        private void DrawBindItem(BindInfo info)
        {
            var bind = info.Bind;
            if (bind == null) return;

            var indent = mBindShowHierarchy ? info.Depth * 15 : 0;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 4;
            row.style.paddingBottom = 4;
            row.style.paddingLeft = indent;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));

            // 警告图标
            if (info.HasWarning)
            {
                var warningIcon = new Image { image = KitIcons.GetTexture(KitIcons.WARNING) };
                warningIcon.style.width = 14;
                warningIcon.style.height = 14;
                warningIcon.style.marginRight = 4;
                warningIcon.tintColor = new Color(1f, 0.7f, 0.3f);
                row.Add(warningIcon);
            }

            // 绑定类型标签
            var typeLabel = GetBindTypeLabel(bind.Bind);
            var typeColor = GetBindTypeColor(bind.Bind);
            var typeBadge = new Label(typeLabel);
            typeBadge.style.width = 25;
            typeBadge.style.unityTextAlign = TextAnchor.MiddleCenter;
            typeBadge.style.backgroundColor = new StyleColor(typeColor);
            typeBadge.style.borderTopLeftRadius = typeBadge.style.borderTopRightRadius = 3;
            typeBadge.style.borderBottomLeftRadius = typeBadge.style.borderBottomRightRadius = 3;
            typeBadge.style.marginRight = 8;
            row.Add(typeBadge);

            // 字段名称
            var displayName = string.IsNullOrEmpty(bind.Name) ? bind.gameObject.name : bind.Name;
            row.Add(new Label(displayName) { style = { width = 120 } });

            // 类型
            var typeName = GetShortTypeName(bind.Type);
            row.Add(new Label(typeName) { style = { width = 100, fontSize = 11, color = new StyleColor(new Color(0.6f, 0.6f, 0.6f)) } });

            row.Add(new VisualElement { style = { flexGrow = 1 } });

            // 操作按钮
            row.Add(CreateSmallButton("选择", () =>
            {
                Selection.activeGameObject = bind.gameObject;
                EditorGUIUtility.PingObject(bind.gameObject);
            }));

            row.Add(CreateSmallButton("编辑", () =>
            {
                Selection.activeGameObject = bind.gameObject;
                EditorApplication.ExecuteMenuItem("Window/General/Inspector");
            }));

            mBindContent.Add(row);

            // 显示警告信息
            if (info.HasWarning && !string.IsNullOrEmpty(info.WarningMessage))
            {
                var warningRow = new VisualElement();
                warningRow.style.paddingLeft = indent + 25;
                warningRow.style.paddingBottom = 4;
                warningRow.Add(new Label(info.WarningMessage) { style = { fontSize = 11, color = new StyleColor(new Color(1f, 0.7f, 0.3f)) } });
                mBindContent.Add(warningRow);
            }
        }

        #endregion

        #region 绑定检查辅助方法

        private void ValidateBinding(BindInfo info)
        {
            var bind = info.Bind;
            info.HasWarning = false;
            info.WarningMessage = null;

            if (bind.Bind == BindType.Leaf) return;

            if (string.IsNullOrEmpty(bind.Name))
            {
                info.HasWarning = true;
                info.WarningMessage = "字段名称为空";
                return;
            }

            if (bind.Bind != BindType.Leaf && string.IsNullOrEmpty(bind.Type))
            {
                info.HasWarning = true;
                info.WarningMessage = "类型未设置";
                return;
            }

            if (bind.Bind == BindType.Member && !string.IsNullOrEmpty(bind.Type))
            {
                var hasComponent = false;
                var components = bind.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] != null && components[i].GetType().FullName == bind.Type)
                    {
                        hasComponent = true;
                        break;
                    }
                }

                if (!hasComponent)
                {
                    info.HasWarning = true;
                    info.WarningMessage = $"组件 {GetShortTypeName(bind.Type)} 不存在";
                }
            }
        }

        private bool PassBindFilter(BindInfo info)
        {
            if (mBindTypeFilter.HasValue && info.Bind.Bind != mBindTypeFilter.Value)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(mBindSearchFilter))
            {
                var searchLower = mBindSearchFilter.ToLower();
                var nameLower = (info.Bind.Name ?? "").ToLower();
                var typeLower = (info.Bind.Type ?? "").ToLower();
                var goNameLower = info.Bind.gameObject.name.ToLower();

                if (!nameLower.Contains(searchLower) &&
                    !typeLower.Contains(searchLower) &&
                    !goNameLower.Contains(searchLower))
                {
                    return false;
                }
            }

            return true;
        }

        private string GetBindRelativePath(Transform transform)
        {
            if (transform == null || mBindTargetRoot == null) return "";

            var path = transform.name;
            var current = transform.parent;

            while (current != null && current != mBindTargetRoot.transform)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private int GetBindDepth(Transform transform)
        {
            if (transform == null || mBindTargetRoot == null) return 0;

            var depth = 0;
            var current = transform.parent;

            while (current != null && current != mBindTargetRoot.transform)
            {
                depth++;
                current = current.parent;
            }

            return depth;
        }

        private static string GetBindTypeLabel(BindType type) => type switch
        {
            BindType.Member => "M",
            BindType.Element => "E",
            BindType.Component => "C",
            BindType.Leaf => "L",
            _ => "?"
        };

        private static Color GetBindTypeColor(BindType type) => type switch
        {
            BindType.Member => new Color(0.5f, 0.8f, 1f),
            BindType.Element => new Color(0.5f, 1f, 0.5f),
            BindType.Component => new Color(1f, 0.8f, 0.5f),
            BindType.Leaf => new Color(0.7f, 0.7f, 0.7f),
            _ => Color.white
        };

        private static string GetShortTypeName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "";

            var lastDot = fullName.LastIndexOf('.');
            return lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
        }

        #endregion
    }
}
