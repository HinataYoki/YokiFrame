namespace YokiFrame
{
    /// <summary>
    /// Buff 实例序列化数据
    /// </summary>
    public struct BuffInstanceSaveData
    {
        public int BuffId;
        public float RemainingDuration;
        public int StackCount;
        public float ElapsedTickTime;
    }

    /// <summary>
    /// Buff 容器序列化数据
    /// </summary>
    public struct BuffContainerSaveData
    {
        public BuffInstanceSaveData[] Instances;
        public int[] ImmuneTags;
    }
}
