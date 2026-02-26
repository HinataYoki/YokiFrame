#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// InputKit 连招系统文档
    /// </summary>
    internal static class InputKitDocCombo
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "连招系统",
                Description = "支持格斗游戏风格的输入序列检测。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "注册连招",
                        Code = @"// 简单连招
InputKit.RegisterCombo(""BasicCombo"",
    new ComboStep(""Attack"", ComboInputType.Tap),
    new ComboStep(""Attack"", ComboInputType.Tap),
    new ComboStep(""Special"", ComboInputType.Tap)
);

// 指定窗口时长
InputKit.RegisterCombo(""QuickCombo"", 0.2f,
    new ComboStep(""Attack"", ComboInputType.Tap),
    new ComboStep(""Attack"", ComboInputType.Tap)
);

// 方向指令
InputKit.RegisterCombo(""Hadouken"", 0.3f,
    new ComboStep(""Down"", ComboInputType.Direction),
    new ComboStep(""Forward"", ComboInputType.Direction),
    new ComboStep(""Punch"", ComboInputType.Tap)
);",
                        Explanation = "ComboStep 支持 Tap、Release、Direction 输入类型。"
                    },
                    new()
                    {
                        Title = "处理输入",
                        Code = @"// 在输入回调中
void OnAttackPressed() => InputKit.ProcessComboTap(""Attack"");
void OnAttackReleased() => InputKit.ProcessComboRelease(""Attack"");
void OnMove(Vector2 dir) => InputKit.ProcessComboDirection(""Move"", dir);

// Update 中更新（检查超时）
void Update() => InputKit.UpdateCombo();",
                        Explanation = "需手动传递输入，并每帧调用 UpdateCombo。"
                    },
                    new()
                    {
                        Title = "监听事件",
                        Code = @"// 连招触发
InputKit.OnComboTriggered += comboId =>
{
    if (comboId == ""Hadouken"") Player.CastHadouken();
};

// 进度（用于 UI）
InputKit.OnComboProgress += (id, current, total) =>
{
    ComboUI.ShowProgress(id, current, total);
};

// 失败/超时
InputKit.OnComboFailed += id => ComboUI.Hide(id);

// 管理
InputKit.UnregisterCombo(""BasicCombo"");
InputKit.ClearAllCombos();",
                        Explanation = "三个事件覆盖连招完整生命周期。"
                    }
                }
            };
        }
    }
}
#endif
