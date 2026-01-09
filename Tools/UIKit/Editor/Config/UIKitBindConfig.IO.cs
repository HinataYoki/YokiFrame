#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 绑定系统配置 - 导入导出功能
    /// </summary>
    public partial class UIKitBindConfig
    {
        #region 导入导出

        /// <summary>
        /// 配置导出数据结构
        /// </summary>
        [Serializable]
        public class ConfigExportData
        {
            public List<TypePrefixMapping> CustomPrefixMappings = new();
            public bool EnableIncrementalGeneration = true;
            public bool ValidateBeforeGeneration = true;
            public bool BlockGenerationOnError = true;
            public bool UseTypePrefixOnBatchBind = true;
            public bool PreserveGameObjectName = true;
        }

        /// <summary>
        /// 导出配置为 JSON 字符串
        /// </summary>
        /// <returns>JSON 格式的配置数据</returns>
        public string ExportToJson()
        {
            ConfigExportData data = new()
            {
                CustomPrefixMappings = new List<TypePrefixMapping>(mCustomPrefixMappings),
                EnableIncrementalGeneration = EnableIncrementalGeneration,
                ValidateBeforeGeneration = ValidateBeforeGeneration,
                BlockGenerationOnError = BlockGenerationOnError,
                UseTypePrefixOnBatchBind = UseTypePrefixOnBatchBind,
                PreserveGameObjectName = PreserveGameObjectName
            };

            return JsonUtility.ToJson(data, true);
        }

        /// <summary>
        /// 从 JSON 字符串导入配置
        /// </summary>
        /// <param name="json">JSON 格式的配置数据</param>
        /// <param name="merge">是否合并（true）或覆盖（false）现有配置</param>
        /// <returns>是否导入成功</returns>
        public bool ImportFromJson(string json, bool merge = false)
        {
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                var data = JsonUtility.FromJson<ConfigExportData>(json);
                if (data == null)
                    return false;

                // 导入前缀映射
                if (merge)
                {
                    // 合并模式：添加不存在的映射
                    foreach (var mapping in data.CustomPrefixMappings)
                    {
                        bool exists = false;
                        foreach (var existing in mCustomPrefixMappings)
                        {
                            if (existing.ComponentTypeName == mapping.ComponentTypeName)
                            {
                                exists = true;
                                break;
                            }
                        }
                        if (!exists)
                        {
                            mCustomPrefixMappings.Add(mapping);
                        }
                    }
                }
                else
                {
                    // 覆盖模式：替换所有映射
                    mCustomPrefixMappings.Clear();
                    mCustomPrefixMappings.AddRange(data.CustomPrefixMappings);
                }

                // 导入其他选项
                EnableIncrementalGeneration = data.EnableIncrementalGeneration;
                ValidateBeforeGeneration = data.ValidateBeforeGeneration;
                BlockGenerationOnError = data.BlockGenerationOnError;
                UseTypePrefixOnBatchBind = data.UseTypePrefixOnBatchBind;
                PreserveGameObjectName = data.PreserveGameObjectName;

                Save(true);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[UIKitBindConfig] 导入配置失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 导出配置到文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否导出成功</returns>
        public bool ExportToFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            try
            {
                string json = ExportToJson();
                System.IO.File.WriteAllText(filePath, json);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[UIKitBindConfig] 导出配置到文件失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从文件导入配置
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="merge">是否合并（true）或覆盖（false）现有配置</param>
        /// <returns>是否导入成功</returns>
        public bool ImportFromFile(string filePath, bool merge = false)
        {
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
                return false;

            try
            {
                string json = System.IO.File.ReadAllText(filePath);
                return ImportFromJson(json, merge);
            }
            catch (Exception e)
            {
                Debug.LogError($"[UIKitBindConfig] 从文件导入配置失败: {e.Message}");
                return false;
            }
        }

        #endregion
    }
}
#endif
