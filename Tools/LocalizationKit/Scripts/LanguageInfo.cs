namespace YokiFrame
{
    /// <summary>
    /// 语言元数据结构体
    /// 使用 readonly struct 保持轻量（小于16字节原则）
    /// </summary>
    public readonly struct LanguageInfo
    {
        /// <summary>语言标识符</summary>
        public readonly LanguageId Id;
        
        /// <summary>显示名称的 TextId（如"中文"）</summary>
        public readonly int DisplayNameTextId;
        
        /// <summary>原生名称的 TextId（如"简体中文"）</summary>
        public readonly int NativeNameTextId;
        
        /// <summary>语言图标资源 ID</summary>
        public readonly int IconSpriteId;

        public LanguageInfo(LanguageId id, int displayNameTextId, int nativeNameTextId, int iconSpriteId)
        {
            Id = id;
            DisplayNameTextId = displayNameTextId;
            NativeNameTextId = nativeNameTextId;
            IconSpriteId = iconSpriteId;
        }

        /// <summary>
        /// 创建一个空的语言信息（用于表示无效/未找到）
        /// </summary>
        public static LanguageInfo Empty => new LanguageInfo(default, 0, 0, 0);

        /// <summary>
        /// 检查是否为有效的语言信息
        /// </summary>
        public bool IsValid => DisplayNameTextId != 0 || NativeNameTextId != 0;

        public override string ToString()
        {
            return $"LanguageInfo({Id}, DisplayName={DisplayNameTextId}, NativeName={NativeNameTextId})";
        }
    }
}
