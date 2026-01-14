#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using System;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;
using YokiFrame.TableKit.Editor;

namespace YokiFrame.Editor
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

        protected override void BuildUI(VisualElement root)
        {
            mEditorUI = new TableKitEditorUI();
            root.Add(mEditorUI.BuildUI());
        }

        public override void OnDeactivate()
        {
            mEditorUI?.SavePrefs();
        }

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
