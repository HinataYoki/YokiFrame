namespace YokiFrame
{
    /// <summary>
    /// 描述 CodeGenKit 对目标文件执行事务提交后的结果。
    /// </summary>
    public enum CodeGenerationFileResult
    {
        /// <summary>目标文件原本不存在，本次已创建。</summary>
        Created,

        /// <summary>目标文件内容发生变化，本次已原子更新。</summary>
        Updated,

        /// <summary>目标文件与生成内容一致，本次未触碰文件。</summary>
        Unchanged
    }
}
