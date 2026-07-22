#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.Unity.Inspector
{
    /// <summary>
    /// InspectorKit 的 UI Toolkit Inspector 基类。
    /// 具体组件只需声明一个 CustomEditor 并继承此类型，即可获得元数据字段和按钮渲染。
    /// </summary>
    public abstract class InspectorKitEditor : Editor
    {
        /// <summary>
        /// 创建带 InspectorKit 样式、字段元数据和操作按钮的 Inspector 视觉树。
        /// </summary>
        /// <returns>绑定当前 SerializedObject 的 Inspector 根元素。</returns>
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = InspectorKitUi.CreateRoot();
            Type targetType = target == null ? null : target.GetType();
            if (targetType == null)
                return root;

            root.Add(InspectorKitUi.CreatePropertyFields(serializedObject, targetType));

            VisualElement actions = InspectorKitUi.CreateActionButtons(targetType, InvokeButton);
            if (actions.childCount > 0)
                root.Add(actions);

            return root;
        }

        /// <summary>
        /// 提供不支持 UI Toolkit 的 Unity 版本回退绘制路径。
        /// 字段回退到 Unity 默认 Inspector，按钮仍保持可用。
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            Type targetType = target == null ? null : target.GetType();
            if (targetType != null)
            {
                MethodInfo[] methods = targetType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int index = 0; index < methods.Length; index++)
                {
                    MethodInfo method = methods[index];
                    InspectorButtonAttribute attribute = method.GetCustomAttribute<InspectorButtonAttribute>();
                    if (attribute == null || method.IsSpecialName || method.GetParameters().Length != 0)
                        continue;

                    string label = string.IsNullOrEmpty(attribute.Label) ? method.Name : attribute.Label;
                    if (GUILayout.Button(label))
                        InvokeButton(method);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 调用一个无参数 Inspector 按钮方法，并记录 Unity Undo 脏状态。
        /// </summary>
        /// <param name="method">待调用的方法。</param>
        private void InvokeButton(MethodInfo method)
        {
            if (method == null || method.IsStatic || method.GetParameters().Length != 0)
                return;

            UnityEngine.Object[] selectedTargets = targets;
            for (int index = 0; index < selectedTargets.Length; index++)
            {
                UnityEngine.Object selectedTarget = selectedTargets[index];
                if (selectedTarget == null)
                    continue;

                Undo.RecordObject(selectedTarget, method.Name);
                try
                {
                    method.Invoke(selectedTarget, null);
                    EditorUtility.SetDirty(selectedTarget);
                }
                catch (TargetInvocationException exception)
                {
                    Debug.LogException(exception.InnerException ?? exception, selectedTarget);
                }
            }

            serializedObject.Update();
        }
    }
}
#endif
