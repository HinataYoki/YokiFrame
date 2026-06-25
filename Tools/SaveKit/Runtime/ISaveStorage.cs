using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 保存槽位存储后端接口。
    /// SaveKit 不在接口层提供并发锁；同一个存储实例若会被多线程访问，实现方需要自行保证线程安全。
    /// </summary>
    public interface ISaveStorage
    {
        /// <summary>
        /// 检查指定槽位是否存在保存数据。
        /// </summary>
        /// <param name="slotId">保存槽位编号。</param>
        /// <returns>存在保存数据时返回 true。</returns>
        bool Exists(int slotId);

        /// <summary>
        /// 写入指定槽位的保存数据字节。
        /// </summary>
        /// <param name="slotId">保存槽位编号。</param>
        /// <param name="bytes">保存文件完整字节。</param>
        void Write(int slotId, byte[] bytes);

        /// <summary>
        /// 读取指定槽位的保存数据字节。
        /// </summary>
        /// <param name="slotId">保存槽位编号。</param>
        /// <returns>保存文件完整字节；不存在时由实现决定返回空或抛出异常。</returns>
        byte[] Read(int slotId);

        /// <summary>
        /// 删除指定槽位的保存数据。
        /// </summary>
        /// <param name="slotId">保存槽位编号。</param>
        /// <returns>删除成功或槽位不存在时返回 true。</returns>
        bool Delete(int slotId);

        /// <summary>
        /// 获取当前存在保存数据的槽位编号。
        /// </summary>
        /// <returns>保存槽位编号列表。</returns>
        IReadOnlyList<int> GetSlotIds();

        /// <summary>
        /// 清空全部保存槽位。
        /// </summary>
        void Clear();
    }
}
