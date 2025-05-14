using System;

namespace YokiFrame
{
    /// <summary>
    /// 存储一些Mark相关的信息
    /// </summary>
    [Serializable]
    public class BindInfo
    {
        public string TypeName;

        public string PathToRoot;

        public IBind BindScript;

        public string MemberName;
    }
}
