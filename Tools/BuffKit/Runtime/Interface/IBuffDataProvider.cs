using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// Buff 数据提供者接口，用于从配置表加载 BuffData
    /// </summary>
    public interface IBuffDataProvider
    {
        /// <summary>
        /// 获取指定 ID 的 BuffData
        /// </summary>
        BuffData GetBuffData(int buffId);
        
        /// <summary>
        /// 获取所有 BuffData
        /// </summary>
        IEnumerable<BuffData> GetAllBuffData();
    }
}
