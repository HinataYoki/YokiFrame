#if UNITY_2022_3_OR_NEWER && UNITY_EDITOR
using UnityEngine;

namespace YokiFrame
{
    public static partial class UIKit
    {
        private static long sDiagnosticVersion;

        /// <summary>
        /// 获取当前 Editor 会话内的单调可观察状态版本；读取不会创建 Root。
        /// </summary>
        internal static long DiagnosticVersion => sDiagnosticVersion;

        /// <summary>
        /// 推进 Editor 可观察状态版本；达到上限后保持最大值，禁止 Root 重建产生 ABA。
        /// </summary>
        internal static void AdvanceDiagnosticVersion()
        {
            if (sDiagnosticVersion < long.MaxValue) sDiagnosticVersion++;
        }

        /// <summary>
        /// Unity 新子系统会话开始时重置版本；同一会话内 Root 创建和销毁只允许递增。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDiagnosticVersion()
        {
            sDiagnosticVersion = 0;
        }
    }
}
#endif
