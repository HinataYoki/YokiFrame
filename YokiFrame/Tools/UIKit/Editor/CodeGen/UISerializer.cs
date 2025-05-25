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
                    Debug.Log(">>>>>>>SerializeUIPrefab: " + uiPrefab);
                    if (uiPrefab == null)
                    {
                        Debug.LogError($"Prefab at path {prefabPath} not found.");
                        continue;
                    }
                    SetObjectRef2Property(uiPrefab, uiPrefab.name, assembly);
                    Debug.Log(">>>>>>>Success Serialize UIPrefab: " + uiPrefab.name);
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.ClearProgressBar();
            }
        }

        private static void SetObjectRef2Property(GameObject prefab, string name, System.Reflection.Assembly assembly)
        {
            var typeName = UIKitCreateConfig.Instance.ScriptNamespace + "." + prefab.name;
            var type = assembly.GetType(typeName);
            var typeIns = prefab.GetComponent(type);
            if (typeIns == null)
            {
                typeIns = prefab.AddComponent(type);
            }

            var serialized = new SerializedObject(typeIns);
            SetObjectRef2Property(prefab.transform, name, assembly, serialized);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SavePrefabAsset(prefab);
        }

        private static void SetObjectRef2Property(Transform transform, string name, System.Reflection.Assembly assembly, SerializedObject serialized)
        {
            foreach (Transform curTrans in transform)
            {
                if (curTrans.TryGetComponent<IBind>(out var bind))
                {
                    //把对应的Bind添加到prefab引用中
                    var objectReference = serialized.FindProperty($"{bind.Name}");
                    if (objectReference == null)
                    {
                        Debug.LogError($"未在类：{name}中查询到对应序列化字段名m{bind.Name}");
                    }
                    else
                    {
                        //如果不是成员或者叶子节点，则删除Mark标记替换成对应的组件
                        if (bind.Bind is not BindType.Member or BindType.Leaf)
                        {
                            var typeName = bind.Bind is BindType.Component ? bind.TypeName : $"{name}{nameof(UIElement)}.{bind.TypeName}";
                            var type = assembly.GetType($"{UIKitCreateConfig.Instance.ScriptNamespace}.{typeName}");
                            var typeIns = curTrans.GetComponent(type);
                            if (typeIns == null)
                            {
                                typeIns = curTrans.gameObject.AddComponent(type);
                            }
                            objectReference.objectReferenceValue = typeIns.gameObject;
                            var newSerialized = new SerializedObject(typeIns);
                            SetObjectRef2Property(curTrans, name, assembly, newSerialized);

                            if (curTrans.TryGetComponent<AbstractBind>(out var markBind))
                            {
                                Object.DestroyImmediate(markBind, true);
                            }
                            newSerialized.ApplyModifiedPropertiesWithoutUndo();
                        }
                        else
                        {
                            objectReference.objectReferenceValue = bind.Transform.gameObject;
                            SetObjectRef2Property(curTrans, name, assembly, serialized);
                        }
                    }
                }
                else
                {
                    SetObjectRef2Property(curTrans, name, assembly, serialized);
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
