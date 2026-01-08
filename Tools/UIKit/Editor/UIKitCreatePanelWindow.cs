using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// UIKit 面板创建窗口
    /// </summary>
    public class UIKitCreatePanelWindow : EditorWindow
    {
        private static readonly string WINDOW_NAME = "YokiFrame_UI创建窗口";
        private static readonly string ASSETS_PREFIX = "Assets";
        private static readonly string ALREADY_EXIST_LABEL = "<color=red>[已存在]</color>";

        #region 配置属性

        private string PrefabGeneratePath
        {
            get => UIKitCreateConfig.Instance.PrefabGeneratePath;
            set => UIKitCreateConfig.Instance.PrefabGeneratePath = value;
        }
        
        private string ScriptGeneratePath
        {
            get => UIKitCreateConfig.Instance.ScriptGeneratePath;
            set => UIKitCreateConfig.Instance.ScriptGeneratePath = value;
        }
        
        private string ScriptNamespace
        {
            get => UIKitCreateConfig.Instance.ScriptNamespace;
            set => UIKitCreateConfig.Instance.ScriptNamespace = value;
        }
        
        private string AssemblyName
        {
            get => UIKitCreateConfig.Instance.AssemblyName;
            set => UIKitCreateConfig.Instance.AssemblyName = value;
        }

        #endregion

        #region 面板配置

        private string mPanelCreateName = string.Empty;
        private UILevel mSelectedLevel = UILevel.Common;
        private bool mIsModal;
        private bool mGenerateLifecycleHooks = true;
        private bool mGenerateFocusSupport;

        #endregion

        #region 动画配置

        private bool mShowAnimationSettings;
        private AnimationType mShowAnimationType = AnimationType.None;
        private AnimationType mHideAnimationType = AnimationType.None;
        private float mAnimationDuration = 0.3f;

        /// <summary>
        /// 动画类型枚举
        /// </summary>
        private enum AnimationType
        {
            None,
            Fade,
            Scale,
            SlideFromLeft,
            SlideFromRight,
            SlideFromTop,
            SlideFromBottom
        }

        #endregion

        #region GUI 样式

        private readonly Lazy<GUIStyle> mLabelStyle = new(() =>
        {
            var labelStyle = new GUIStyle(GUI.skin.GetStyle("label"))
            {
                richText = true
            };
            return labelStyle;
        });

        private readonly Lazy<GUIStyle> mFoldoutStyle = new(() =>
        {
            var style = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold
            };
            return style;
        });

        #endregion

        #region 路径属性

        private string PrefabName => $"{mPanelCreateName}.prefab";
        private string ScriptName => $"{mPanelCreateName}.cs";
        private string DesignerName => $"{mPanelCreateName}.Designer.cs";

        private string PrefabPath => $"{PrefabGeneratePath}/{PrefabName}";
        private string ScriptPath => $"{ScriptGeneratePath}/{mPanelCreateName}/{ScriptName}";
        private string DesignerPath => $"{ScriptGeneratePath}/{mPanelCreateName}/{DesignerName}";

        #endregion

        public static void Open()
        {
            Rect wr = new(0, 0, 800, 600);
            var window = GetWindowWithRect<UIKitCreatePanelWindow>(wr, true, WINDOW_NAME);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(5);
            
            DrawAssemblySection();
            DrawNamespaceSection();
            DrawPathsSection();
            DrawPanelConfigSection();
            DrawAnimationSection();
            DrawPreviewSection();
            DrawCreateButton();
        }

        #region GUI 绘制方法

        /// <summary>
        /// 绘制程序集配置
        /// </summary>
        private void DrawAssemblySection()
        {
            EditorGUILayout.LabelField("UI脚本所在的程序集：");
            GUILayout.BeginHorizontal("box");
            {
                AssemblyName = EditorGUILayout.TextField(AssemblyName);
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制命名空间配置
        /// </summary>
        private void DrawNamespaceSection()
        {
            EditorGUILayout.LabelField("Scripts命名空间：");
            GUILayout.BeginHorizontal("box");
            {
                ScriptNamespace = EditorGUILayout.TextField(ScriptNamespace);
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制路径配置
        /// </summary>
        private void DrawPathsSection()
        {
            // Scripts 目录
            EditorGUILayout.LabelField("Scripts目录：");
            GUILayout.BeginHorizontal("box");
            {
                EditorGUILayout.LabelField(ScriptGeneratePath);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    var folderPath = EditorUtility.OpenFolderPanel("Scripts目录", ScriptGeneratePath, string.Empty);
                    if (!string.IsNullOrEmpty(folderPath))
                    {
                        var assetsIndex = folderPath.IndexOf(ASSETS_PREFIX, StringComparison.Ordinal);
                        if (assetsIndex >= 0)
                        {
                            ScriptGeneratePath = folderPath[assetsIndex..];
                        }
                    }
                }
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();

            // Prefab 目录
            EditorGUILayout.LabelField("Prefab目录：");
            GUILayout.BeginHorizontal("box");
            {
                EditorGUILayout.LabelField(PrefabGeneratePath);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    var folderPath = EditorUtility.OpenFolderPanel("Prefab目录", PrefabGeneratePath, string.Empty);
                    if (!string.IsNullOrEmpty(folderPath))
                    {
                        var assetsIndex = folderPath.IndexOf(ASSETS_PREFIX, StringComparison.Ordinal);
                        if (assetsIndex >= 0)
                        {
                            PrefabGeneratePath = folderPath[assetsIndex..];
                        }
                    }
                }
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制面板配置
        /// </summary>
        private void DrawPanelConfigSection()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("面板配置", EditorStyles.boldLabel);
            
            GUILayout.BeginVertical("box");
            {
                // 面板名称
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Panel名字：", GUILayout.Width(80));
                mPanelCreateName = EditorGUILayout.TextField(mPanelCreateName);
                EditorGUILayout.EndHorizontal();

                // UI 层级
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("UI层级：", GUILayout.Width(80));
                mSelectedLevel = (UILevel)EditorGUILayout.EnumPopup(mSelectedLevel);
                EditorGUILayout.EndHorizontal();

                // 模态选项
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("模态面板：", GUILayout.Width(80));
                mIsModal = EditorGUILayout.Toggle(mIsModal);
                EditorGUILayout.EndHorizontal();

                // 生命周期钩子
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("生命周期钩子：", GUILayout.Width(80));
                mGenerateLifecycleHooks = EditorGUILayout.Toggle(mGenerateLifecycleHooks);
                EditorGUILayout.EndHorizontal();

                // 焦点支持
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("焦点导航支持：", GUILayout.Width(80));
                mGenerateFocusSupport = EditorGUILayout.Toggle(mGenerateFocusSupport);
                EditorGUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制动画配置
        /// </summary>
        private void DrawAnimationSection()
        {
            EditorGUILayout.Space(10);
            mShowAnimationSettings = EditorGUILayout.Foldout(mShowAnimationSettings, "动画配置", true, mFoldoutStyle.Value);
            
            if (mShowAnimationSettings)
            {
                GUILayout.BeginVertical("box");
                {
                    // 显示动画
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("显示动画：", GUILayout.Width(80));
                    mShowAnimationType = (AnimationType)EditorGUILayout.EnumPopup(mShowAnimationType);
                    EditorGUILayout.EndHorizontal();

                    // 隐藏动画
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("隐藏动画：", GUILayout.Width(80));
                    mHideAnimationType = (AnimationType)EditorGUILayout.EnumPopup(mHideAnimationType);
                    EditorGUILayout.EndHorizontal();

                    // 动画时长
                    if (mShowAnimationType != AnimationType.None || mHideAnimationType != AnimationType.None)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("动画时长：", GUILayout.Width(80));
                        mAnimationDuration = EditorGUILayout.Slider(mAnimationDuration, 0.1f, 2f);
                        EditorGUILayout.EndHorizontal();
                    }
                }
                GUILayout.EndVertical();
            }
        }

        /// <summary>
        /// 绘制文件预览
        /// </summary>
        private void DrawPreviewSection()
        {
            if (string.IsNullOrEmpty(mPanelCreateName)) return;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("生成文件预览", EditorStyles.boldLabel);
            
            GUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField(
                    File.Exists(PrefabPath)
                        ? $"{PrefabName}{ALREADY_EXIST_LABEL}"
                        : $"{PrefabGeneratePath}/{PrefabName}", mLabelStyle.Value);

                EditorGUILayout.LabelField(
                    File.Exists(ScriptPath)
                        ? $"{ScriptName}{ALREADY_EXIST_LABEL}"
                        : $"{ScriptGeneratePath}/{mPanelCreateName}/{ScriptName}", mLabelStyle.Value);

                EditorGUILayout.LabelField(
                    File.Exists(DesignerPath)
                        ? $"{DesignerName}{ALREADY_EXIST_LABEL}"
                        : $"{ScriptGeneratePath}/{mPanelCreateName}/{DesignerName}", mLabelStyle.Value);
            }
            GUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制创建按钮
        /// </summary>
        private void DrawCreateButton()
        {
            EditorGUILayout.Space(10);
            
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(mPanelCreateName) || File.Exists(PrefabPath));
            {
                if (GUILayout.Button("创建 UI Panel", GUILayout.Height(30)))
                {
                    OnCreateUIPanelClick();
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        #endregion

        #region 创建逻辑

        /// <summary>
        /// 创建 UI 预制体
        /// </summary>
        private void OnCreateUIPanelClick()
        {
            var panelName = mPanelCreateName;

            if (string.IsNullOrEmpty(panelName)) return;

            var uiKitPrefab = Resources.Load<GameObject>(nameof(UIKit));
            var uikit = Instantiate(uiKitPrefab);
            var uiRoot = uikit.GetComponentInChildren<UIRoot>();

            if (uiRoot == null)
            {
                KitLogger.Error("UIKit预制体中不包含UIRoot组件!");
                DestroyImmediate(uikit);
                return;
            }

            var gameObj = new GameObject(Path.GetFileNameWithoutExtension(panelName))
            {
                transform =
                {
                    parent = uiRoot.transform,
                    localScale = Vector3.one
                }
            };

            var rect = gameObj.AddComponent<RectTransform>();
            rect.anchoredPosition3D = Vector3.zero;
            rect.localEulerAngles = Vector3.zero;
            rect.localScale = Vector3.one;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(gameObj, PrefabPath, InteractionMode.AutomatedAction);

            // 使用增强的代码生成
            var options = new PanelCodeGenOptions
            {
                Level = mSelectedLevel,
                IsModal = mIsModal,
                GenerateLifecycleHooks = mGenerateLifecycleHooks,
                GenerateFocusSupport = mGenerateFocusSupport,
                ShowAnimationType = GetAnimationTypeName(mShowAnimationType),
                HideAnimationType = GetAnimationTypeName(mHideAnimationType),
                AnimationDuration = mAnimationDuration
            };

            UICodeGenerator.DoCreateCode(prefab, ScriptPath, DesignerPath, ScriptNamespace, options);

            DestroyImmediate(gameObj);
            DestroyImmediate(uikit);

            var window = GetWindow<UIKitCreatePanelWindow>();
            window.Close();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 获取动画类型名称
        /// </summary>
        private string GetAnimationTypeName(AnimationType type) => type switch
        {
            AnimationType.Fade => "Fade",
            AnimationType.Scale => "Scale",
            AnimationType.SlideFromLeft => "SlideFromLeft",
            AnimationType.SlideFromRight => "SlideFromRight",
            AnimationType.SlideFromTop => "SlideFromTop",
            AnimationType.SlideFromBottom => "SlideFromBottom",
            _ => null
        };

        #endregion
    }

    /// <summary>
    /// 面板代码生成选项
    /// </summary>
    public class PanelCodeGenOptions
    {
        /// <summary>
        /// UI 层级
        /// </summary>
        public UILevel Level { get; set; } = UILevel.Common;
        
        /// <summary>
        /// 是否为模态面板
        /// </summary>
        public bool IsModal { get; set; }
        
        /// <summary>
        /// 是否生成生命周期钩子
        /// </summary>
        public bool GenerateLifecycleHooks { get; set; } = true;
        
        /// <summary>
        /// 是否生成焦点支持
        /// </summary>
        public bool GenerateFocusSupport { get; set; }
        
        /// <summary>
        /// 显示动画类型
        /// </summary>
        public string ShowAnimationType { get; set; }
        
        /// <summary>
        /// 隐藏动画类型
        /// </summary>
        public string HideAnimationType { get; set; }
        
        /// <summary>
        /// 动画时长
        /// </summary>
        public float AnimationDuration { get; set; } = 0.3f;
    }
}
