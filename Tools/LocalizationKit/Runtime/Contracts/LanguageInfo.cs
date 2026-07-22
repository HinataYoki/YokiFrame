namespace YokiFrame
{
    /// <summary>保存语言显示名称和图标资源的文本编号。</summary>
    public readonly struct LanguageInfo
    {
        /// <summary>语言标识。</summary>
        public readonly LanguageId Id;
        /// <summary>显示名称文本编号。</summary>
        public readonly int DisplayNameTextId;
        /// <summary>原生名称文本编号。</summary>
        public readonly int NativeNameTextId;
        /// <summary>图标资源编号。</summary>
        public readonly int IconSpriteId;

        /// <summary>创建语言显示信息。</summary>
        public LanguageInfo(LanguageId id, int displayNameTextId, int nativeNameTextId, int iconSpriteId)
        {
            Id = id;
            DisplayNameTextId = displayNameTextId;
            NativeNameTextId = nativeNameTextId;
            IconSpriteId = iconSpriteId;
        }

        /// <summary>获取未配置的语言信息。</summary>
        public static LanguageInfo Empty => new LanguageInfo(default(LanguageId), 0, 0, 0);

        /// <summary>判断是否至少配置了一个显示资源编号。</summary>
        public bool IsValid => DisplayNameTextId != 0 || NativeNameTextId != 0 || IconSpriteId != 0;

        /// <inheritdoc />
        public override string ToString() =>
            "LanguageInfo(" + Id + ", DisplayName=" + DisplayNameTextId + ", NativeName=" + NativeNameTextId + ")";
    }
}
