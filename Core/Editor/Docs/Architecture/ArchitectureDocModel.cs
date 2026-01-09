#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Architecture 实现数据模型文档
    /// </summary>
    internal static class ArchitectureDocModel
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "实现数据模型",
                Description = "继承 AbstractModel 实现数据模型，用于存储游戏状态数据。IModel 继承 ISerializable，支持与 SaveKit 集成。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "数据模型示例",
                        Code = @"public class PlayerModel : AbstractModel
{
    public int Level = 1;
    public int Exp = 0;
    public int Gold = 0;
    public List<int> UnlockedSkills = new();
    
    protected override void OnInit()
    {
        // 可以在这里加载初始数据
    }
    
    // 实现 ISerializable（SaveKit 集成需要）
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        info.AddValue(""Level"", Level);
        info.AddValue(""Exp"", Exp);
        info.AddValue(""Gold"", Gold);
    }
}",
                        Explanation = "数据模型与业务逻辑分离，便于存档和测试。"
                    }
                }
            };
        }
    }
}
#endif
