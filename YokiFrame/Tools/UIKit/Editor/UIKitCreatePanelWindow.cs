using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    public class UIKitCreatePanelWindow : EditorWindow
    {
        private readonly static string WindowName = "YokiFrame_UI创建窗口";

        [MenuItem("YokiFrame/UIKit/CreatePanel #_U")]
        private static void Open()
        {
            Rect wr = new(0, 0, 800, 500);
            var window = GetWindowWithRect<UIKitCreatePanelWindow>(wr, true, WindowName);
            window.Show();
        }

        #region 路径参数

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

        private string PanelCreateName = string.Empty;

        private bool IsCloneInScene
        {
            get => EditorPrefs.GetBool($"{nameof(UIKitCreateConfig)}{IsCloneInScene}", false);
            set => EditorPrefs.SetBool($"{nameof(UIKitCreateConfig)}{IsCloneInScene}", value);
        }

        private readonly Lazy<GUIStyle> LabelStyle = new(() =>
        {
            var labelStyle = new GUIStyle(GUI.skin.GetStyle("label"))
            {
                richText = true
            };
            return labelStyle;
        });
        private readonly static string Asstes = "Asstes";
        private readonly static string AlreadyExist = "<color=red>[已存在]</color>";

        private string PrefabName => $"{PanelCreateName}.prefab";
        private string ScriptName => $"{PanelCreateName}.cs";
        private string DesignerName => $"{PanelCreateName}.Designer.cs";

        private string PrefabPath => $"{PrefabGeneratePath}/{PrefabName}";
        private string ScriptPath => $"{ScriptGeneratePath}/{PanelCreateName}/{ScriptName}";
        private string DesignerPath => $"{ScriptGeneratePath}/{PanelCreateName}/{DesignerName}";
        #endregion

        private void OnGUI()
        {
            #region UI脚本所在的程序集
            EditorGUILayout.LabelField("UI脚本所在的程序集：");
            GUILayout.BeginHorizontal("box");
            {
                AssemblyName = EditorGUILayout.TextField(AssemblyName);
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();
            #endregion

            #region Scripts命名空间
            EditorGUILayout.LabelField("Scripts命名空间：");
            GUILayout.BeginHorizontal("box");
            {
                ScriptNamespace = EditorGUILayout.TextField(ScriptNamespace);
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();
            #endregion

            #region Scripts目录
            EditorGUILayout.LabelField("Scripts目录：");
            GUILayout.BeginHorizontal("box");
            {
                EditorGUILayout.LabelField(ScriptGeneratePath);
                if (GUILayout.Button("..."))
                {
                    var folderPath = EditorUtility.OpenFolderPanel("Scripts目录", ScriptGeneratePath, string.Empty);

                    if (!string.IsNullOrEmpty(folderPath))
                    {
                        ScriptGeneratePath = folderPath[folderPath.IndexOf(Asstes)..];
                    }
                }
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();
            #endregion

            #region Prefab目录
            EditorGUILayout.LabelField("Prefab目录：");
            GUILayout.BeginHorizontal("box");
            {
                EditorGUILayout.LabelField(PrefabGeneratePath);
                if (GUILayout.Button("..."))
                {
                    var folderPath = EditorUtility.OpenFolderPanel("Prefab目录", PrefabGeneratePath, string.Empty);
                    if (!string.IsNullOrEmpty(folderPath))
                    {
                        PrefabGeneratePath = folderPath[folderPath.IndexOf(Asstes)..];
                    }
                }
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();
            #endregion

            #region Panel名字

            /*EditorGUILayout.LabelField("面板选项：");
            GUILayout.BeginHorizontal("box");
            {
                IsCloneInScene = EditorGUILayout.Toggle("在场景中生成预制件", IsCloneInScene);
            }
            GUILayout.EndHorizontal();*/

            EditorGUILayout.LabelField("Panel名字");
            GUILayout.BeginHorizontal("box");
            {
                PanelCreateName = EditorGUILayout.TextField(PanelCreateName);
            }
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(PanelCreateName))
            {
                EditorGUILayout.LabelField("生成文件预览");
                GUILayout.BeginVertical("box");
                {
                    EditorGUILayout.LabelField(
                    File.Exists(PrefabPath)
                        ? $"{PrefabName}{AlreadyExist}"
                        : $"{PrefabGeneratePath}/{PrefabName}", LabelStyle.Value);


                    EditorGUILayout.LabelField(
                        File.Exists(ScriptPath)
                            ? $"{ScriptName}{AlreadyExist}"
                            : $"{ScriptGeneratePath}/{PanelCreateName}/{ScriptName}", LabelStyle.Value);

                    EditorGUILayout.LabelField(
                        File.Exists(DesignerPath)
                            ? $"{DesignerName}{AlreadyExist}"
                            : $"{ScriptGeneratePath}/{PanelCreateName}/{DesignerName}", LabelStyle.Value);
                }
                GUILayout.EndVertical();
            }

            if (!string.IsNullOrEmpty(PanelCreateName) && !File.Exists(PrefabPath) && GUILayout.Button("创建 UI Panel"))
            {
                OnCreateUIPanelClick();
                GUIUtility.ExitGUI();
            }
            #endregion
        }

        /// <summary>
        /// 创建UI预制体
        /// </summary>
        private void OnCreateUIPanelClick()
        {
            var panelName = PanelCreateName;

            if (!string.IsNullOrEmpty(panelName))
            {
                var uiKitPrefab = Resources.Load<GameObject>(nameof(UIKit));
                var uikit = Instantiate(uiKitPrefab);
                var uiRoot = uikit.GetComponentInChildren<UIRoot>();

                if (uiRoot == null)
                {
                    LogKitLogger.LogError<UIKitCreatePanelWindow>("UIKit预制体中不包含UIRoot组件!");
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

                UICodeGenerator.DoCreateCode(prefab, ScriptPath, DesignerPath, ScriptNamespace);

                DestroyImmediate(gameObj);
                DestroyImmediate(uikit);
            }

            var window = GetWindow<UIKitCreatePanelWindow>();
            window.Close();
            AssetDatabase.Refresh();
        }
    }
}
