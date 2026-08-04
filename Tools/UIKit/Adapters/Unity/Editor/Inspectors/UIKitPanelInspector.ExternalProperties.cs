#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    internal sealed partial class UIKitPanelInspector
    {
        private readonly List<string> mCustomPropertyPaths = new(8);
        private readonly List<MethodInfo> mCustomButtonMethods = new(4);

        /// <summary>
        /// 创建其它属性卡片使用的 IMGUI 桥接容器。
        /// UI Toolkit 的 PropertyField 不会进入 TriInspector、Odin 或其它 IMGUI
        /// PropertyHandler，因此这里仅把派生字段切换到 Unity 原生 IMGUI 管线。
        /// </summary>
        /// <returns>包含第三方绘制入口的视觉元素；没有可绘制字段时返回空。</returns>
        private VisualElement CreateExternalPropertyFields()
        {
            if (!TryGetSerializedObject(out SerializedObject currentSerializedObject))
                return default;

            Type targetType = target == default ? default : target.GetType();
            CollectCustomPropertyPaths(currentSerializedObject, targetType, mCustomPropertyPaths);
            CollectCustomButtonMethods(targetType, mCustomButtonMethods);
            if (mCustomPropertyPaths.Count == 0 && mCustomButtonMethods.Count == 0)
                return default;

            IMGUIContainer container = new(DrawCustomPropertiesIMGUI);
            container.AddToClassList("yoki-editor-inspector__external-properties");
            container.AddToClassList("uipanel-custom-imgui");
            return container;
        }

        /// <summary>
        /// 逐帧读取派生字段并调用 Unity IMGUI PropertyField。
        /// 该调用会保留 Unity 自定义 PropertyDrawer 以及第三方工具通过
        /// PropertyHandler 注册的绘制、Undo 和序列化行为。
        /// </summary>
        private void DrawCustomPropertiesIMGUI()
        {
            if (!TryGetSerializedObject(out SerializedObject currentSerializedObject))
                return;

            currentSerializedObject.UpdateIfRequiredOrScript();
            try
            {
                for (var index = 0; index < mCustomPropertyPaths.Count; index++)
                {
                    SerializedProperty property = currentSerializedObject.FindProperty(mCustomPropertyPaths[index]);
                    if (property == null)
                        continue;

                    FieldInfo field = target == default
                        ? default
                        : FindField(target.GetType(), GetRootPropertyName(property.propertyPath));
                    DrawCustomPropertyDecorators(field);
                    if (ShouldForceExpandCustomProperty(field))
                        property.isExpanded = true;

                    GUIContent label = CreateCustomPropertyLabel(property, field);
                    EditorGUI.BeginDisabledGroup(HasReadOnlyAttribute(field));
                    try
                    {
                        if (label == null)
                            EditorGUILayout.PropertyField(property, true);
                        else
                            EditorGUILayout.PropertyField(property, label, true);
                    }
                    finally
                    {
                        EditorGUI.EndDisabledGroup();
                    }
                }

                DrawCustomPropertyButtons();
            }
            finally
            {
                currentSerializedObject.ApplyModifiedProperties();
            }
        }

        /// <summary>绘制第三方 ButtonAttribute 标记的无参数方法，并保持按钮位于字段之后。</summary>
        private void DrawCustomPropertyButtons()
        {
            for (var index = 0; index < mCustomButtonMethods.Count; index++)
            {
                MethodInfo method = mCustomButtonMethods[index];
                string label = ResolveButtonLabel(method);
                if (!GUILayout.Button(label))
                    continue;

                InvokeCustomButton(method);
            }
        }

        /// <summary>
        /// 在当前 Inspector 选中的全部面板上执行按钮方法。
        /// 先提交字段修改，再逐对象记录 Undo，避免按钮读取到旧的序列化值。
        /// </summary>
        /// <param name="method">待调用的无参数实例方法。</param>
        private void InvokeCustomButton(MethodInfo method)
        {
            if (method == null || !TryGetSerializedObject(out SerializedObject currentSerializedObject))
                return;

            currentSerializedObject.ApplyModifiedProperties();
            string undoLabel = ResolveButtonLabel(method);
            UnityEngine.Object[] editorTargets = targets;
            for (var index = 0; index < editorTargets.Length; index++)
            {
                UnityEngine.Object editorTarget = editorTargets[index];
                if (editorTarget == default)
                    continue;

                try
                {
                    Undo.RecordObject(editorTarget, undoLabel);
                    method.Invoke(editorTarget, null);
                    EditorUtility.SetDirty(editorTarget);
                }
                catch (TargetInvocationException exception)
                {
                    Debug.LogException(exception.InnerException ?? exception, editorTarget);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, editorTarget);
                }
            }

            currentSerializedObject.UpdateIfRequiredOrScript();
        }

        /// <summary>收集当前面板中需要交给 IMGUI 绘制的派生字段路径。</summary>
        /// <param name="serializedObject">当前面板的序列化对象。</param>
        /// <param name="targetType">当前面板类型。</param>
        /// <param name="propertyPaths">输出字段路径列表。</param>
        private static void CollectCustomPropertyPaths(
            SerializedObject serializedObject,
            Type targetType,
            List<string> propertyPaths)
        {
            if (propertyPaths == null)
                return;

            propertyPaths.Clear();
            if (serializedObject == null || targetType == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty iterator = serializedObject.GetIterator();
            if (!iterator.NextVisible(true))
                return;

            do
            {
                if (ShouldShowCustomProperty(iterator.propertyPath, targetType))
                    propertyPaths.Add(iterator.propertyPath);
            }
            while (iterator.NextVisible(false));
        }

        /// <summary>收集派生面板上可由 IMGUI 执行的无参数按钮方法。</summary>
        /// <param name="targetType">当前面板类型。</param>
        /// <param name="buttonMethods">输出按钮方法列表。</param>
        private static void CollectCustomButtonMethods(Type targetType, List<MethodInfo> buttonMethods)
        {
            if (buttonMethods == null)
                return;

            buttonMethods.Clear();
            if (targetType == null)
                return;

            MethodInfo[] methods = targetType.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (var index = 0; index < methods.Length; index++)
            {
                MethodInfo method = methods[index];
                if (ShouldShowCustomButton(method, targetType))
                    buttonMethods.Add(method);
            }
        }

        /// <summary>判断方法是否是当前面板应显示的第三方按钮。</summary>
        /// <param name="method">待判断方法。</param>
        /// <param name="targetType">当前面板类型。</param>
        /// <returns>带按钮属性且可安全调用时返回 true。</returns>
        private static bool ShouldShowCustomButton(MethodInfo method, Type targetType)
        {
            if (method == null
                || targetType == null
                || method.IsStatic
                || method.IsSpecialName
                || method.ContainsGenericParameters
                || method.GetParameters().Length != 0)
                return false;

            Type declaringType = method.DeclaringType;
            if (declaringType == null
                || !declaringType.IsAssignableFrom(targetType)
                || IsFrameworkPanelType(declaringType))
                return false;

            object[] attributes = method.GetCustomAttributes(true);
            for (var index = 0; index < attributes.Length; index++)
            {
                object attribute = attributes[index];
                if (attribute != null && LooksLikeButtonAttribute(attribute.GetType()))
                    return true;
            }

            return false;
        }

        /// <summary>判断一个序列化路径是否是可展示的派生字段。</summary>
        /// <param name="propertyPath">Unity 序列化属性路径。</param>
        /// <param name="targetType">当前面板类型。</param>
        /// <returns>应展示时返回 true。</returns>
        private static bool ShouldShowCustomProperty(string propertyPath, Type targetType)
        {
            if (targetType == null || string.IsNullOrEmpty(propertyPath))
                return false;

            string rootName = GetRootPropertyName(propertyPath);
            if (rootName == "m_Script")
                return false;

            FieldInfo field = FindField(targetType, rootName);
            return ShouldShowCustomMember(field, targetType);
        }

        /// <summary>只接受真实序列化字段，并过滤 UIPanel 框架字段和生成数据缓存。</summary>
        /// <param name="memberInfo">待检查成员。</param>
        /// <param name="targetType">当前面板类型。</param>
        /// <returns>成员属于派生业务字段时返回 true。</returns>
        private static bool ShouldShowCustomMember(MemberInfo memberInfo, Type targetType)
        {
            if (!(memberInfo is FieldInfo field) || targetType == null)
                return false;

            Type declaringType = field.DeclaringType;
            if (declaringType == null || !declaringType.IsAssignableFrom(targetType))
                return false;

            return !IsFrameworkPanelType(declaringType) && !IsGeneratedPanelDataField(field);
        }

        /// <summary>识别当前包程序集中的 UIPanel 框架声明字段。</summary>
        /// <param name="declaringType">字段声明类型。</param>
        /// <returns>属于框架面板类型时返回 true。</returns>
        private static bool IsFrameworkPanelType(Type declaringType)
        {
            return declaringType != null
                && typeof(UIPanel).IsAssignableFrom(declaringType)
                && declaringType.Assembly == typeof(UIPanel).Assembly;
        }

        /// <summary>识别生成面板数据字段，避免把运行时缓存展示到业务属性卡片。</summary>
        /// <param name="field">待检查字段。</param>
        /// <returns>字段是 IUIData 数据缓存时返回 true。</returns>
        private static bool IsGeneratedPanelDataField(FieldInfo field)
        {
            return field != null
                && field.Name == "mData"
                && typeof(IUIData).IsAssignableFrom(field.FieldType);
        }

        /// <summary>从当前类型及基类查找序列化字段。</summary>
        /// <param name="type">起始类型。</param>
        /// <param name="fieldName">字段名称。</param>
        /// <returns>找到的字段；不存在时返回空。</returns>
        private static FieldInfo FindField(Type type, string fieldName)
        {
            Type current = type;
            while (current != null)
            {
                FieldInfo field = current.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field;
                current = current.BaseType;
            }

            return default;
        }

        /// <summary>取得序列化路径对应的根字段名称，忽略嵌套成员和数组索引。</summary>
        /// <param name="propertyPath">Unity 序列化属性路径。</param>
        /// <returns>根字段名称。</returns>
        private static string GetRootPropertyName(string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath))
                return string.Empty;

            int dotIndex = propertyPath.IndexOf('.');
            return dotIndex >= 0 ? propertyPath.Substring(0, dotIndex) : propertyPath;
        }

        /// <summary>检查编辑器当前全部目标，避免 Unity 刷新期间创建无效 SerializedObject。</summary>
        /// <param name="editorTargets">Editor 当前目标集合。</param>
        /// <returns>所有目标均有效时返回 true。</returns>
        private static bool HasValidTargets(UnityEngine.Object[] editorTargets)
        {
            if (editorTargets == null || editorTargets.Length == 0)
                return false;

            for (var index = 0; index < editorTargets.Length; index++)
            {
                if (editorTargets[index] == default)
                    return false;
            }

            return true;
        }

        /// <summary>安全读取 Unity SerializedObject，并吞掉编译刷新期间的无效对象异常。</summary>
        /// <param name="currentSerializedObject">输出可用序列化对象。</param>
        /// <returns>序列化对象可用时返回 true。</returns>
        private bool TryGetSerializedObject(out SerializedObject currentSerializedObject)
        {
            currentSerializedObject = default;
            if (!HasValidTargets(targets))
                return false;

            try
            {
                currentSerializedObject = serializedObject;
            }
            catch (Exception exception)
            {
                if (IsInvalidSerializedObjectException(exception))
                    return false;
                throw;
            }

            return currentSerializedObject != null && currentSerializedObject.targetObject != default;
        }

        /// <summary>识别 Unity 刷新期间可能抛出的 SerializedObject 创建异常。</summary>
        /// <param name="exception">待判断异常。</param>
        /// <returns>属于无效序列化对象异常时返回 true。</returns>
        private static bool IsInvalidSerializedObjectException(Exception exception)
        {
            return exception is InvalidOperationException
                || (exception != null && exception.GetType().Name == "SerializedObjectNotCreatableException");
        }
    }
}
#endif
