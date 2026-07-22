using System;

namespace YokiFrame
{
    /// <summary>SaveKit 与 Architecture IModel 的收集和恢复入口。</summary>
    public static partial class SaveKit
    {
        /// <summary>收集 Architecture 中全部 IModel。</summary>
        /// <typeparam name="T">Architecture 类型。</typeparam>
        /// <param name="data">接收模块的保存数据。</param>
        public static void CollectFromArchitecture<T>(SaveData data) where T : Architecture<T>, new()
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var architecture = Architecture<T>.Interface;
            foreach (var service in architecture.GetAllServices())
            {
                if (service is IModel model)
                {
                    data.RegisterModuleByType(model, model.GetType());
                }
            }
        }

        /// <summary>把保存数据中的 IModel 覆盖回当前 Architecture。</summary>
        /// <typeparam name="T">Architecture 类型。</typeparam>
        /// <param name="data">包含模型 payload 的保存数据。</param>
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
                if (!(service is IModel model))
                {
                    continue;
                }

                var id = SaveModuleIdentity.GetId(model.GetType());
                var bytes = data.GetRawModuleOrSerializedRef(id, serializer);
                if (bytes != null)
                {
                    if (serializer is IModuleIdAwareSaveSerializer idAwareSerializer)
                    {
                        idAwareSerializer.DeserializeOverwrite(id, bytes, model);
                    }
                    else
                    {
                        serializer.DeserializeOverwrite(bytes, model);
                    }
                }
            }
        }
    }
}
