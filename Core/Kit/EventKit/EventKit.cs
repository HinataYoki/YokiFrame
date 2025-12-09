namespace YokiFrame
{
    public class EventKit
    {
        public readonly static TypeEvent Type = new();
        public readonly static EnumEvent Enum = new();
        [System.Obsolete]
        public readonly static StringEvent String = new();
    }
}