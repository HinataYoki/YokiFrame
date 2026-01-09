#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 绑定系统配置 - 管理命名规则、验证选项等
    /// </summary>
    [FilePath("ProjectSettings/UIKitBindConfig.asset", FilePathAttribute.Location.ProjectFolder)]
    public partial class UIKitBindConfig : ScriptableSingleton<UIKitBindConfig>
    {
        #region 静态访问

        /// <summary>
        /// 配置单例实例
        /// </summary>
        public static UIKitBindConfig Instance => instance;

        #endregion

        #region 类型前缀映射

        /// <summary>
        /// 组件类型到前缀的映射
        /// </summary>
        [Serializable]
        public class TypePrefixMapping
        {
            /// <summary>
            /// 组件完整类型名（如 "UnityEngine.UI.Button"）
            /// </summary>
            public string ComponentTypeName;

            /// <summary>
            /// 前缀（如 "Btn"）
            /// </summary>
            public string Prefix;

            public TypePrefixMapping() { }

            public TypePrefixMapping(string typeName, string prefix)
            {
                ComponentTypeName = typeName;
                Prefix = prefix;
            }
        }

        /// <summary>
        /// 自定义类型前缀映射列表
        /// </summary>
        [SerializeField]
        private List<TypePrefixMapping> mCustomPrefixMappings = new();

        /// <summary>
        /// 默认前缀映射（只读）
        /// </summary>
        private static readonly Dictionary<string, string> sDefaultPrefixes = new()
        {
            // Unity UI
            { "UnityEngine.UI.Button", "Btn" },
            { "UnityEngine.UI.Image", "Img" },
            { "UnityEngine.UI.RawImage", "RawImg" },
            { "UnityEngine.UI.Text", "Txt" },
            { "UnityEngine.UI.Toggle", "Tog" },
            { "UnityEngine.UI.Slider", "Sld" },
            { "UnityEngine.UI.Scrollbar", "Scr" },
            { "UnityEngine.UI.ScrollRect", "Scroll" },
            { "UnityEngine.UI.InputField", "Input" },
            { "UnityEngine.UI.Dropdown", "Drop" },
            // TextMeshPro
            { "TMPro.TextMeshProUGUI", "Txt" },
            { "TMPro.TMP_Text", "Txt" },
            { "TMPro.TMP_InputField", "Input" },
            { "TMPro.TMP_Dropdown", "Drop" },
            // Transform
            { "UnityEngine.RectTransform", "Rect" },
            { "UnityEngine.Transform", "Trans" },
            // Canvas
            { "UnityEngine.CanvasGroup", "CG" },
            { "UnityEngine.Canvas", "Canvas" },
        };

        #endregion

        #region 生成选项

        /// <summary>
        /// 是否启用增量生成（仅在内容变化时写入文件）
        /// </summary>
        [Tooltip("启用后，仅在生成内容与现有文件不同时才写入")]
        public bool EnableIncrementalGeneration = true;

        /// <summary>
        /// 是否在生成前自动验证
        /// </summary>
        [Tooltip("启用后，代码生成前会自动验证绑定配置")]
        public bool ValidateBeforeGeneration = true;

        /// <summary>
        /// 验证失败时是否阻止生成
        /// </summary>
        [Tooltip("启用后，存在验证错误时将阻止代码生成")]
        public bool BlockGenerationOnError = true;

        #endregion

        #region 命名选项

        /// <summary>
        /// 批量绑定时是否使用组件类型前缀
        /// </summary>
        [Tooltip("批量添加绑定时，自动在字段名前添加组件类型前缀")]
        public bool UseTypePrefixOnBatchBind = true;

        /// <summary>
        /// 是否保留 GameObject 原始名称
        /// </summary>
        [Tooltip("生成字段名时保留 GameObject 的原始名称部分")]
        public bool PreserveGameObjectName = true;

        #endregion

        #region 公共方法

        /// <summary>
        /// 获取组件类型的前缀
        /// </summary>
        /// <param name="componentTypeName">组件完整类型名</param>
        /// <returns>前缀，如果未找到则返回空字符串</returns>
        public string GetPrefix(string componentTypeName)
        {
            if (string.IsNullOrEmpty(componentTypeName))
                return string.Empty;

            // 优先查找自定义映射
            foreach (var mapping in mCustomPrefixMappings)
            {
                if (mapping.ComponentTypeName == componentTypeName)
                    return mapping.Prefix ?? string.Empty;
            }

            // 回退到默认映射
            return sDefaultPrefixes.TryGetValue(componentTypeName, out var prefix) 
                ? prefix 
                : string.Empty;
        }

        /// <summary>
        /// 获取组件类型的前缀（通过 Type）
        /// </summary>
        /// <param name="componentType">组件类型</param>
        /// <returns>前缀，如果未找到则返回空字符串</returns>
        public string GetPrefix(Type componentType)
        {
            if (componentType == null)
                return string.Empty;

            return GetPrefix(componentType.FullName);
        }

        /// <summary>
        /// 添加或更新自定义前缀映射
        /// </summary>
        /// <param name="componentTypeName">组件完整类型名</param>
        /// <param name="prefix">前缀</param>
        public void SetCustomPrefix(string componentTypeName, string prefix)
        {
            if (string.IsNullOrEmpty(componentTypeName))
                return;

            // 查找是否已存在
            for (int i = 0; i < mCustomPrefixMappings.Count; i++)
            {
                if (mCustomPrefixMappings[i].ComponentTypeName == componentTypeName)
                {
                    mCustomPrefixMappings[i].Prefix = prefix;
                    Save(true);
                    return;
                }
            }

            // 添加新映射
            mCustomPrefixMappings.Add(new TypePrefixMapping(componentTypeName, prefix));
            Save(true);
        }

        /// <summary>
        /// 移除自定义前缀映射
        /// </summary>
        /// <param name="componentTypeName">组件完整类型名</param>
        /// <returns>是否成功移除</returns>
        public bool RemoveCustomPrefix(string componentTypeName)
        {
            for (int i = mCustomPrefixMappings.Count - 1; i >= 0; i--)
            {
                if (mCustomPrefixMappings[i].ComponentTypeName == componentTypeName)
                {
                    mCustomPrefixMappings.RemoveAt(i);
                    Save(true);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 获取所有前缀映射（包括默认和自定义）
        /// </summary>
        /// <returns>类型名到前缀的字典</returns>
        public Dictionary<string, string> GetAllPrefixMappings()
        {
            var result = new Dictionary<string, string>(sDefaultPrefixes);

            // 自定义映射覆盖默认映射
            foreach (var mapping in mCustomPrefixMappings)
            {
                result[mapping.ComponentTypeName] = mapping.Prefix;
            }

            return result;
        }

        /// <summary>
        /// 获取自定义前缀映射列表（用于序列化）
        /// </summary>
        public IReadOnlyList<TypePrefixMapping> CustomPrefixMappings => mCustomPrefixMappings;

        /// <summary>
        /// 获取默认前缀映射（只读）
        /// </summary>
        public static IReadOnlyDictionary<string, string> DefaultPrefixes => sDefaultPrefixes;

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        public void ResetToDefault()
        {
            mCustomPrefixMappings.Clear();
            EnableIncrementalGeneration = true;
            ValidateBeforeGeneration = true;
            BlockGenerationOnError = true;
            UseTypePrefixOnBatchBind = true;
            PreserveGameObjectName = true;
            Save(true);
        }

        #endregion

    }
}
#endif
