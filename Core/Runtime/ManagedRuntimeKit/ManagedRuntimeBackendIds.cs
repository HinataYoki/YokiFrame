namespace YokiFrame
{
    /// <summary>
    /// ManagedRuntimeKit 内置后端标识。具体宿主或可选包可以注册自己的后端。
    /// </summary>
    public static class ManagedRuntimeBackendIds
    {
        public const string Default = "default";
        public const string LeanClr = "LeanCLR";
        public const string UnityMono = "Unity.Mono";
        public const string UnityIl2Cpp = "Unity.IL2CPP";
        public const string GodotDotNet = "Godot.DotNet";
        public const string CoreClr = "CoreCLR";
    }
}
