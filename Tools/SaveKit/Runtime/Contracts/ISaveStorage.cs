using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// SaveKit 文档存储后端。实现负责线程安全和物理存储的原子性。
    /// </summary>
    public interface ISaveStorage
    {
        /// <summary>判断目标文档是否存在。</summary>
        /// <param name="target">保存目标。</param>
        /// <returns>目标存在时返回 true。</returns>
        bool Exists(SaveTarget target);

        /// <summary>写入目标文档的完整文件字节。</summary>
        /// <param name="target">保存目标。</param>
        /// <param name="bytes">完整容器字节。</param>
        void Write(SaveTarget target, byte[] bytes);

        /// <summary>读取目标文档的完整文件字节。</summary>
        /// <param name="target">保存目标。</param>
        /// <returns>目标不存在时返回空。</returns>
        byte[] Read(SaveTarget target);

        /// <summary>删除目标文档。</summary>
        /// <param name="target">保存目标。</param>
        /// <returns>实际删除文件时返回 true。</returns>
        bool Delete(SaveTarget target);

        /// <summary>枚举指定类型的持久化目标。</summary>
        /// <param name="kind">目标类型。</param>
        /// <returns>目标快照。</returns>
        IReadOnlyList<SaveTarget> GetTargets(SaveTargetKind kind);

        /// <summary>清空指定类型的全部目标。</summary>
        /// <param name="kind">目标类型。</param>
        void Clear(SaveTargetKind kind);
    }
}
