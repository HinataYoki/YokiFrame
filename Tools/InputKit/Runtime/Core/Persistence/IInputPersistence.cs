#if YOKIFRAME_INPUTSYSTEM_SUPPORT
namespace YokiFrame
{
    /// <summary>
    /// 输入绑定持久化接口
    /// </summary>
    public interface IInputPersistence
    {
        /// <summary>
        /// 保存数据
        /// </summary>
        /// <param name="key">存储键</param>
        /// <param name="json">JSON 数据</param>
        void Save(string key, string json);
        
        /// <summary>
        /// 加载数据
        /// </summary>
        /// <param name="key">存储键</param>
        /// <returns>JSON 数据，不存在返回 null</returns>
        string Load(string key);
        
        /// <summary>
        /// 删除数据
        /// </summary>
        /// <param name="key">存储键</param>
        void Delete(string key);
        
        /// <summary>
        /// 检查数据是否存在
        /// </summary>
        /// <param name="key">存储键</param>
        bool Exists(string key);
    }
}

#endif