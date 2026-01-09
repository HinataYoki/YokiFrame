#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// 类型转换对话框 - 显示转换影响分析并执行转换
    /// </summary>
    public class TypeConvertDialog : EditorWindow
    {
        #region 常量

        private const string WINDOW_TITLE = "绑定类型转换";
        private const int MIN_WIDTH = 450;
        private const int MIN_HEIGHT = 400;

        #endregion

        #region 字段

        private AbstractBind mTargetBind;
        private BindType mTargetType;
        private TypeConvertResult mPreviewResult;

        // UI 元素
        private VisualElement mRoot;
        private VisualElement mWarningRow;
        private Label mDescriptionLabel;
        private VisualElement mFilesContainer;
        private Label mWarningLabel;
        private Button mExecuteBtn;
        private Label mResultLabel;

        #endregion

        #region 公共方法

        /// <summary>
        /// 显示类型转换对话框
        /// </summary>
        /// <param name="bind">目标 Bind 组件</param>
        /// <param name="targetType">目标类型</param>
        public static void Show(AbstractBind bind, BindType targetType)
        {
            if (bind == null) return;

            if (!BindTypeConverter.CanConvert(bind, targetType, out string reason))
            {
                EditorUtility.DisplayDialog("无法转换", reason, "确定");
                return;
            }

            var window = GetWindow<TypeConvertDialog>(true, WINDOW_TITLE, true);
            window.minSize = new Vector2(MIN_WIDTH, MIN_HEIGHT);
            window.mTargetBind = bind;
            window.mTargetType = targetType;
            window.RefreshPreview();
            window.ShowUtility();
        }

        #endregion

        #region 窗口生命周期

        private void CreateGUI()
        {
            mRoot = rootVisualElement;
            mRoot.AddToClassList("type-convert-dialog");

            // 加载样式
            var styleSheet = YokiFrameEditorUtility.LoadStyleSheetByName("TypeConvertDialogStyles");
            if (styleSheet != null)
            {
                mRoot.styleSheets.Add(styleSheet);
            }

            // 标题
            var title = new Label(WINDOW_TITLE);
            title.AddToClassList("dialog-title");
            mRoot.Add(title);

            // 转换描述
            mDescriptionLabel = new Label();
            mDescriptionLabel.AddToClassList("description-label");
            mRoot.Add(mDescriptionLabel);

            // 影响分析区域
            CreateImpactSection();

            // 警告信息
            mWarningRow = new VisualElement();
            mWarningRow.style.flexDirection = FlexDirection.Row;
            mWarningRow.style.alignItems = Align.Center;
            
            var warningIcon = new Image { image = KitIcons.GetTexture(KitIcons.WARNING) };
            warningIcon.style.width = 14;
            warningIcon.style.height = 14;
            warningIcon.style.marginRight = 4;
            mWarningRow.Add(warningIcon);
            
            mWarningLabel = new Label("此操作不可撤销，建议先提交版本控制");
            mWarningLabel.AddToClassList("warning-label");
            mWarningRow.Add(mWarningLabel);
            
            mRoot.Add(mWarningRow);

            // 操作按钮
            CreateActionButtons();

            // 结果显示
            mResultLabel = new Label();
            mResultLabel.AddToClassList("result-label");
            mRoot.Add(mResultLabel);
        }

        #endregion

        #region UI 构建

        /// <summary>
        /// 创建影响分析区域
        /// </summary>
        private void CreateImpactSection()
        {
            var section = new VisualElement();
            section.AddToClassList("impact-section");

            var header = new Label("影响分析:");
            header.AddToClassList("section-header");
            section.Add(header);

            mFilesContainer = new ScrollView();
            mFilesContainer.AddToClassList("files-container");
            section.Add(mFilesContainer);

            mRoot.Add(section);
        }

        /// <summary>
        /// 创建操作按钮
        /// </summary>
        private void CreateActionButtons()
        {
            var buttonRow = new VisualElement();
            buttonRow.AddToClassList("button-row");

            var cancelBtn = new Button(Close) { text = "取消" };
            cancelBtn.AddToClassList("cancel-btn");
            buttonRow.Add(cancelBtn);

            mExecuteBtn = new Button(ExecuteConvert) { text = "执行转换" };
            mExecuteBtn.AddToClassList("execute-btn");
            buttonRow.Add(mExecuteBtn);

            mRoot.Add(buttonRow);
        }

        #endregion

        #region 预览逻辑

        /// <summary>
        /// 刷新预览
        /// </summary>
        private void RefreshPreview()
        {
            if (mTargetBind == null) return;

            // 更新描述
            string bindName = mTargetBind.Name;
            string sourceType = GetTypeDisplayName(mTargetBind.Bind);
            string targetType = GetTypeDisplayName(mTargetType);
            mDescriptionLabel.text = $"将 \"{bindName}\" 从 {sourceType} 转换为 {targetType}";

            // 获取预览结果
            mPreviewResult = BindTypeConverter.Preview(mTargetBind, mTargetType);

            // 更新文件列表
            RefreshFilesList();

            // 更新按钮状态
            mExecuteBtn.SetEnabled(mPreviewResult.CanExecute);

            // 显示冲突警告
            if (mPreviewResult.HasNameConflict)
            {
                mWarningLabel.text = $"存在命名冲突: {mPreviewResult.ConflictFilePath}";
                mWarningLabel.AddToClassList("warning-conflict");
            }
        }

        /// <summary>
        /// 刷新文件列表
        /// </summary>
        private void RefreshFilesList()
        {
            if (mFilesContainer == null || mPreviewResult == null) return;

            mFilesContainer.Clear();

            // 需要创建的文件
            if (mPreviewResult.FilesToCreate.Count > 0)
            {
                var createSection = CreateFileSection("需要创建:", mPreviewResult.FilesToCreate, "file-create");
                mFilesContainer.Add(createSection);
            }

            // 需要修改的文件
            if (mPreviewResult.FilesToModify.Count > 0)
            {
                var modifySection = CreateFileSection("需要修改:", mPreviewResult.FilesToModify, "file-modify");
                mFilesContainer.Add(modifySection);
            }

            // 需要删除的文件
            if (mPreviewResult.FilesToDelete.Count > 0)
            {
                var deleteSection = CreateFileSection("需要删除:", mPreviewResult.FilesToDelete, "file-delete");
                mFilesContainer.Add(deleteSection);
            }

            // 无变更
            if (mPreviewResult.FilesToCreate.Count == 0 &&
                mPreviewResult.FilesToModify.Count == 0 &&
                mPreviewResult.FilesToDelete.Count == 0)
            {
                var noChangeLabel = new Label("无文件变更");
                noChangeLabel.AddToClassList("no-change-label");
                mFilesContainer.Add(noChangeLabel);
            }
        }

        /// <summary>
        /// 创建文件区块
        /// </summary>
        private VisualElement CreateFileSection(string title, System.Collections.Generic.List<string> files, string className)
        {
            var section = new VisualElement();
            section.AddToClassList("file-section");
            section.AddToClassList(className);

            var header = new Label(title);
            header.AddToClassList("file-section-header");
            section.Add(header);

            foreach (var file in files)
            {
                var fileLabel = new Label($"   {file}");
                fileLabel.AddToClassList("file-path");
                section.Add(fileLabel);
            }

            return section;
        }

        #endregion

        #region 执行逻辑

        /// <summary>
        /// 执行转换
        /// </summary>
        private void ExecuteConvert()
        {
            if (mTargetBind == null || mPreviewResult == null) return;

            // 再次确认
            bool hasFileChanges = mPreviewResult.FilesToCreate.Count > 0 ||
                                  mPreviewResult.FilesToDelete.Count > 0;

            if (hasFileChanges)
            {
                if (!EditorUtility.DisplayDialog("确认转换",
                    "此操作将修改文件系统，确定要继续吗？",
                    "执行", "取消"))
                {
                    return;
                }
            }

            // 执行转换
            var result = BindTypeConverter.Execute(mTargetBind, mTargetType);

            if (result.Success)
            {
                ShowResult("转换成功", true);

                // 延迟关闭窗口
                EditorApplication.delayCall += () =>
                {
                    if (this != null)
                        Close();
                };
            }
            else
            {
                ShowResult($"转换失败: {result.ErrorMessage}", false);
            }
        }

        /// <summary>
        /// 显示结果
        /// </summary>
        private void ShowResult(string message, bool success)
        {
            if (mResultLabel == null) return;

            mResultLabel.text = message;
            mResultLabel.RemoveFromClassList("result-success");
            mResultLabel.RemoveFromClassList("result-error");
            mResultLabel.AddToClassList(success ? "result-success" : "result-error");
            mResultLabel.style.display = DisplayStyle.Flex;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取类型显示名称
        /// </summary>
        private static string GetTypeDisplayName(BindType type)
        {
            return type switch
            {
                BindType.Member => "成员",
                BindType.Element => "元素",
                BindType.Component => "组件",
                BindType.Leaf => "叶子",
                _ => type.ToString()
            };
        }

        #endregion
    }
}
#endif
