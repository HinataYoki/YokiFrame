#if UNITY_2022_3_OR_NEWER
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 在 Unity 子系统重建时清理 SceneKit 上一会话遗留的静态场景状态。
    /// </summary>
    public static class UnitySceneKitRuntimeInstaller
    {
        /// <summary>
        /// 在进入新 Player 子系统时释放上一会话的场景缓存、已加载列表与激活 Handler。
        /// </summary>
        /// <remarks>
        /// 必要性：关闭 Domain Reload（Enter Play Mode Options）后静态字段会跨 Play 会话存活，
        /// 而 <c>sSceneCache</c>、<c>sLoadedScenes</c> 与 <c>sActiveSceneHandler</c> 会继续引用上一会话的
        /// <see cref="SceneHandler"/> 与已经失效的宿主场景对象 —— <see cref="SceneKit.GetActiveScene"/> 会返回陈旧句柄、
        /// <see cref="SceneKit.GetLoadedScenes"/> 会列出实际并不存在的场景，卸载路径也可能触达已失效的后端。
        /// <para>
        /// <see cref="SceneKit.Reset"/> 本来就负责清理这些状态，但此前没有任何宿主调用方；
        /// 本适配器按宿主边界规则（Tools 的宿主接入只放在 Adapters）补上该调用点。
        /// </para>
        /// <para>
        /// 注意：<c>sDefaultProvider</c> / <c>sDefaultBackend</c> 无需在此处理，
        /// <c>SceneKit.ResolveDefaultBackend</c> 已用 Provider 引用比对自行失效缓存。
        /// </para>
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSceneKitState()
        {
            SceneKit.Reset();
        }
    }
}
#endif
