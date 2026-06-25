#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using System;
using UnityEngine.UIElements;
using YokiFrame.Unity;

namespace YokiFrame.Unity
{
    /// <summary>
    /// TableKit 工具页面 (YokiFrame 集成版)
    /// 嵌入 YokiFrame 工具面板中使用
    /// </summary>
    [YokiToolPage(
        kit: "TableKit",
        name: "TableKit",
        icon: KitIcons.TABLEKIT,
        priority: 50,
        category: YokiPageCategory.Tool)]
    public class TableKitToolPage : YokiToolPageBase
    {

        private TableKitEditorUI mEditorUI;

        /// <summary>
        /// 构建 TableKit 工具页 UI。
        /// </summary>
        /// <param name="root">工具页根节点。</param>
        protected override void BuildUI(VisualElement root)
        {
            mEditorUI = new TableKitEditorUI();
            root.Add(mEditorUI.BuildUI());
        }

        /// <summary>
        /// 停用工具页时保存用户配置。
        /// </summary>
        public override void OnDeactivate()
        {
            mEditorUI?.SavePrefs();
        }

        /// <summary>
        /// 在编辑器播放时刷新 TableKit 状态。
        /// </summary>
        [Obsolete("保留用于状态刷新")]
        public override void OnUpdate()
        {
            if (IsPlaying)
            {
                mEditorUI?.RefreshStatus();
            }
        }
    }
}
#endif
