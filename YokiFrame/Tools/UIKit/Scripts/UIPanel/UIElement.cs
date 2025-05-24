using UnityEngine;

namespace YokiFrame
{
    public abstract class UIElement : MonoBehaviour, IBind
    {
        public virtual BindType Bind => BindType.Element;

        public virtual string TypeName => GetType().Name;

        public virtual string Name => transform.name;

        public virtual string Comment => string.Empty;

        public Transform Transform => transform;
    }
}