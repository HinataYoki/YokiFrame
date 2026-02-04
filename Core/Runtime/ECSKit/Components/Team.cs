namespace YokiFrame.ECS
{
    /// <summary>
    /// 阵营组件 - 用于区分敌我
    /// </summary>
    public struct Team : IComponentData
    {
        public int Id;
        
        public Team(int id)
        {
            Id = id;
        }
        
        public bool IsSameTeam(Team other) => Id == other.Id;
        public bool IsEnemy(Team other) => Id != other.Id;
        
        public static Team Player => new Team(0);
        public static Team Enemy => new Team(1);
        public static Team Neutral => new Team(2);
    }
}
