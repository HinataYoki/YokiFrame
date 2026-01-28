namespace YokiFrame
{
    public class EventKit
    {
        public readonly static TypeEvent Type = new();
        public readonly static EnumEvent Enum = new();
        
        [System.Obsolete("字符串事件已废弃，存在类型安全隐患且重构困难。请迁移到 EventKit.Type（类型事件）或 EventKit.Enum（枚举事件）。详见文档：Assets/YokiFrame/Core/Editor/Docs/EventKit/")]
        public readonly static StringEvent String = new();
    }
}