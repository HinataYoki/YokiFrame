#if UNITY_EDITOR
namespace YokiFrame
{
    internal sealed partial class UIKitBindingTreeView
    {
        /// <summary>返回 BindType 的稳定符号，供层级项和图例复用。</summary>
        private static string GetMarker(BindType bindType)
        {
            switch (bindType)
            {
                case BindType.Member:
                    return "◇";
                case BindType.Element:
                    return "●";
                case BindType.Component:
                    return "◆";
                default:
                    return "○";
            }
        }

        /// <summary>从完整类型名提取层级显示短名称。</summary>
        private static string ShortTypeName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
                return string.Empty;
            int index = fullName.LastIndexOf('.');
            return index >= 0 && index < fullName.Length - 1
                ? fullName.Substring(index + 1)
                : fullName;
        }
    }
}
#endif
