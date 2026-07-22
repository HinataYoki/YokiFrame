#if UNITY_2022_3_OR_NEWER && UNITY_EDITOR
namespace YokiFrame
{
    internal sealed partial class UIKitController
    {
        /// <summary>
        /// Editor 构建提交一次可观察状态变化；Player 中该 partial 调用被完全移除。
        /// </summary>
        partial void OnStateChanged()
        {
            UIKit.AdvanceDiagnosticVersion();
        }
    }
}
#endif
