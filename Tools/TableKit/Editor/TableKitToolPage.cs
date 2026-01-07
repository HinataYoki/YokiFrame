#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using UnityEngine.UIElements;
using YokiFrame.EditorTools;
using YokiFrame.TableKit.Editor;

namespace YokiFrame.Editor
{
    /// <summary>
    /// TableKit 工具页面 (YokiFrame 集成版)
    /// 嵌入 YokiFrame 工具面板中使用
    /// </summary>
    public class TableKitToolPage : YokiFrameToolPageBase
    {
        public override string PageName => "TableKit";
        public override int Priority => 50;

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
