#if UNITY_2022_3_OR_NEWER
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 可挂载到 Unity UI 节点的绑定标记组件。
    /// </summary>
    [AddComponentMenu("YokiFrame/UIKit/Bind")]
    [DisallowMultipleComponent]
    public sealed class Bind : AbstractBind
    {
    }
}
#endif
