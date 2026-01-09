#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame
{
    /// <summary>
    /// UIKitToolPage - 绑定检查功能 - 检查逻辑
    /// </summary>
    public partial class UIKitToolPage
    {
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
                var helpBox = CreateHelpBox("请选择一个 GameObject 或 UIPanel 作为检查目标");
                mBindContent.Add(helpBox);
                return;
            }

            DrawBindStatistics();

            if (mBindInfos.Count == 0)
            {
                mBindContent.Add(new Label("未找到 Bind 组件") { style = { color = new StyleColor(Colors.TextTertiary), marginTop = Spacing.MD } });
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
            statsRow.style.paddingTop = Spacing.SM;
            statsRow.style.paddingBottom = Spacing.SM;
            statsRow.style.paddingLeft = Spacing.SM;
            statsRow.style.paddingRight = Spacing.SM;
            statsRow.style.backgroundColor = new StyleColor(Colors.LayerToolbar);
            statsRow.style.borderTopLeftRadius = statsRow.style.borderTopRightRadius = Radius.MD;
            statsRow.style.borderBottomLeftRadius = statsRow.style.borderBottomRightRadius = Radius.MD;
            statsRow.style.marginBottom = Spacing.MD;

            statsRow.Add(new Label($"总计: {totalCount}") { style = { marginRight = Spacing.LG } });
            statsRow.Add(new Label($"Member: {memberCount}") { style = { marginRight = Spacing.LG, color = new StyleColor(Colors.StatusInfo) } });
            statsRow.Add(new Label($"Element: {elementCount}") { style = { marginRight = Spacing.LG, color = new StyleColor(Colors.StatusSuccess) } });
            statsRow.Add(new Label($"Component: {componentCount}") { style = { marginRight = Spacing.LG, color = new StyleColor(Colors.StatusWarning) } });

            if (warningCount > 0)
            {
                var warningBadge = CreateBadge(warningCount.ToString(), Colors.BadgeWarning);
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
            row.style.paddingTop = Spacing.XS;
            row.style.paddingBottom = Spacing.XS;
            row.style.paddingLeft = indent;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new StyleColor(Colors.BorderLight);

            // 警告图标
            if (info.HasWarning)
            {
                var warningIcon = new Image { image = KitIcons.GetTexture(KitIcons.WARNING) };
                warningIcon.style.width = 14;
                warningIcon.style.height = 14;
                warningIcon.style.marginRight = Spacing.XS;
                warningIcon.tintColor = Colors.StatusWarning;
                row.Add(warningIcon);
            }

            // 绑定类型标签
            var typeLabel = GetBindTypeLabel(bind.Bind);
            var typeColor = GetBindTypeColor(bind.Bind);
            var typeBadge = new Label(typeLabel);
            typeBadge.style.width = 25;
            typeBadge.style.unityTextAlign = TextAnchor.MiddleCenter;
            typeBadge.style.backgroundColor = new StyleColor(typeColor);
            typeBadge.style.borderTopLeftRadius = typeBadge.style.borderTopRightRadius = Radius.SM;
            typeBadge.style.borderBottomLeftRadius = typeBadge.style.borderBottomRightRadius = Radius.SM;
            typeBadge.style.marginRight = Spacing.SM;
            row.Add(typeBadge);

            // 字段名称
            var displayName = string.IsNullOrEmpty(bind.Name) ? bind.gameObject.name : bind.Name;
            row.Add(new Label(displayName) { style = { width = 120 } });

            // 类型
            var typeName = GetBindShortTypeName(bind.Type);
            row.Add(new Label(typeName) { style = { width = 100, fontSize = 11, color = new StyleColor(Colors.TextTertiary) } });

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
                warningRow.style.paddingBottom = Spacing.XS;
                warningRow.Add(new Label(info.WarningMessage) { style = { fontSize = 11, color = new StyleColor(Colors.StatusWarning) } });
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
                    info.WarningMessage = $"组件 {GetBindShortTypeName(bind.Type)} 不存在";
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
            BindType.Member => Colors.StatusInfo,
            BindType.Element => Colors.StatusSuccess,
            BindType.Component => Colors.StatusWarning,
            BindType.Leaf => Colors.TextTertiary,
            _ => Color.white
        };

        private static string GetBindShortTypeName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "";

            var lastDot = fullName.LastIndexOf('.');
            return lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
        }

        #endregion
    }
}
#endif
