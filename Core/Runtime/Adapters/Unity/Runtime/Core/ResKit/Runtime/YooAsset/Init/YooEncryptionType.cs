#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT
namespace YokiFrame.Unity
{
    /// <summary>
    /// YooAsset 资源加密类型。
    /// </summary>
    public enum YooEncryptionType
    {
        /// <summary>
        /// 不启用资源加密。
        /// </summary>
        None,

        /// <summary>
        /// 使用 XOR 流式加密。
        /// </summary>
        XorStream,

        /// <summary>
        /// 使用文件偏移加密。
        /// </summary>
        FileOffset,

        /// <summary>
        /// 使用 AES 加密。
        /// </summary>
        Aes,

        /// <summary>
        /// 使用自定义加密。
        /// </summary>
        Custom
    }
}
#endif
#endif
