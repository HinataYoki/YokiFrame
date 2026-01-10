using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 原始文件加载器接口（用于加载非 Unity 资源的原始文件）
    /// </summary>
    public interface IRawFileLoader
    {
        /// <summary>
        /// 同步加载原始文件文本
        /// </summary>
        string LoadRawFileText(string path);

        /// <summary>
        /// 同步加载原始文件字节数据
        /// </summary>
        byte[] LoadRawFileData(string path);

        /// <summary>
        /// 异步加载原始文件文本
        /// </summary>
        void LoadRawFileTextAsync(string path, Action<string> onComplete);

        /// <summary>
        /// 异步加载原始文件字节数据
        /// </summary>
        void LoadRawFileDataAsync(string path, Action<byte[]> onComplete);

        /// <summary>
        /// 获取原始文件的完整路径（用于需要直接访问文件的场景）
        /// </summary>
        string GetRawFilePath(string path);

        /// <summary>
        /// 卸载并回收加载器
        /// </summary>
        void UnloadAndRecycle();
    }

    /// <summary>
    /// 原始文件加载池接口
    /// </summary>
    public interface IRawFileLoaderPool
    {
        IRawFileLoader Allocate();
        void Recycle(IRawFileLoader loader);
    }

    /// <summary>
    /// 抽象原始文件加载池基类
    /// </summary>
    public abstract class AbstractRawFileLoaderPool : IRawFileLoaderPool
    {
        private readonly Stack<IRawFileLoader> mPool = new();

        public IRawFileLoader Allocate() => mPool.Count > 0 ? mPool.Pop() : CreateLoader();
        public void Recycle(IRawFileLoader loader) => mPool.Push(loader);

        protected abstract IRawFileLoader CreateLoader();
    }
}
