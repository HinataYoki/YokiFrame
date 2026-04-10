#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// InputKit 输入上下文系统文档
    /// </summary>
    internal static class InputKitDocContext
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "输入上下文",
                Description = "基于栈的输入状态管理，支持 UI/对话/过场等场景的输入切换。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基础用法",
                        Code = @"// 注册上下文（GameManager 中）
InputKit.RegisterContext(gameplayContext);
InputKit.RegisterContext(uiContext);

// 压入/弹出
InputKit.PushContext(""UI"");   // 打开 UI
InputKit.PopContext();          // 关闭 UI

// 检查 Action 是否被屏蔽
if (InputKit.IsActionBlocked(""Attack"")) return;

// 监听变更
InputKit.OnContextChanged += (old, current) =>
{
    Cursor.visible = current?.ContextName == ""UI"";
};",
                        Explanation = "栈结构自动管理嵌套 UI 的输入状态。"
                    },
                    new()
                    {
                        Title = "创建上下文",
                        Code = @"// 编辑器创建：Project 右键 → Create → YokiFrame → InputKit → Input Context

// 常用配置：
// Gameplay: Priority=0, EnabledActionMaps=[""Player""]
// UI: Priority=10, EnabledActionMaps=[""UI""], BlockedActions=[""Attack"",""Dodge""]
// Dialog: Priority=20, BlockAllLowerPriority=true
// Cutscene: Priority=100, BlockAllLowerPriority=true",
                        Explanation = "InputContext 是 ScriptableObject，可视化配置。"
                    },
                    new()
                    {
                        Title = "典型场景",
                        Code = @"// UI 系统
public void OpenPanel()
{
    InputKit.PushContext(""UI"");
    panel.SetActive(true);
}
public void ClosePanel()
{
    InputKit.PopContext();
    panel.SetActive(false);
}

// 嵌套 UI（自动处理）
public void ShowItemDetail()
{
    InputKit.PushContext(""UI"");  // 再次 Push
    detailPanel.Show();
}

// 对话系统
public void StartDialog()
{
    InputKit.PushContext(""Dialog"");
}

// 过场动画
public async UniTaskVoid PlayCutscene()
{
    InputKit.PushContext(""Cutscene"");
    await PlayAsync();
    InputKit.PopContext();
}",
                        Explanation = "Push/Pop 配对使用，栈自动恢复上一状态。"
                    },
                    new()
                    {
                        Title = "高级操作",
                        Code = @"// 弹出到指定上下文（跳过中间层）
InputKit.PopToContext(""Gameplay"");

// 清空栈
InputKit.ClearContextStack();

// 查询状态
var current = InputKit.CurrentContext;
int depth = InputKit.ContextDepth;
bool hasUI = InputKit.HasContext(""UI"");",
                        Explanation = "PopToContext 可一次性返回特定状态。"
                    }
                }
            };
        }
    }
}
#endif
