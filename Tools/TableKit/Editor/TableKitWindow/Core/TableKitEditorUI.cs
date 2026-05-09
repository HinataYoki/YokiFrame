#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKit 编辑器 UI 核心逻辑
    /// 可被独立窗口或 YokiFrame 工具页面复用
    /// </summary>
    public partial class TableKitEditorUI
    {
        #region 设计令牌常量

        private static class Design
        {
            // 品牌色
            public static readonly Color BrandPrimary = new(0.13f, 0.59f, 0.95f);
            public static readonly Color BrandPrimaryHover = new(0.26f, 0.65f, 0.96f);
            public static readonly Color BrandSuccess = new(0.30f, 0.69f, 0.31f);
            public static readonly Color BrandDanger = new(0.96f, 0.26f, 0.21f);
            public static readonly Color BrandWarning = new(1f, 0.60f, 0f);

            // 层级色
            public static readonly Color LayerCard = new(0.18f, 0.18f, 0.21f);
            public static readonly Color LayerElevated = new(0.20f, 0.22f, 0.24f);
            public static readonly Color LayerHover = new(0.23f, 0.24f, 0.27f);
            public static readonly Color LayerBackground = new(0.12f, 0.12f, 0.14f);
            public static readonly Color LayerConsole = new(0.08f, 0.08f, 0.10f);

            // 文本色
            public static readonly Color TextPrimary = new(0.94f, 0.94f, 0.96f);
            public static readonly Color TextSecondary = new(0.71f, 0.73f, 0.76f);
            public static readonly Color TextTertiary = new(0.51f, 0.53f, 0.57f);

            // 边框色
            public static readonly Color BorderDefault = new(0.22f, 0.23f, 0.25f);
            public static readonly Color BorderLight = new(0.28f, 0.28f, 0.30f);
            public static readonly Color BorderValid = new(0.30f, 0.69f, 0.31f, 0.6f);
            public static readonly Color BorderInvalid = new(0.96f, 0.26f, 0.21f, 0.6f);

            // 字体大小
            public const int FontSizeTitle = 16;        // 主标题
            public const int FontSizeSection = 14;      // 区块标题
            public const int FontSizeBody = 13;         // 正文
            public const int FontSizeSmall = 12;        // 小字/提示
            public const int FontSizeCode = 11;         // 代码块
        }

        #endregion

        #region 构建状态枚举

        private enum BuildStatus { Ready, Building, Success, Failed }

        #endregion

        #region 配置参数

        private string mEditorDataPath;
        private string mRuntimePathPattern;
        private string mLubanWorkDir;
        private string mLubanDllPath;
        private string mTarget;
        private string mCodeTarget;
        private string mDataTarget;
        private string mOutputDataDir;
        private string mOutputCodeDir;
        private bool mUseAssemblyDefinition;
        private string mAssemblyName;
        private bool mGenerateExternalTypeUtil;
        private bool mUseAsyncLoading;
        private bool mCustomEditorDataPath;

        #endregion

        #region UI 元素引用

        private TextField mEditorDataPathField;
        private TextField mRuntimePathPatternField;
        private TextField mLubanWorkDirField;
        private TextField mLubanDllPathField;
        private DropdownField mTargetDropdown;
        private DropdownField mCodeTargetDropdown;
        private DropdownField mDataTargetDropdown;
        private TextField mOutputDataDirField;
        private TextField mOutputCodeDirField;
        private VisualElement mUseAssemblyToggle;
        private TextField mAssemblyNameField;
        private VisualElement mGenerateExternalTypeUtilToggle;
        private VisualElement mUseAsyncLoadingToggle;
        private VisualElement mCustomEditorDataPathToggle;
        private VisualElement mEditorDataPathRow;
        private VisualElement mConfigFoldout;
        private VisualElement mConfigStatusDot;
        private VisualElement mStatusBanner;
        private Label mStatusBannerLabel;
        private VisualElement mLogContainer;
        private TextField mLogContent;
        private VisualElement mDataPreviewContainer;
        private VisualElement mTablesInfoContainer;
        private Button mGenerateBtn;
        private BuildStatus mCurrentStatus = BuildStatus.Ready;

        #endregion

        #region 下拉选项

        private static readonly string[] TARGET_OPTIONS = { "client", "server", "all" };
        private static readonly string[] CODE_TARGET_OPTIONS = { "cs-bin", "cs-simple-json", "cs-newtonsoft-json" };
        private static readonly string[] DATA_TARGET_OPTIONS = { "bin", "json", "lua" };

        #endregion

        /// <summary>
        /// 构建完整 UI
        /// </summary>
        public VisualElement BuildUI()
        {
            LoadPrefs();

            var root = new VisualElement();
            root.style.flexGrow = 1;
            root.style.backgroundColor = new StyleColor(Design.LayerBackground);

            // 命令中心固定在顶部，不随滚动
            var commandCenter = BuildCommandCenter();
            commandCenter.style.marginLeft = 16;
            commandCenter.style.marginRight = 16;
            commandCenter.style.marginTop = 12;
            root.Add(commandCenter);

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.style.paddingLeft = 16;
            scrollView.style.paddingRight = 16;
            scrollView.style.paddingTop = 12;
            root.Add(scrollView);

            scrollView.Add(BuildConfigFoldout());
            scrollView.Add(BuildBuildOptions());
            scrollView.Add(BuildConsole());
            scrollView.Add(BuildDataPreview());
            scrollView.Add(BuildTablesInfo());
            scrollView.Add(BuildUsageGuide());

            RefreshConfigStatus();

            return root;
        }

        /// <summary>
        /// 刷新状态 (供外部调用)
        /// </summary>
        public void RefreshStatus() => RefreshConfigStatus();

        #region 配置持久化

        private void LoadPrefs()
        {
            LoadUserPrefs();

            mEditorDataPath = mUserPrefs.editorDataPath;
            mRuntimePathPattern = mUserPrefs.runtimePathPattern;
            mLubanWorkDir = mUserPrefs.lubanWorkDir;
            mLubanDllPath = mUserPrefs.lubanDllPath;
            mTarget = mUserPrefs.target;
            mCodeTarget = mUserPrefs.codeTarget;
            mDataTarget = mUserPrefs.dataTarget;
            mOutputDataDir = mUserPrefs.outputDataDir;
            mOutputCodeDir = mUserPrefs.outputCodeDir;
            mUseAssemblyDefinition = mUserPrefs.useAssemblyDefinition;
            mAssemblyName = mUserPrefs.assemblyName;
            mGenerateExternalTypeUtil = mUserPrefs.generateExternalTypeUtil;
            mUseAsyncLoading = mUserPrefs.useAsyncLoading;
            mCustomEditorDataPath = mUserPrefs.customEditorDataPath;

            // 未开启自定义编辑器数据路径时，跟随数据输出目录
            if (!mCustomEditorDataPath)
            {
                mEditorDataPath = mOutputDataDir;
            }

            // 加载多目标输出配置
            LoadExtraOutputTargets();
        }

        /// <summary>
        /// 加载持久化的控制台日志
        /// </summary>
        private string LoadConsoleLog() => mUserPrefs?.consoleLog ?? "等待操作...";

        public void SavePrefs()
        {
            SyncToUserPrefs();
            SaveUserPrefs();
        }

        /// <summary>
        /// 保存控制台日志（独立方法，供生成/验证后调用）
        /// </summary>
        private void SaveConsoleLog()
        {
            if (mUserPrefs == default || mLogContent == default) return;
            mUserPrefs.consoleLog = mLogContent.value;
            SaveUserPrefs();
        }

        /// <summary>
        /// 将当前 m* 字段同步到持久化数据容器
        /// </summary>
        private void SyncToUserPrefs()
        {
            if (mUserPrefs == default) return;
            mUserPrefs.editorDataPath = mEditorDataPath;
            mUserPrefs.runtimePathPattern = mRuntimePathPattern;
            mUserPrefs.lubanWorkDir = mLubanWorkDir;
            mUserPrefs.lubanDllPath = mLubanDllPath;
            mUserPrefs.target = mTarget;
            mUserPrefs.codeTarget = mCodeTarget;
            mUserPrefs.dataTarget = mDataTarget;
            mUserPrefs.outputDataDir = mOutputDataDir;
            mUserPrefs.outputCodeDir = mOutputCodeDir;
            mUserPrefs.useAssemblyDefinition = mUseAssemblyDefinition;
            mUserPrefs.assemblyName = mAssemblyName;
            mUserPrefs.generateExternalTypeUtil = mGenerateExternalTypeUtil;
            mUserPrefs.useAsyncLoading = mUseAsyncLoading;
            mUserPrefs.customEditorDataPath = mCustomEditorDataPath;
            if (mLogContent != default)
            {
                mUserPrefs.consoleLog = mLogContent.value;
            }
        }

        /// <summary>
        /// 还原所有配置为默认值
        /// </summary>
        private void ResetToDefaults()
        {
            if (!EditorUtility.DisplayDialog("还原默认设置", "确定要将所有配置还原为默认值吗？", "确定", "取消"))
                return;

            // 设置默认值
            mEditorDataPath = "Assets/Resources/Art/Table/";
            mRuntimePathPattern = "Art/Table/{0}";
            mLubanWorkDir = "Luban/MiniTemplate";
            mLubanDllPath = "Luban/Tools/Luban/Luban.dll";
            mTarget = "client";
            mCodeTarget = "cs-bin";
            mDataTarget = "bin";
            mOutputDataDir = "Assets/Resources/Art/Table/";
            mOutputCodeDir = "Assets/Scripts/TableKit/";
            mUseAssemblyDefinition = false;
            mAssemblyName = "YokiFrame.TableKit";
            mGenerateExternalTypeUtil = false;
            mUseAsyncLoading = false;
            mCustomEditorDataPath = false;
            mEditorDataPath = mOutputDataDir;
            
            // 清空多目标输出列表
            mExtraOutputTargets.Clear();
            SaveExtraOutputTargets();
            RefreshExtraOutputList();

            // 更新 UI - 文本框
            mEditorDataPathField.value = mEditorDataPath;
            mRuntimePathPatternField.value = mRuntimePathPattern;
            mLubanWorkDirField.value = mLubanWorkDir;
            mLubanDllPathField.value = mLubanDllPath;
            mTargetDropdown.value = mTarget;
            mCodeTargetDropdown.value = mCodeTarget;
            mDataTargetDropdown.value = mDataTarget;
            mOutputDataDirField.value = mOutputDataDir;
            mOutputCodeDirField.value = mOutputCodeDir;
            mAssemblyNameField.value = mAssemblyName;
            mAssemblyNameField.SetEnabled(mUseAssemblyDefinition);

            // 更新 UI - Toggle 开关
            UpdateCapsuleToggle(mUseAssemblyToggle, mUseAssemblyDefinition);
            UpdateCapsuleToggle(mGenerateExternalTypeUtilToggle, mGenerateExternalTypeUtil);
            UpdateCapsuleToggle(mUseAsyncLoadingToggle, mUseAsyncLoading);
            UpdateCapsuleToggle(mCustomEditorDataPathToggle, mCustomEditorDataPath);
            mEditorDataPathField.value = mEditorDataPath;
            mEditorDataPathRow.SetEnabled(mCustomEditorDataPath);

            // 保存并刷新
            SavePrefs();
            RefreshConfigStatus();

            mLogContent.value = $"[{System.DateTime.Now:HH:mm:ss}] 已还原为默认设置";
        }

        #endregion
    }
}
#endif
