#if !GODOT
using System;

namespace YokiFrame
{
    public readonly struct UIOpenRequest
    {
        public UIOpenRequest(Type panelType, UILevel level, IUIData data, string tag)
        {
            PanelType = panelType;
            Level = level;
            Data = data;
            Tag = tag;
        }

        public Type PanelType { get; }

        public UILevel Level { get; }

        public IUIData Data { get; }

        public string Tag { get; }
    }
}
#endif
