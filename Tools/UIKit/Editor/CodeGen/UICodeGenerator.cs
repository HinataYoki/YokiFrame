using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// UI 代码生成器 - 负责协调代码生成流程
    /// </summary>
    public static class UICodeGenerator
    {
        #region 菜单项

        [MenuItem("Assets/UIKit - Create UICode", true)]
        private static bool CreateUICodeValidate()
        {
            var obj = Selection.GetFiltered(typeof(GameObject), SelectionMode.Assets | SelectionMode.TopLevel).FirstOrDefault() as GameObject;
            if (obj == null) return false;

            var prefabType = PrefabUtility.GetPrefabAssetType(obj);
            if (prefabType == PrefabAssetType.NotAPrefab) return false;

            return obj.GetComponent<UIPanel>() != null;
        }

        [MenuItem("Assets/UIKit - Create UICode")]
        private static void CreateUICode()
        {
            var obj = Selection.GetFiltered(typeof(GameObject), SelectionMode.Assets | SelectionMode.TopLevel).First() as GameObject;

            if (obj == null)
            {
                Debug.LogError("请选择一个预制体");
                return;
            }

            // 检查是否是 Prefab
            var prefabType = PrefabUtility.GetPrefabAssetType(obj);
            if (prefabType == PrefabAssetType.NotAPrefab)
            {
                Debug.LogError($"{obj.name} 不是预制体");
                return;
            }

            // 检查是否挂载了 UIPanel 组件
            if (obj.GetComponent<UIPanel>() == null)
            {
                Debug.LogError($"预制体 {obj.name} 没有挂载 UIPanel 组件，无法生成 UI 代码");
                return;
            }

            DoCreateCode(obj, UIKitCreateConfig.Instance.ScriptNamespace);
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

        #endregion

        #region 公共方法

        /// <summary>
        /// 生成 UI 代码
        /// </summary>
        /// <param name="prefab">UI 预制体</param>
        /// <param name="scriptNamespace">命名空间</param>
        /// <param name="options">代码生成选项</param>
        public static void DoCreateCode(GameObject prefab, string scriptNamespace, PanelCodeGenOptions options = null)
        {
            if (prefab == null)
            {
                Debug.LogError("[UICodeGenerator] 预制体为空");
                return;
            }

            var prefabType = PrefabUtility.GetPrefabAssetType(prefab);
            if (prefabType is PrefabAssetType.NotAPrefab)
            {
                Debug.LogError($"[UICodeGenerator] {prefab.name} 不是预制体", prefab);
                return;
            }

            // 创建上下文
            var context = CreateContext(prefab, scriptNamespace, options);

            // 收集绑定信息
            BindCollector.SearchBinds(prefab.transform, prefab.name, context.BindCodeInfo);

            // 生成代码
            GenerateCode(context);

            // 添加到序列化队列
            UISerializer.AddPrefabReferencesAfterCompile(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[UICodeGenerator] 成功生成 UI 代码: {prefab.name}");
        }

        /// <summary>
        /// 生成 UI 代码（兼容旧 API）
        /// </summary>
        [System.Obsolete("使用 DoCreateCode(GameObject, string, PanelCodeGenOptions) 替代")]
        public static void DoCreateCode(GameObject prefab, string scriptPath, string designerPath, string scriptNamespace, PanelCodeGenOptions options = null)
        {
            DoCreateCode(prefab, scriptNamespace, options);
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 创建代码生成上下文
        /// </summary>
        private static UICodeGenContext CreateContext(GameObject prefab, string scriptNamespace, PanelCodeGenOptions options)
        {
            var bindCodeInfo = new BindCodeInfo
            {
                Type = prefab.name,
                Name = prefab.name,
                Self = prefab,
            };

            return new UICodeGenContext
            {
                PanelName = prefab.name,
                ScriptRootPath = UIKitCreateConfig.Instance.ScriptGeneratePath,
                ScriptNamespace = scriptNamespace,
                BindCodeInfo = bindCodeInfo,
                Options = options
            };
        }

        /// <summary>
        /// 执行代码生成
        /// </summary>
        private static void GenerateCode(UICodeGenContext context)
        {
            var template = UICodeGenTemplateRegistry.ActiveTemplate;

            // 生成 Panel 用户文件
            var panelPath = context.GetPanelFilePath();
            if (!File.Exists(panelPath))
            {
                Directory.CreateDirectory(PathUtils.GetDirectoryPath(panelPath));
                template.WritePanel(context);
                Debug.Log($"[UICodeGenerator] 创建 Panel 文件: {panelPath}");
            }

            // 生成 Panel Designer 文件
            var designerPath = context.GetPanelDesignerPath();
            Directory.CreateDirectory(PathUtils.GetDirectoryPath(designerPath));
            template.WritePanelDesigner(context);
        }

        #endregion
    }
}
