using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace YokiFrame
{
    public interface IBaseTemplate
    {
        void Generate(string generateFilePath, string behaviourName, string nameSpace, PanelCodeInfo panelCodeInfo);
    }
    /// <summary>
    /// 存储一些ScriptKit相关的信息
    /// </summary>
    public class ScriptKitInfo
    {
        public string HotScriptFilePath;
        public string HotScriptSuffix;
        public IBaseTemplate[] Templates;
        public ScriptKitCodeBind CodeBind;
    }

    public delegate void ScriptKitCodeBind(GameObject uiPrefab, string filePath);

    public class UICodeGenerator
    {
        private static readonly UICodeGenerator mInstance = new();

        private static ScriptKitInfo ScriptKitInfo;

        [MenuItem("Asset/UIKit - Create UICode")]
        private static void CreateUICode()
        {
            var obj = Selection.GetFiltered(typeof(GameObject), SelectionMode.Assets | SelectionMode.TopLevel).First() as GameObject;

            var prefabPath = $"{UIKitCreateConfig.GeneratePrePath}{UIKitCreateConfig.Instance.PrefabGeneratePath}/{obj.name}.prefab";
            var scriptPath = $"{UIKitCreateConfig.GeneratePrePath}{UIKitCreateConfig.Instance.ScriptGeneratePath}/{obj.name}/{obj.name}.cs";
            var designerPath = $"{UIKitCreateConfig.GeneratePrePath}{UIKitCreateConfig.Instance.ScriptGeneratePath}/{obj.name}/{obj.name}.Designer.cs";

            DoCreateCode(obj, prefabPath, scriptPath, designerPath, UIKitCreateConfig.Instance.ScriptNamespace);
        }

        [MenuItem("GameObject/UIKit/(Alt+B)Add Bind &b", false, 1)]
        private static void AddBind()
        {
            foreach (var o in Selection.objects.OfType<GameObject>())
            {
                if (o)
                {
                    var uiMark = o.GetComponent<Bind>();

                    if (!uiMark)
                    {
                        o.AddComponent<Bind>();
                    }

                    EditorUtility.SetDirty(o);
                    EditorSceneManager.MarkSceneDirty(o.scene);
                }
            }
        }

        public static void DoCreateCode(GameObject prefab, string prefabPath, string scriptPath, string designerPath, string scriptNamespace)
        {
            ScriptKitInfo = null;
            mInstance.CreateCode(prefab, prefabPath, scriptPath, designerPath, scriptNamespace);
        }

        private void CreateCode(GameObject prefab, string prefabPath, string scriptPath, string designerPath, string scriptNamespace)
        {
            if (prefab != null)
            {
                var objType = PrefabUtility.GetPrefabAssetType(prefab);

                if (objType == PrefabAssetType.NotAPrefab)
                {
                    LogKit.Warning<UICodeGenerator>($"{prefab} 不是预制体", prefab);
                    return;
                }

                var clone = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

                if (clone == null)
                {
                    LogKit.Warning<UICodeGenerator>($"实例化预制体{prefab}失败", prefab);
                    return;
                }

                var panelCodeInfo = new PanelCodeInfo
                {
                    GameObjectName = clone.name.Replace("(clone)", string.Empty)
                };

                BindCollector.SearchBinds(clone.transform, string.Empty, panelCodeInfo);

                CreateUIPanelCode(prefab, prefabPath, scriptPath, designerPath, scriptNamespace, panelCodeInfo);

                UnityEngine.Object.DestroyImmediate(clone);
            }
        }

        private void CreateUIPanelCode(GameObject prefab, string prefabPath, string scriptPath, string designerPath, string scriptNamespace, PanelCodeInfo panelCodeInfo)
        {
            var behaviourName = prefab.name;

            if (!File.Exists(scriptPath))
            {
                if (ScriptKitInfo != null)
                {
                    if (ScriptKitInfo.Templates != null && ScriptKitInfo.Templates[0] != null)
                    {
                        ScriptKitInfo.Templates[0].Generate(scriptPath, behaviourName, scriptNamespace, null);
                    }
                } 
                else
                {
                    Directory.CreateDirectory(PathUtils.GetDirectoryPath(scriptPath));
                    UIPanelTemplate.Write(behaviourName, scriptPath, scriptNamespace);
                }
            }

            CreateUIPanelDesignerCode(behaviourName, designerPath, scriptNamespace, panelCodeInfo);

            LogKit.Log<UICodeGenerator>($">>>>>>>Success Create UIPrefab Code: {behaviourName}");
        }

        private void CreateUIPanelDesignerCode(string behaviourName, string designerPath, string scriptNamespace, PanelCodeInfo panelCodeInfo)
        {
            if (ScriptKitInfo != null)
            {
                if (ScriptKitInfo.Templates != null && ScriptKitInfo.Templates[1] != null)
                {
                    ScriptKitInfo.Templates[0].Generate(designerPath, behaviourName, scriptNamespace, null);
                }
            }
            else
            {
                Directory.CreateDirectory(PathUtils.GetDirectoryPath(designerPath));
                UIPanelTemplate.WriteDesigner(behaviourName, designerPath, scriptNamespace, panelCodeInfo);
            }

            var dir = designerPath.Replace($"{behaviourName}.Designer.cs", string.Empty);

            foreach (var elementCodeData in panelCodeInfo.ElementCodeDatas)
            {
                string elementDirPath;
                if (elementCodeData.BindInfo.BindScript.GetBindType() is BindType.Element)
                {
                    var dirFullPath = dir + behaviourName + "/";
                    if (!Directory.Exists(dirFullPath))
                    {
                        Directory.CreateDirectory(dirFullPath);
                    }
                    elementDirPath = dirFullPath;
                }
                else
                {
                    var dirFullPath = dir + "/Components/";
                    if (!Directory.Exists(dirFullPath))
                    {
                        Directory.CreateDirectory(dirFullPath);
                    }
                    elementDirPath = dirFullPath;
                }

                CreateUIElementCode(elementDirPath, elementCodeData);
            }
        }

        private void CreateUIElementCode(string elementDirPath, ElementCodeInfo elementCodeData)
        {
            var panelFilePathWhithoutExt = elementDirPath + elementCodeData.BehaviourName;

            if (!File.Exists(panelFilePathWhithoutExt + ".cs"))
            {
                UIPanelTemplate.WriteElement(panelFilePathWhithoutExt + ".cs",
                    elementCodeData.BehaviourName, UIKitCreateConfig.Instance.ScriptNamespace, elementCodeData);
            }

            UIPanelTemplate.WriteElementComponent(panelFilePathWhithoutExt + ".Designer.cs",
                elementCodeData.BehaviourName, UIKitCreateConfig.Instance.ScriptNamespace, elementCodeData);

            foreach (var childElementCodeData in elementCodeData.ElementCodeDatas)
            {
                var elementDir = (panelFilePathWhithoutExt + "/").CreateDirIfNotExists();
                CreateUIElementCode(elementDir, childElementCodeData);
            }
        }
    }
}
