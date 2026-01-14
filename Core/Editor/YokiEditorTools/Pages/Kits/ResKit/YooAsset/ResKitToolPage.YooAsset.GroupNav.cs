#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YooAsset.Editor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 工具页面 - YooAsset 分组导航
    /// </summary>
    public partial class ResKitToolPage
    {
        /// <summary>分组列表容器</summary>
        private VisualElement mYooGroupListContainer;

        /// <summary>
        /// 构建分组导航面板
        /// </summary>
        private VisualElement BuildYooGroupNav()
        {
            var nav = new VisualElement();
            nav.AddToClassList("yoo-group-nav");
            nav.style.backgroundColor = new StyleColor(new Color(0.16f, 0.16f, 0.18f));
            nav.style.minWidth = 250;

            // 头部
            var header = CreatePanelHeader("分组");
            nav.Add(header);

            // 分组列表滚动容器
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            nav.Add(scrollView);

            mYooGroupListContainer = new VisualElement();
            mYooGroupListContainer.style.paddingLeft = 8;
            mYooGroupListContainer.style.paddingRight = 8;
            mYooGroupListContainer.style.paddingTop = 8;
            mYooGroupListContainer.style.paddingBottom = 8;
            scrollView.Add(mYooGroupListContainer);

            // 底部新建按钮
            var footer = new VisualElement();
            footer.style.paddingLeft = 8;
            footer.style.paddingRight = 8;
            footer.style.paddingTop = 8;
            footer.style.paddingBottom = 12;
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = new StyleColor(new Color(0.22f, 0.22f, 0.24f));
            nav.Add(footer);

            var addBtn = CreatePrimaryButton("+ 新建分组", ShowYooCreateGroupDialog);
            addBtn.style.width = new StyleLength(StyleKeyword.Auto);
            footer.Add(addBtn);

            return nav;
        }

        /// <summary>
        /// 刷新分组导航列表
        /// </summary>
        private void RefreshYooGroupNav()
        {
            if (mYooGroupListContainer == default)
                return;

            mYooGroupListContainer.Clear();

            var package = YooCurrentPackage;
            if (package == default || package.Groups == default)
            {
                var emptyState = CreateEmptyState(KitIcons.FOLDER, "暂无分组", "点击下方按钮创建分组");
                mYooGroupListContainer.Add(emptyState);
                return;
            }

            for (int i = 0; i < package.Groups.Count; i++)
            {
                var group = package.Groups[i];
                var groupItem = CreateYooGroupItem(group, i);
                mYooGroupListContainer.Add(groupItem);
            }
        }

        /// <summary>
        /// 创建分组列表项
        /// </summary>
        private VisualElement CreateYooGroupItem(AssetBundleCollectorGroup group, int index)
        {
            bool isGroupActive = group.ActiveRuleName != nameof(DisableGroup);
            int capturedIndex = index;
            var capturedGroup = group;

            var item = new VisualElement();
            item.AddToClassList("yoo-group-item");
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.paddingLeft = 8;
            item.style.paddingRight = 8;
            item.style.paddingTop = 6;
            item.style.paddingBottom = 6;
            item.style.marginBottom = 4;
            item.style.borderTopLeftRadius = 6;
            item.style.borderTopRightRadius = 6;
            item.style.borderBottomLeftRadius = 6;
            item.style.borderBottomRightRadius = 6;
            item.style.backgroundColor = new StyleColor(new Color(0.20f, 0.20f, 0.22f));

            // 选中高亮
            if (index == mYooSelectedGroupIndex)
            {
                item.style.backgroundColor = new StyleColor(new Color(0.25f, 0.45f, 0.70f));
            }

            // 分组启用开关（使用 YokiFrameUIComponents 滑动开关）
            var activeToggle = YokiFrameUIComponents.CreateModernToggle("", isGroupActive, newValue =>
            {
                capturedGroup.ActiveRuleName = newValue ? nameof(EnableGroup) : nameof(DisableGroup);
                AssetBundleCollectorSettingData.ModifyGroup(YooCurrentPackage, capturedGroup);
                MarkYooDirty();
                RefreshYooGroupNav();
            });
            activeToggle.style.marginRight = 4;
            activeToggle.tooltip = isGroupActive ? "点击禁用此分组（构建时跳过）" : "点击启用此分组";
            item.Add(activeToggle);

            // 文件夹图标
            var icon = new Image { image = KitIcons.GetTexture(KitIcons.FOLDER) };
            icon.style.width = 14;
            icon.style.height = 14;
            icon.style.marginRight = 8;
            icon.style.opacity = isGroupActive ? 1f : 0.4f;
            item.Add(icon);

            // 分组名称标签
            var nameLabel = new Label(group.GroupName);
            nameLabel.name = "group-name-label";
            nameLabel.style.flexGrow = 1;
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.textOverflow = TextOverflow.Ellipsis;
            nameLabel.style.opacity = isGroupActive ? 1f : 0.5f;
            item.Add(nameLabel);

            // 隐藏的重命名输入框
            var nameInput = new TextField();
            nameInput.name = "group-name-input";
            nameInput.style.display = DisplayStyle.None;
            nameInput.style.flexGrow = 1;
            nameInput.style.marginRight = 8;
            item.Add(nameInput);

            // Collector 数量徽章
            var badge = YokiFrameUIComponents.CreateCountLabel(
                group.Collectors.Count,
                isGroupActive ? YokiFrameUIComponents.Colors.BadgeDefault : YokiFrameUIComponents.Colors.TextTertiary
            );
            badge.style.marginLeft = 8;
            item.Add(badge);

            // 单击选中
            item.RegisterCallback<ClickEvent>(_ => SelectYooGroup(capturedIndex));

            // 双击进入编辑模式
            item.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.clickCount == 2)
                {
                    EnterYooGroupEditMode(item, capturedGroup, capturedIndex);
                    evt.StopPropagation();
                }
            });

            // 右键菜单
            item.RegisterCallback<ContextClickEvent>(evt =>
            {
                ShowYooGroupContextMenu(item, capturedGroup, capturedIndex);
                evt.StopPropagation();
            });

            // 悬停效果
            item.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (capturedIndex != mYooSelectedGroupIndex)
                    item.style.backgroundColor = new StyleColor(new Color(0.24f, 0.24f, 0.26f));
            });
            item.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (capturedIndex != mYooSelectedGroupIndex)
                    item.style.backgroundColor = new StyleColor(new Color(0.20f, 0.20f, 0.22f));
            });

            return item;
        }

        /// <summary>
        /// 选中分组
        /// </summary>
        private void SelectYooGroup(int index)
        {
            if (index == mYooSelectedGroupIndex)
                return;

            mYooSelectedGroupIndex = index;
            RefreshYooGroupNav();
            RefreshYooCollectorCanvas();
        }

        /// <summary>
        /// 显示创建分组对话框
        /// </summary>
        private void ShowYooCreateGroupDialog()
        {
            var package = YooCurrentPackage;
            if (package == default)
            {
                EditorUtility.DisplayDialog("提示", "请先选择一个资源包", "确定");
                return;
            }

            // 使用简单的输入对话框
            var groupName = "NewGroup";
            CreateYooNewGroup(groupName);
        }

        /// <summary>
        /// 显示分组右键菜单
        /// </summary>
        private void ShowYooGroupContextMenu(VisualElement item, AssetBundleCollectorGroup group, int index)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("重命名"), false, () =>
            {
                EnterYooGroupEditMode(item, group, index);
            });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("删除"), false, () =>
            {
                if (EditorUtility.DisplayDialog("确认删除", $"确定要删除分组 \"{group.GroupName}\" 吗？\n此操作不可撤销。", "删除", "取消"))
                {
                    DeleteYooGroup(group);
                }
            });

            menu.ShowAsContext();
        }

        /// <summary>
        /// 进入分组编辑模式
        /// </summary>
        private void EnterYooGroupEditMode(VisualElement item, AssetBundleCollectorGroup group, int index)
        {
            var nameLabel = item.Q<Label>("group-name-label");
            var nameInput = item.Q<TextField>("group-name-input");

            if (nameLabel == default || nameInput == default)
                return;

            mYooEditingGroupIndex = index;

            // 隐藏标签，显示输入框
            nameLabel.style.display = DisplayStyle.None;
            nameInput.style.display = DisplayStyle.Flex;
            nameInput.value = group.GroupName;

            // 延迟聚焦和选中文本
            nameInput.schedule.Execute(() =>
            {
                nameInput.Focus();
                nameInput.SelectAll();
            });

            // 注册键盘事件
            nameInput.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    SaveYooGroupRename(item, group, nameInput.value);
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    CancelYooGroupRename(item, group);
                    evt.StopPropagation();
                }
            });

            // 失去焦点时保存
            nameInput.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (mYooEditingGroupIndex == index)
                {
                    SaveYooGroupRename(item, group, nameInput.value);
                }
            });
        }

        /// <summary>
        /// 保存分组重命名
        /// </summary>
        private void SaveYooGroupRename(VisualElement item, AssetBundleCollectorGroup group, string newName)
        {
            var nameLabel = item.Q<Label>("group-name-label");
            var nameInput = item.Q<TextField>("group-name-input");

            if (!string.IsNullOrWhiteSpace(newName) && newName != group.GroupName)
            {
                group.GroupName = newName;
                AssetBundleCollectorSettingData.ModifyGroup(YooCurrentPackage, group);
                MarkYooDirty();
            }

            // 恢复显示
            nameLabel.text = group.GroupName;
            nameLabel.style.display = DisplayStyle.Flex;
            nameInput.style.display = DisplayStyle.None;
            mYooEditingGroupIndex = -1;
        }

        /// <summary>
        /// 取消分组重命名
        /// </summary>
        private void CancelYooGroupRename(VisualElement item, AssetBundleCollectorGroup group)
        {
            var nameLabel = item.Q<Label>("group-name-label");
            var nameInput = item.Q<TextField>("group-name-input");

            // 恢复显示，不保存
            nameLabel.style.display = DisplayStyle.Flex;
            nameInput.style.display = DisplayStyle.None;
            mYooEditingGroupIndex = -1;
        }
    }
}
#endif
