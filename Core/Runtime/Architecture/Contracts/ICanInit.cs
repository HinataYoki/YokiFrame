using System;

namespace YokiFrame
{
    /// <summary>
    /// 定义可初始化和可释放对象的基础生命周期契约。
    /// </summary>
    public interface ICanInit : IDisposable
    {
        /// <summary>
        /// 获取当前对象是否已经完成初始化。
        /// </summary>
        bool Initialized { get; }

        /// <summary>
        /// 执行一次初始化逻辑；实现应保证重复调用不会重复初始化。
        /// </summary>
        void Init();
    }
}
