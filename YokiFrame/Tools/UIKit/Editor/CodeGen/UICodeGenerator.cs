using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace YokiFrame
{
    public class UICodeGenerator
    {
        private static readonly UICodeGenerator mInstance = new();

        [MenuItem("Assets/UIKit - Create UICode")]
        private static void CreateUICode()
        {
            var obj = Selection.GetFiltered(typeof(GameObject), SelectionMode.Assets | SelectionMode.TopLevel).First() as GameObject;

            var scriptPath = $"{UIKitCreateConfig.Instance.ScriptGeneratePath}/{obj.name}/{obj.name}.cs";
            var designerPath = $"{UIKitCreateConfig.Instance.ScriptGeneratePath}/{obj.name}/{obj.name}.Designer.cs";

            DoCreateCode(obj, scriptPath, designerPath, UIKitCreateConfig.Instance.ScriptNamespace);
        }

        [MenuItem("GameObject/UIKit/(Alt+B)Add Bind &b", false, 1)]
        private static void AddBind()
        {
            foreach (var obj in Selection.objects.OfType<GameObject>())
            {
                if (obj)
                {
                    var bind = obj.GetComponent<Bind>();

                    if (!bind)
                    {
                        obj.AddComponent<Bind>();
                    }

                    EditorUtility.SetDirty(obj);
                    EditorSceneManager.MarkSceneDirty(obj.scene);
                }
            }
        }

        public static void DoCreateCode(GameObject prefab, string scriptPath, string designerPath, string scriptNamespace)
        {
            mInstance.CreateCodePipeline(prefab, scriptPath, designerPath, scriptNamespace);
        }

        /// <summary>
        /// 代码生成管线
        /// </summary>
        /// <param name="prefab">需要生成代码的UI预制体</param>
        /// <param name="scriptPath">代码路径</param>
        /// <param name="designerPath">定义代码路径</param>
        /// <param name="scriptNamespace">命名空间</param>
        private void CreateCodePipeline(GameObject prefab, string scriptPath, string designerPath, string scriptNamespace)
        {
            if (prefab != null)
            {
                var prefabType = PrefabUtility.GetPrefabAssetType(prefab);

                if (prefabType is PrefabAssetType.NotAPrefab)
                {
                    Debug.LogError($"{prefab} 是预制体", prefab);
                    return;
                }

                var bindCodeInfo = new BindCodeInfo
                {
                    TypeName = prefab.name,
                    Name = prefab.name,
                    Self = prefab,
                };

                BindCollector.SearchBinds(prefab.transform, prefab.name, bindCodeInfo);

                CreateUIPanelCode(prefab, scriptPath, designerPath, scriptNamespace, bindCodeInfo);

                UISerializer.AddPrefabReferencesAfterCompoile(prefab);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }
        /// <summary>
        /// 创建UI面板代码
        /// </summary>
        /// <param name="prefab">预制体</param>
        /// <param name="scriptFilePath">面板代码路径</param>
        /// <param name="designerPath">成员定义代码路径</param>
        /// <param name="scriptNamespace">代码命名空间</param>
        /// <param name="bindCodeInfo">成员绑定信息</param>
        private void CreateUIPanelCode(GameObject prefab, string scriptFilePath, string designerPath, string scriptNamespace, BindCodeInfo bindCodeInfo)
        {
            var name = prefab.name;

            if (!File.Exists(scriptFilePath))
            {
                Directory.CreateDirectory(PathUtils.GetDirectoryPath(scriptFilePath));
                UICodeGenTemplate.WritePanel(name, scriptFilePath, scriptNamespace);
            }
            LogKitLogger.Log<UICodeGenerator>($">>>>>>>Success Create UIPrefab Code: {name}");

            CreateUIPanelDesignerCode(name, designerPath, scriptNamespace, bindCodeInfo);
        }
        /// <summary>
        /// 创建定义代码
        /// </summary>
        /// <param name="name">代码名称</param>
        /// <param name="designerPath">定义代码路径</param>
        /// <param name="scriptNamespace">代码命名空间</param>
        /// <param name="bindCodeInfo">成员绑定信息</param>
        private void CreateUIPanelDesignerCode(string name, string designerPath, string scriptNamespace, BindCodeInfo bindCodeInfo)
        {
            Directory.CreateDirectory(PathUtils.GetDirectoryPath(designerPath));
            UICodeGenTemplate.WritePanelDesigner(name, designerPath, scriptNamespace, bindCodeInfo);
        }
    }
}
