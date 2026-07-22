namespace YokiFrame
{
    /// <summary>为诊断工具声明 Provider 的可选能力，不参与资源加载热路径。</summary>
    public interface IResourceProviderCapabilities
    {
        /// <summary>获取当前 Provider 是否支持 raw bytes。</summary>
        bool SupportsRawBytes { get; }

        /// <summary>获取当前 Provider 是否支持 raw 文本。</summary>
        bool SupportsRawText { get; }

    }
}
