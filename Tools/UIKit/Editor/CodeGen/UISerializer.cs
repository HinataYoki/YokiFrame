using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// UI 序列化器 - 负责将 Bind 关系序列化到 Prefab 中
    /// </summary>
    public static class UISerializer
    {
        #region 序列化上下文

        /// <summary>
        /// 序列化上下文 - 避免重复传递参数
        /// </summary>
        private class SerializeContext
        {
            public System.Reflection.Assembly Assembly { get; set; }
            public string Namespace { get; set; }
            public string PanelName { get; set; }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 添加预制体到编译后序列化队列
        /// </summary>
        /// <param name="uiPrefab">需要生成 bind 关系的预制体</param>
        public static void AddPrefabReferencesAfterCompile(GameObject uiPrefab)
        {
            var path = AssetDatabase.GetAssetPath(uiPrefab);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[UISerializer] Prefab 路径为空");
                return;
            }

            UIKitCreateConfig.Instance.BindPrefabPathList.Add(path);
        }

        #endregion

        #region 编辑器回调

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            var pathList = UIKitCreateConfig.Instance.BindPrefabPathList;
            if (pathList.Count == 0) return;

            // 复制列表，保留原始数据以便失败时重试
            var prefabPaths = new List<string>(pathList);

            var assembly = LoadAssembly();
            if (assembly == null) return;

            // 成功加载程序集后才清空列表
            pathList.Clear();

            try
            {
                ProcessPrefabs(prefabPaths, assembly);
            }
            catch (System.Exception e)
            {
                // 失败时恢复列表，允许下次重试
                pathList.AddRange(prefabPaths);
                Debug.LogError($"[UISerializer] 序列化失败: {e.Message}\n{e.StackTrace}");
            }
        }

        #endregion

        #region 私有方法 - 批量处理

        /// <summary>
        /// 批量处理预制体
        /// </summary>
        private static void ProcessPrefabs(List<string> prefabPaths, System.Reflection.Assembly assembly)
        {
            // 处理 Prefab Stage
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            string currentStagePath = prefabStage?.assetPath;

            if (prefabStage != null)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabStage.prefabContentsRoot, currentStagePath);
                StageUtility.GoToMainStage();
            }

            try
            {
                for (int i = 0; i < prefabPaths.Count; i++)
                {
                    string prefabPath = prefabPaths[i];
                    float progress = (float)i / prefabPaths.Count;
                    EditorUtility.DisplayProgressBar("UIKit", $"序列化 UIPrefab: {prefabPath}", progress);

                    ProcessSinglePrefab(prefabPath, assembly);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();

                // 恢复 Prefab Stage
                if (!string.IsNullOrEmpty(currentStagePath))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(currentStagePath);
                    if (prefab != null)
                    {
                        AssetDatabase.OpenAsset(prefab);
                    }
                }
            }
        }

        /// <summary>
        /// 处理单个预制体
        /// </summary>
        private static void ProcessSinglePrefab(string prefabPath, System.Reflection.Assembly assembly)
        {
            var uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (uiPrefab == null)
            {
                Debug.LogError($"[UISerializer] 预制体未找到: {prefabPath}");
                return;
            }

            var context = new SerializeContext
            {
                Assembly = assembly,
                Namespace = UIKitCreateConfig.Instance.ScriptNamespace,
                PanelName = uiPrefab.name
            };

            SerializePrefab(uiPrefab, context);
            Debug.Log($"[UISerializer] 成功序列化: {uiPrefab.name}");
        }

        #endregion

        #region 私有方法 - 序列化

        /// <summary>
        /// 序列化预制体
        /// </summary>
        private static void SerializePrefab(GameObject prefab, SerializeContext context)
        {
            // 收集绑定信息
            var bindCodeInfo = new BindCodeInfo
            {
                Type = prefab.name,
                Name = prefab.name,
                Self = prefab,
            };
            BindCollector.SearchBinds(prefab.transform, prefab.name, bindCodeInfo);

            // 获取或添加 Panel 组件
            var typeName = $"{context.Namespace}.{prefab.name}";
            var type = context.Assembly.GetType(typeName);
            if (type == null)
            {
                Debug.LogError($"[UISerializer] 未找到类型: {typeName}", prefab);
                return;
            }

            var component = prefab.GetComponent(type);
            if (component == null)
            {
                component = prefab.AddComponent(type);
            }

            // 序列化绑定关系
            var serialized = new SerializedObject(component);
            serialized.Update();
            SerializeBindings(serialized, bindCodeInfo, context);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 序列化绑定关系
        /// </summary>
        private static void SerializeBindings(SerializedObject serialized, BindCodeInfo bindCodeInfo, SerializeContext context)
        {
            foreach (var bindInfo in bindCodeInfo.MemberDic.Values)
            {
                var property = serialized.FindProperty(bindInfo.Name);
                if (property == null)
                {
                    Debug.LogError($"[UISerializer] 未找到序列化字段: {bindInfo.Name} (类型: {bindInfo.Type})", bindInfo.Self);
                    continue;
                }

                // Element 和 Component 类型需要添加对应的组件
                if (bindInfo.Bind is BindType.Element or BindType.Component)
                {
                    SerializeComplexBinding(property, bindInfo, context);
                }
                else if (bindInfo.Bind is BindType.Member)
                {
                    // Member 类型直接绑定 GameObject 上的组件
                    property.objectReferenceValue = bindInfo.Self;
                }
            }
        }

        /// <summary>
        /// 序列化复杂绑定（Element/Component）
        /// </summary>
        private static void SerializeComplexBinding(SerializedProperty property, BindCodeInfo bindInfo, SerializeContext context)
        {
            // 构建类型名
            string typeName;
            if (bindInfo.Bind is BindType.Component)
            {
                typeName = $"{context.Namespace}.{bindInfo.Type}";
            }
            else
            {
                typeName = $"{context.Namespace}.{context.PanelName}{nameof(UIElement)}.{bindInfo.Type}";
            }

            var type = context.Assembly.GetType(typeName);
            if (type == null)
            {
                Debug.LogError($"[UISerializer] 未找到类型: {typeName}", bindInfo.Self);
                return;
            }

            // 获取或添加组件
            var component = bindInfo.Self.GetComponent(type);
            if (component == null)
            {
                component = bindInfo.Self.AddComponent(type);
            }

            // 设置引用（非重复元素）
            if (!bindInfo.RepeatElement)
            {
                property.objectReferenceValue = component;
            }

            // 递归序列化子绑定
            var childSerialized = new SerializedObject(component);
            childSerialized.Update();
            SerializeBindings(childSerialized, bindInfo, context);
            childSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        #endregion

        #region 私有方法 - 工具

        /// <summary>
        /// 加载程序集
        /// </summary>
        private static System.Reflection.Assembly LoadAssembly()
        {
            var assemblyName = UIKitCreateConfig.Instance.AssemblyName;
            try
            {
                var assembly = System.Reflection.Assembly.Load(assemblyName);
                if (assembly == null)
                {
                    Debug.LogError($"[UISerializer] 程序集未找到: {assemblyName}");
                }
                return assembly;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[UISerializer] 加载程序集失败: {assemblyName}\n{e.Message}");
                return null;
            }
        }

        #endregion
    }
}
