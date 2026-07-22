#if UNITY_2022_3_OR_NEWER
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 提供 UIKit 面板的运行状态与受管理操作入口。
    /// </summary>
    public interface IPanel
    {
        /// <summary>获取面板的 Unity Transform。</summary>
        Transform Transform { get; }

        /// <summary>获取面板名称，默认使用具体面板类型名。</summary>
        string PanelName { get; }

        /// <summary>获取面板当前所属的主层级。</summary>
        UILevel Level { get; }

        /// <summary>获取面板在主层级内的子层级。</summary>
        int SubLevel { get; }

        /// <summary>获取当前打开轮次关联的业务标签。</summary>
        string Tag { get; }

        /// <summary>获取或替换当前打开轮次关联的面板数据；赋值不会重新触发生命周期。</summary>
        IUIData Data { get; set; }

        /// <summary>获取面板当前生命周期状态。</summary>
        PanelState State { get; }

        /// <summary>获取面板关闭后的缓存策略。</summary>
        PanelCachePolicy CachePolicy { get; }

        /// <summary>获取面板当前是否作为模态面板显示。</summary>
        bool IsModal { get; }

        /// <summary>获取面板所属的命名栈；未入栈时返回空值。</summary>
        string StackName { get; }

        /// <summary>
        /// 请求 UIKit 显示当前面板；操作由 UIKit 状态机统一调度。
        /// </summary>
        void Show();

        /// <summary>
        /// 请求 UIKit 隐藏当前面板；操作由 UIKit 状态机统一调度。
        /// </summary>
        void Hide();

        /// <summary>
        /// 请求 UIKit 关闭当前面板；操作由 UIKit 状态机统一调度。
        /// </summary>
        void Close();
    }
}
#endif
