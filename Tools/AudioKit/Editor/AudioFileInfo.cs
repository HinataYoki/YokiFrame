#if UNITY_EDITOR
namespace YokiFrame
{
    /// <summary>
    /// 音频文件信息
    /// </summary>
    internal struct AudioFileInfo
    {
        /// <summary>
        /// 音频 ID
        /// </summary>
        public int Id;

        /// <summary>
        /// 文件名（不含扩展名）
        /// </summary>
        public string Name;

        /// <summary>
        /// 相对路径（不含扩展名）
        /// </summary>
        public string Path;

        /// <summary>
        /// 生成的常量名
        /// </summary>
        public string ConstantName;

        /// <summary>
        /// 文件夹分类
        /// </summary>
        public string FolderCategory;
    }
}
#endif
