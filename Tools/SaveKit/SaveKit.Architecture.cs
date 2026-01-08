using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// SaveKit Architecture 集成扩展
    /// </summary>
    public static partial class SaveKit
    {
        #region Architecture 集成

        /// <summary>
        /// 从 Architecture 收集所有 IModel 数据
        /// </summary>
        /// <typeparam name="T">Architecture 类型</typeparam>
        /// <param name="data">要填充的 SaveData</param>
        public static void CollectFromArchitecture<T>(SaveData data) where T : Architecture<T>, new()
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var architecture = Architecture<T>.Interface;
            
            Pool.List<IModel>(models =>
            {
                // 获取所有 IModel 服务
                CollectModelsFromArchitecture(architecture, models);

                var serializer = GetSerializer();
                foreach (var model in models)
                {
                    var modelType = model.GetType();
                    var typeKey = modelType.FullName.GetHashCode();

                    // 使用 JsonUtility 序列化 Model
                    var jsonData = JsonUtility.ToJson(model);
                    var modelWrapper = new SerializableModelData
                    {
                        TypeName = modelType.AssemblyQualifiedName,
                        Data = jsonData
                    };

                    var bytes = serializer.Serialize(modelWrapper);
                    data.SetRawModule(typeKey, bytes);
                }

                KitLogger.Log($"[SaveKit] 从 Architecture 收集了 {models.Count} 个 Model");
            });
        }

        /// <summary>
        /// 将 SaveData 应用到 Architecture 的 IModel
        /// </summary>
        /// <typeparam name="T">Architecture 类型</typeparam>
        /// <param name="data">包含数据的 SaveData</param>
        public static void ApplyToArchitecture<T>(SaveData data) where T : Architecture<T>, new()
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var architecture = Architecture<T>.Interface;
            
            Pool.List<IModel>(models =>
            {
                CollectModelsFromArchitecture(architecture, models);

                var appliedCount = 0;
                var serializer = GetSerializer();

                foreach (var model in models)
                {
                    var modelType = model.GetType();
                    var typeKey = modelType.FullName.GetHashCode();

                    if (!data.HasRawModule(typeKey))
                    {
                        continue;
                    }

                    try
                    {
                        var bytes = data.GetRawModule(typeKey);
                        var modelWrapper = serializer.Deserialize<SerializableModelData>(bytes);

                        if (modelWrapper.TypeName != modelType.AssemblyQualifiedName)
                        {
                            KitLogger.Warning($"[SaveKit] 类型不匹配: {modelWrapper.TypeName} vs {modelType.AssemblyQualifiedName}");
                            continue;
                        }

                        // 使用 JsonUtility 覆盖对象数据
                        JsonUtility.FromJsonOverwrite(modelWrapper.Data, model);
                        appliedCount++;
                    }
                    catch (Exception ex)
                    {
                        KitLogger.Warning($"[SaveKit] 应用数据到 {modelType.Name} 失败: {ex.Message}");
                    }
                }

                KitLogger.Log($"[SaveKit] 已应用 {appliedCount} 个 Model 数据");
            });
        }

        private static void CollectModelsFromArchitecture(IArchitecture architecture, List<IModel> models)
        {
            // 从所有服务中筛选出 IModel
            foreach (var service in architecture.GetAllServices())
            {
                if (service is IModel model)
                {
                    models.Add(model);
                }
            }
        }

        /// <summary>
        /// 用于序列化 Model 数据的包装类
        /// </summary>
        [Serializable]
        private class SerializableModelData
        {
            public string TypeName;
            public string Data;
        }

        #endregion
    }
}
