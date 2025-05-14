using UnityEngine;

namespace YokiFrame
{
    public interface IBind // TODO  UIKit 绑定的时候支持 
    {
        string TypeName { get; }

        string Comment { get; }

        Transform Transform { get; }

        BindType GetBindType();
    }
}