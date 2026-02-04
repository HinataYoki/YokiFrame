namespace YokiFrame.ECS
{
    /// <summary>
    /// 移动速度组件
    /// </summary>
    public struct MoveSpeed : IComponentData
    {
        public float Value;
        public float BaseValue;
        public float Multiplier;
        
        public MoveSpeed(float speed)
        {
            Value = speed;
            BaseValue = speed;
            Multiplier = 1f;
        }
        
        public void SetMultiplier(float multiplier)
        {
            Multiplier = multiplier;
            Value = BaseValue * Multiplier;
        }
        
        public void ResetMultiplier()
        {
            Multiplier = 1f;
            Value = BaseValue;
        }
    }
}
