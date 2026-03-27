using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.NodeKit.Editor
{
    /// <summary>
    /// 编辑器工具类
    /// </summary>
    public static class NodeEditorUtility
    {
        private static readonly Dictionary<Type, Color> sTypeColors = new()
        {
            { typeof(float), new Color(0.5f, 0.8f, 0.5f) },
            { typeof(int), new Color(0.5f, 0.7f, 0.9f) },
            { typeof(bool), new Color(0.9f, 0.5f, 0.5f) },
            { typeof(string), new Color(0.9f, 0.7f, 0.5f) },
            { typeof(Vector2), new Color(0.9f, 0.9f, 0.5f) },
            { typeof(Vector3), new Color(0.9f, 0.9f, 0.5f) },
            { typeof(Vector4), new Color(0.9f, 0.9f, 0.5f) },
            { typeof(Color), new Color(0.9f, 0.5f, 0.9f) },
            { typeof(GameObject), new Color(0.5f, 0.9f, 0.9f) },
            { typeof(UnityEngine.Object), new Color(0.7f, 0.7f, 0.7f) },
        };

        /// <summary>
        /// 获取类型颜色
        /// </summary>
        public static Color GetTypeColor(Type type)
        {
            if (type == default) return Color.gray;
            if (sTypeColors.TryGetValue(type, out var color)) return color;

            // 检查基类
            var current = type.BaseType;
            while (current != default)
            {
                if (sTypeColors.TryGetValue(current, out color)) return color;
                current = current.BaseType;
            }

            // 默认颜色
            return new Color(0.7f, 0.7f, 0.7f);
        }

        /// <summary>
        /// 注册类型颜色
        /// </summary>
        public static void RegisterTypeColor(Type type, Color color) => sTypeColors[type] = color;

        /// <summary>
        /// 记录 Undo
        /// </summary>
        public static void RecordUndo(UnityEngine.Object target, string name)
        {
            if (target == default) return;
            Undo.RecordObject(target, name);
        }

        /// <summary>
        /// 记录多个对象的 Undo
        /// </summary>
        public static void RecordUndo(UnityEngine.Object[] targets, string name)
        {
            if (targets == default || targets.Length == 0) return;
            Undo.RecordObjects(targets, name);
        }

        /// <summary>
        /// 标记脏
        /// </summary>
        public static void SetDirty(UnityEngine.Object target)
        {
            if (target == default) return;
            EditorUtility.SetDirty(target);
        }

        /// <summary>
        /// 保存资产
        /// </summary>
        public static void SaveAsset(UnityEngine.Object target)
        {
            if (target == default) return;
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
        }
    }
}
