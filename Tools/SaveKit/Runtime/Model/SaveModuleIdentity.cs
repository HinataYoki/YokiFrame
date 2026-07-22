using System;

namespace YokiFrame
{
    /// <summary>
    /// 计算模块的持久化 ID。显式注册 ID 优先，未提供时退回完整类型名。
    /// </summary>
    internal static class SaveModuleIdentity
    {
        /// <summary>获取强类型模块的稳定 ID。</summary>
        /// <typeparam name="T">模块类型。</typeparam>
        /// <param name="moduleId">可选显式模块 ID。</param>
        /// <returns>稳定模块 ID。</returns>
        public static string GetId<T>(string moduleId = null)
        {
            return GetId(typeof(T), moduleId);
        }

        /// <summary>获取运行时模块类型的稳定 ID。</summary>
        /// <param name="type">模块类型。</param>
        /// <param name="moduleId">可选显式模块 ID。</param>
        /// <returns>稳定模块 ID。</returns>
        public static string GetId(Type type, string moduleId = null)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (!string.IsNullOrEmpty(moduleId))
            {
                ValidateId(moduleId);
                return moduleId;
            }

            if (string.IsNullOrEmpty(type.FullName))
            {
                throw new ArgumentException("Save module type must have a full name.", nameof(type));
            }

            return type.FullName;
        }

        /// <summary>验证从容器读取的模块 ID。</summary>
        /// <param name="id">待验证模块 ID。</param>
        public static void ValidateId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 256)
            {
                throw new ArgumentException("Save module id must contain 1 to 256 characters.", nameof(id));
            }
        }
    }
}
