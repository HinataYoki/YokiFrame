namespace YokiFrame
{
    /// <summary>
    /// ManagedRuntimeKit 工作流动作标识。宿主后端用这些动作对齐工作台体验。
    /// </summary>
    public static class ManagedRuntimeActionIds
    {
        public const string EnableBackend = "enable_backend";
        public const string InstallLocalIl2Cpp = "install_local_il2cpp";
        public const string GenerateAotRules = "generate_aot_rules";
        public const string CompileDll = "compile_dll";
        public const string BuildPlayer = "build_player";
        public const string RunSetupPipeline = "run_setup_pipeline";
        public const string OpenBackendSettings = "open_backend_settings";
    }
}
