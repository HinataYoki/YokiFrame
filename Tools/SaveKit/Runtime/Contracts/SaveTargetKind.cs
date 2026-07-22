namespace YokiFrame
{
    /// <summary>
    /// 标识保存数据属于槽位还是命名的非槽位文档。
    /// </summary>
    public enum SaveTargetKind
    {
        /// <summary>玩家可选择的数字槽位。</summary>
        Slot = 0,

        /// <summary>与槽位无关的命名持久化文档。</summary>
        Global = 1
    }
}
