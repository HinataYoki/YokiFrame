using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using YokiFrame.ECS;

namespace YokiFrame.Editor.ECS
{
    /// <summary>
    /// ECSSystemRunner的Inspector编辑器
    /// 提供System类型下拉选择
    /// </summary>
    [CustomEditor(typeof(ECSSystemRunner))]
    public class ECSSystemRunnerEditor : UnityEditor.Editor
    {
        private SerializedProperty _worldNameProp;
        private SerializedProperty _autoCreateWorldProp;
        private SerializedProperty _autoUpdateProp;
        private SerializedProperty _systemsProp;
        
        private List<Type> _availableSystemTypes;
        private string[] _systemTypeNames;
        private bool _systemsFoldout = true;
        
        private void OnEnable()
        {
            _worldNameProp = serializedObject.FindProperty("_worldName");
            _autoCreateWorldProp = serializedObject.FindProperty("_autoCreateWorld");
            _autoUpdateProp = serializedObject.FindProperty("_autoUpdate");
            _systemsProp = serializedObject.FindProperty("_systems");
            
            RefreshAvailableSystemTypes();
        }
        
        private void RefreshAvailableSystemTypes()
        {
            _availableSystemTypes = new List<Type>();
            
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (typeof(IECSSystem).IsAssignableFrom(type) && 
                            !type.IsAbstract && 
                            !type.IsInterface &&
                            type.GetConstructor(Type.EmptyTypes) != null)
                        {
                            _availableSystemTypes.Add(type);
                        }
                    }
                }
                catch { }
            }
            
            _availableSystemTypes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            _systemTypeNames = _availableSystemTypes.Select(t => t.Name).ToArray();
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            EditorGUILayout.PropertyField(_worldNameProp, new GUIContent("World名称"));
            EditorGUILayout.PropertyField(_autoCreateWorldProp, new GUIContent("自动创建World"));
            EditorGUILayout.PropertyField(_autoUpdateProp, new GUIContent("自动更新"));
            
            EditorGUILayout.Space();
            
            _systemsFoldout = EditorGUILayout.Foldout(_systemsFoldout, "Systems", true);
            if (_systemsFoldout)
            {
                EditorGUI.indentLevel++;
                DrawSystemsList();
                EditorGUI.indentLevel--;
            }
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private void DrawSystemsList()
        {
            for (int i = 0; i < _systemsProp.arraySize; i++)
            {
                var element = _systemsProp.GetArrayElementAtIndex(i);
                var assemblyQualifiedNameProp = element.FindPropertyRelative("_assemblyQualifiedName");
                var enabledProp = element.FindPropertyRelative("_enabled");
                
                EditorGUILayout.BeginHorizontal();
                
                enabledProp.boolValue = EditorGUILayout.Toggle(enabledProp.boolValue, GUILayout.Width(20));
                
                var currentType = Type.GetType(assemblyQualifiedNameProp.stringValue);
                int currentIndex = currentType != null ? _availableSystemTypes.IndexOf(currentType) : -1;
                
                int newIndex = EditorGUILayout.Popup(currentIndex, _systemTypeNames);
                if (newIndex != currentIndex && newIndex >= 0 && newIndex < _availableSystemTypes.Count)
                {
                    assemblyQualifiedNameProp.stringValue = _availableSystemTypes[newIndex].AssemblyQualifiedName;
                }
                
                if (GUILayout.Button("-", GUILayout.Width(25)))
                {
                    _systemsProp.DeleteArrayElementAtIndex(i);
                    i--;
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ 添加System", GUILayout.Width(100)))
            {
                _systemsProp.InsertArrayElementAtIndex(_systemsProp.arraySize);
                var newElement = _systemsProp.GetArrayElementAtIndex(_systemsProp.arraySize - 1);
                newElement.FindPropertyRelative("_assemblyQualifiedName").stringValue = "";
                newElement.FindPropertyRelative("_enabled").boolValue = true;
            }
            if (GUILayout.Button("刷新", GUILayout.Width(60)))
            {
                RefreshAvailableSystemTypes();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
    
    /// <summary>
    /// ECSEntityBinder的Inspector编辑器
    /// 提供组件类型下拉选择和字段编辑
    /// </summary>
    [CustomEditor(typeof(ECSEntityBinder))]
    public class ECSEntityBinderEditor : UnityEditor.Editor
    {
        private SerializedProperty _worldNameProp;
        private SerializedProperty _autoCreateEntityProp;
        private SerializedProperty _syncTransformToECSProp;
        private SerializedProperty _syncECSToTransformProp;
        private SerializedProperty _componentsProp;
        
        private List<Type> _availableComponentTypes;
        private string[] _componentTypeNames;
        private bool _componentsFoldout = true;
        
        private void OnEnable()
        {
            _worldNameProp = serializedObject.FindProperty("_worldName");
            _autoCreateEntityProp = serializedObject.FindProperty("_autoCreateEntity");
            _syncTransformToECSProp = serializedObject.FindProperty("_syncTransformToECS");
            _syncECSToTransformProp = serializedObject.FindProperty("_syncECSToTransform");
            _componentsProp = serializedObject.FindProperty("_components");
            
            RefreshAvailableComponentTypes();
        }
        
        private void RefreshAvailableComponentTypes()
        {
            _availableComponentTypes = new List<Type>();
            
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.IsValueType && 
                            !type.IsPrimitive && 
                            !type.IsEnum &&
                            type.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() == null)
                        {
                            if (typeof(IComponentData).IsAssignableFrom(type) || IsUnmanagedStruct(type))
                            {
                                _availableComponentTypes.Add(type);
                            }
                        }
                    }
                }
                catch { }
            }
            
            _availableComponentTypes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            _componentTypeNames = _availableComponentTypes.Select(t => t.Name).ToArray();
        }
        
        private bool IsUnmanagedStruct(Type type)
        {
            if (!type.IsValueType) return false;
            if (type.IsPrimitive) return true;
            if (type.IsEnum) return true;
            
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!field.FieldType.IsValueType) return false;
                if (!field.FieldType.IsPrimitive && !field.FieldType.IsEnum && !IsUnmanagedStruct(field.FieldType))
                    return false;
            }
            return true;
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            EditorGUILayout.PropertyField(_worldNameProp, new GUIContent("World名称"));
            EditorGUILayout.PropertyField(_autoCreateEntityProp, new GUIContent("自动创建实体"));
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Transform同步", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_syncTransformToECSProp, new GUIContent("Transform -> ECS"));
            if (EditorGUI.EndChangeCheck() && _syncTransformToECSProp.boolValue)
            {
                _syncECSToTransformProp.boolValue = false;
            }
            
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_syncECSToTransformProp, new GUIContent("ECS -> Transform"));
            if (EditorGUI.EndChangeCheck() && _syncECSToTransformProp.boolValue)
            {
                _syncTransformToECSProp.boolValue = false;
            }
            
            EditorGUI.indentLevel--;
            
            EditorGUILayout.Space();
            
            _componentsFoldout = EditorGUILayout.Foldout(_componentsFoldout, "ECS组件", true);
            if (_componentsFoldout)
            {
                EditorGUI.indentLevel++;
                DrawComponentsList();
                EditorGUI.indentLevel--;
            }
            
            // 运行时信息
            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("运行时信息", EditorStyles.boldLabel);
                var binder = (ECSEntityBinder)target;
                EditorGUILayout.LabelField("实体:", binder.Entity.ToString());
                EditorGUILayout.LabelField("有效:", binder.IsValid.ToString());
            }
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private void DrawComponentsList()
        {
            for (int i = 0; i < _componentsProp.arraySize; i++)
            {
                var element = _componentsProp.GetArrayElementAtIndex(i);
                var assemblyQualifiedNameProp = element.FindPropertyRelative("_assemblyQualifiedName");
                var jsonDataProp = element.FindPropertyRelative("_jsonData");
                
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                
                var currentType = Type.GetType(assemblyQualifiedNameProp.stringValue);
                int currentIndex = currentType != null ? _availableComponentTypes.IndexOf(currentType) : -1;
                
                int newIndex = EditorGUILayout.Popup(currentIndex, _componentTypeNames);
                if (newIndex != currentIndex && newIndex >= 0 && newIndex < _availableComponentTypes.Count)
                {
                    assemblyQualifiedNameProp.stringValue = _availableComponentTypes[newIndex].AssemblyQualifiedName;
                    jsonDataProp.stringValue = "";
                }
                
                if (GUILayout.Button("-", GUILayout.Width(25)))
                {
                    _componentsProp.DeleteArrayElementAtIndex(i);
                    i--;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    continue;
                }
                
                EditorGUILayout.EndHorizontal();
                
                // 显示组件字段编辑器
                if (currentType != null)
                {
                    DrawComponentFields(currentType, jsonDataProp);
                }
                
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ 添加组件", GUILayout.Width(100)))
            {
                _componentsProp.InsertArrayElementAtIndex(_componentsProp.arraySize);
                var newElement = _componentsProp.GetArrayElementAtIndex(_componentsProp.arraySize - 1);
                newElement.FindPropertyRelative("_assemblyQualifiedName").stringValue = "";
                newElement.FindPropertyRelative("_jsonData").stringValue = "";
            }
            if (GUILayout.Button("刷新", GUILayout.Width(60)))
            {
                RefreshAvailableComponentTypes();
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawComponentFields(Type componentType, SerializedProperty jsonDataProp)
        {
            object instance;
            try
            {
                if (!string.IsNullOrEmpty(jsonDataProp.stringValue))
                {
                    instance = JsonUtility.FromJson(jsonDataProp.stringValue, componentType);
                }
                else
                {
                    instance = Activator.CreateInstance(componentType);
                }
            }
            catch
            {
                instance = Activator.CreateInstance(componentType);
            }
            
            EditorGUI.indentLevel++;
            bool changed = false;
            
            foreach (var field in componentType.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                var value = field.GetValue(instance);
                object newValue = DrawField(field.Name, field.FieldType, value);
                
                if (!Equals(value, newValue))
                {
                    field.SetValue(instance, newValue);
                    changed = true;
                }
            }
            
            if (changed)
            {
                jsonDataProp.stringValue = JsonUtility.ToJson(instance);
            }
            
            EditorGUI.indentLevel--;
        }
        
        private object DrawField(string name, Type fieldType, object value)
        {
            if (fieldType == typeof(int))
                return EditorGUILayout.IntField(name, (int)value);
            if (fieldType == typeof(float))
                return EditorGUILayout.FloatField(name, (float)value);
            if (fieldType == typeof(bool))
                return EditorGUILayout.Toggle(name, (bool)value);
            if (fieldType == typeof(string))
                return EditorGUILayout.TextField(name, (string)value ?? "");
            if (fieldType == typeof(Vector2))
                return EditorGUILayout.Vector2Field(name, (Vector2)value);
            if (fieldType == typeof(Vector3))
                return EditorGUILayout.Vector3Field(name, (Vector3)value);
            if (fieldType == typeof(Vector4))
                return EditorGUILayout.Vector4Field(name, (Vector4)value);
            if (fieldType == typeof(Color))
                return EditorGUILayout.ColorField(name, (Color)value);
            if (fieldType.IsEnum)
                return EditorGUILayout.EnumPopup(name, (Enum)value);
            
            EditorGUILayout.LabelField(name, value?.ToString() ?? "null");
            return value;
        }
    }
}
