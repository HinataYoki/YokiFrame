namespace YokiFrame
{
    /// <summary>
    /// 表示语言显示名称、原生名称和图标资源信息。
    /// </summary>
    public readonly struct LanguageInfo
    {
        /// <summary>
        /// 语言标识。
        /// </summary>
        public readonly LanguageId Id;

        /// <summary>
        /// 显示名称文本编号。
        /// </summary>
        public readonly int DisplayNameTextId;

        /// <summary>
        /// 原生名称文本编号。
        /// </summary>
        public readonly int NativeNameTextId;

        /// <summary>
        /// 图标资源编号。
        /// </summary>
        public readonly int IconSpriteId;

        /// <summary>
        /// 创建语言信息。
        /// </summary>
        /// <param name="id">语言标识。</param>
        /// <param name="displayNameTextId">显示名称文本编号。</param>
        /// <param name="nativeNameTextId">原生名称文本编号。</param>
        /// <param name="iconSpriteId">图标资源编号。</param>
        public LanguageInfo(LanguageId id, int displayNameTextId, int nativeNameTextId, int iconSpriteId)
        {
            Id = id;
            DisplayNameTextId = displayNameTextId;
            NativeNameTextId = nativeNameTextId;
            IconSpriteId = iconSpriteId;
        }

        /// <summary>
        /// 获取空语言信息。
        /// </summary>
        public static LanguageInfo Empty
        {
            get { return new(default(LanguageId), 0, 0, 0); }
        }

        /// <summary>
        /// 获取语言信息是否包含有效显示数据。
        /// </summary>
        public bool IsValid
        {
            get { return DisplayNameTextId != 0 || NativeNameTextId != 0 || IconSpriteId != 0; }
        }

        /// <summary>
        /// 返回语言信息的调试字符串。
        /// </summary>
        /// <returns>调试字符串。</returns>
        public override string ToString()
        {
            return "LanguageInfo(" + Id + ", DisplayName=" + DisplayNameTextId + ", NativeName=" + NativeNameTextId + ")";
        }
    }
}
