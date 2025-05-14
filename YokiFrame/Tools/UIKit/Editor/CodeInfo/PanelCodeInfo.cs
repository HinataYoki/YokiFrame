using System.Collections.Generic;

namespace YokiFrame
{
    public class PanelCodeInfo
    {
        public string GameObjectName;
        public Dictionary<string, string> DicNameToFullName = new();
        public readonly List<BindInfo> BindInfos = new();
        public readonly List<ElementCodeInfo> ElementCodeDatas = new();

        public string Identifier { get; set; }
        public bool Changed { get; set; }
        public IEnumerable<string> ForeignKeys { get; private set; }
    }
}
