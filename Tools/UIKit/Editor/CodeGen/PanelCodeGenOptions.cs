#if UNITY_EDITOR
namespace YokiFrame
{
    /// <summary>
    /// 面板代码生成选项
    /// </summary>
    public class PanelCodeGenOptions
    {
        /// <summary>
        /// UI 层级
        /// </summary>
        public UILevel Level { get; set; } = UILevel.Common;

        /// <summary>
        /// 是否为模态面板
        /// </summary>
        public bool IsModal { get; set; }

        /// <summary>
        /// 是否生成生命周期钩子
        /// </summary>
        public bool GenerateLifecycleHooks { get; set; } = true;

        /// <summary>
        /// 是否生成焦点支持
        /// </summary>
        public bool GenerateFocusSupport { get; set; }

        /// <summary>
        /// 显示动画类型
        /// </summary>
        public string ShowAnimationType { get; set; }

        /// <summary>
        /// 隐藏动画类型
        /// </summary>
        public string HideAnimationType { get; set; }

        /// <summary>
        /// 动画时长
        /// </summary>
        public float AnimationDuration { get; set; } = 0.3f;
    }
}
#endif
