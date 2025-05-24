using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    public abstract class AbstractBind : MonoBehaviour, IBind
    {
        [HideInInspector]
        public BindType customBind = BindType.Member;
        public BindType Bind => customBind;
        [HideInInspector]
        public string customName;
        public string Name => customName;
        [HideInInspector]
        public string autoType;
        [HideInInspector]
        public string customType;
        [HideInInspector]
        public string type;
        public string TypeName => type;
        [HideInInspector]
        public string customComment;
        public string Comment => customComment;

        public Transform Transform => transform;
    }
}