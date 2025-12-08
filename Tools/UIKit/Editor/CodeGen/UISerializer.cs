using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace YokiFrame
{
    public class UISerializer
    {
        /// <summary>
        /// 把Bind关系序列化到Prefab中
        /// </summary>
        /// <param name="uiPrefab">需要生成bind关系的预制体</param>
        public static void AddPrefabReferencesAfterCompoile(GameObject uiPrefab)
        {
            var path = AssetDatabase.GetAssetPath(uiPrefab);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("Prefab path is null or empty.");
                return;
            }

            UIKitCreateConfig.Instance.BindPrefabPathList.Add(path);
        }

        [DidReloadScripts]
        private static void DoAddComponent2Prefab()
        {
            if (UIKitCreateConfig.Instance.BindPrefabPathList.Count > 0)
            {
                var assembly = GetAssembly();
                if (assembly == null) return;

                // 缓存预制路径以避免在循环中的资产数据库操作
                var prefabPaths = new List<string>(UIKitCreateConfig.Instance.BindPrefabPathList);
                UIKitCreateConfig.Instance.BindPrefabPathList.Clear();

                for (int i = 0; i < prefabPaths.Count; i++)
                {
                    string prefabPath = prefabPaths[i];
                    EditorUtility.DisplayProgressBar("UIKit", $"Serialize UIPrefab...{prefabPath}", i);

                    var uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (uiPrefab == null)
                    {
                        Debug.LogError($"Prefab at path {prefabPath} not found.");
                        continue;
                    }
                    Debug.Log(">>>>>>>SerializeUIPrefab: " + uiPrefab);

                    //GameObject tempInstance = Object.Instantiate(uiPrefab);
                    SetObjectRef2Property(uiPrefab, uiPrefab.name, assembly);
                    //PrefabUtility.SaveAsPrefabAsset(tempInstance, prefabPath);
                    //Object.DestroyImmediate(tempInstance);

                    /*SetObjectRef2Property(uiPrefab, uiPrefab.name, assembly);
                    EditorUtility.SetDirty(uiPrefab);
                    PrefabUtility.SavePrefabAsset(uiPrefab);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(uiPrefab);*/
                    Debug.Log(">>>>>>>Success Serialize UIPrefab: " + uiPrefab.name);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.ClearProgressBar();
            }
        }

        private static void SetObjectRef2Property(GameObject prefab, string name, System.Reflection.Assembly assembly)
        {
            var bindCodeInfo = new BindCodeInfo()
            {
                Type = name,
                Name = name,
                Self = prefab,
            };

            BindCollector.SearchBinds(prefab.transform, name, bindCodeInfo);

            var typeName = UIKitCreateConfig.Instance.ScriptNamespace + "." + name;
            var type = assembly.GetType(typeName);
            var typeIns = prefab.GetComponent(type);
            if (typeIns == null)
            {
                typeIns = prefab.AddComponent(type);
            }

            var serialized = new SerializedObject(typeIns);
            serialized.Update();
            SetObjectRef2Property(name, assembly, serialized, bindCodeInfo);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectRef2Property(string name, System.Reflection.Assembly assembly, SerializedObject serialized, BindCodeInfo bindCodeInfo)
        {
            foreach (var bindInfo in bindCodeInfo.MemberDic.Values)
            {
                // 把对应的Bind添加到prefab引用中
                var objectReference = serialized.FindProperty($"{bindInfo.Name}");
                if (objectReference == null)
                {
                    Debug.LogError($"未在类：{bindInfo.Type}中查询到对应序列化字段名{bindInfo.Name}", bindInfo.Self);
                }
                else
                {
                    // 如果不是成员或者叶子节点，添加对应的组件
                    if (bindInfo.Bind is not BindType.Member or BindType.Leaf)
                    {
                        var typeName = bindInfo.Bind is BindType.Component ? bindInfo.Type : $"{name}{nameof(UIElement)}.{bindInfo.Type}";
                        var type = assembly.GetType($"{UIKitCreateConfig.Instance.ScriptNamespace}.{typeName}");
                        var typeIns = bindInfo.Self.GetComponent(type);
                        if (typeIns == null)
                        {
                            typeIns = bindInfo.Self.AddComponent(type);
                        }
                        if (!bindInfo.RepeatElement)
                        {
                            objectReference.objectReferenceValue = typeIns.gameObject;
                        }
                        var newSerialized = new SerializedObject(typeIns);
                        newSerialized.Update();
                        SetObjectRef2Property(name, assembly, newSerialized, bindInfo);

                        /*if (bindInfo.Self.TryGetComponent<Bind>(out var markBind))
                        {
                            Object.DestroyImmediate(markBind, true);
                        }*/
                        newSerialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                    else
                    {
                        objectReference.objectReferenceValue = bindInfo.Self;
                    }
                }
            }
        }

        private static System.Reflection.Assembly GetAssembly()
        {
            var assembly = System.Reflection.Assembly.Load(UIKitCreateConfig.Instance.AssemblyName);
            if (assembly == null)
            {
                Debug.LogError($"Assembly: {UIKitCreateConfig.Instance.AssemblyName} not found.");
                return null;
            }
            return assembly;
        }
    }
}
