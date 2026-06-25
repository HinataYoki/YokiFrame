namespace YokiFrame
{
    public class EventKit
    {
        public static readonly TypeEvent Type = new();
        public static readonly EnumEvent Enum = new();

        [System.Obsolete("StringEvent is deprecated due to type-safety issues. Use EventKit.Type or EventKit.Enum instead.")]
        public static readonly StringEvent String = new();
    }
}
