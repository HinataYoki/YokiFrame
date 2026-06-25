using System;

namespace YokiFrame
{
    /// <summary>
    /// SaveKit 与 Architecture 的 1.x 兼容集成入口。
    /// </summary>
    public static partial class SaveKit
    {
        /// <summary>
        /// 从 Architecture 中收集所有 IModel 并注册到 SaveData。
        /// </summary>
        /// <typeparam name="T">Architecture 类型。</typeparam>
        /// <param name="data">要填充的保存数据。</param>
        public static void CollectFromArchitecture<T>(SaveData data) where T : Architecture<T>, new()
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var architecture = Architecture<T>.Interface;
            foreach (var service in architecture.GetAllServices())
            {
                var model = service as IModel;
                if (model != null)
                {
                    data.RegisterModuleByType(model, model.GetType());
                }
            }
        }

        /// <summary>
        /// 将 SaveData 中的 IModel 数据覆盖回 Architecture。
        /// </summary>
        /// <typeparam name="T">Architecture 类型。</typeparam>
        /// <param name="data">包含模型数据的保存数据。</param>
        public static void ApplyToArchitecture<T>(SaveData data) where T : Architecture<T>, new()
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var serializer = GetSerializer();
            var architecture = Architecture<T>.Interface;
            foreach (var service in architecture.GetAllServices())
            {
                var model = service as IModel;
                if (model == null)
                {
                    continue;
                }

                var key = model.GetType().FullName.GetHashCode();
                var bytes = data.GetRawModuleOrSerializedRef(key, serializer);
                if (bytes == null)
                {
                    continue;
                }

                serializer.DeserializeOverwrite(bytes, model);
            }
        }
    }
}
