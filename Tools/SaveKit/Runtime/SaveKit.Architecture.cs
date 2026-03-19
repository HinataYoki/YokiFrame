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
        /// 从 Architecture 一键注册所有 IModel 到 SaveData
        /// 等价于对每个 IModel 手动调用 RegisterModule，注册的是对象引用
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
                CollectModelsFromArchitecture(architecture, models);

                foreach (var model in models)
                {
                    data.RegisterModuleByType(model, model.GetType());
                }

                KitLogger.Log($"[SaveKit] 从 Architecture 注册了 {models.Count} 个 Model");
            });
        }

        /// <summary>
        /// 将 SaveData 中的数据一键应用回 Architecture 的 IModel
        /// 通过 JsonUtility.FromJsonOverwrite 直接覆盖 Architecture 中的 Model 数据
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

                    // 优先从引用模块获取字节（已注册的模块保存后在 raw data 中）
                    // 也兼容手动 RegisterModule 后保存的数据
                    if (!data.HasRawModule(typeKey))
                        continue;

                    try
                    {
                        var bytes = data.GetRawModule(typeKey);
                        var json = System.Text.Encoding.UTF8.GetString(bytes);
                        JsonUtility.FromJsonOverwrite(json, model);
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
            foreach (var service in architecture.GetAllServices())
            {
                if (service is IModel model)
                {
                    models.Add(model);
                }
            }
        }

        #endregion
    }
}
