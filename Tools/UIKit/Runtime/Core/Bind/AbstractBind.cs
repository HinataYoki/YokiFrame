using UnityEngine;

namespace YokiFrame
{
    public abstract class AbstractBind : MonoBehaviour, IBind
    {
        public BindType bind = BindType.Member;
        public string mName;
        public string autoType;
        public string customType;
        public string type;
        public string comment;


        public BindType Bind => bind;
        public string Name => mName;
        public string Type => type;
        public string Comment => comment;
        public Transform Transform => transform;
    }
}