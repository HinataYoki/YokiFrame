#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// InputKit 连招/组合键系统文档
    /// </summary>
    internal static class InputKitDocCombo
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "连招/组合键系统",
                Description = "InputKit 提供灵活的连招检测系统，支持格斗游戏风格的输入序列、方向指令和组合键。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "注册连招",
                        Code = @"// 简单连招：轻攻击 → 轻攻击 → 重攻击
InputKit.RegisterCombo(""BasicCombo"",
    new ComboStep(""LightAttack"", ComboInputType.Tap),
    new ComboStep(""LightAttack"", ComboInputType.Tap),
    new ComboStep(""HeavyAttack"", ComboInputType.Tap)
);

// 指定窗口时长（步骤间最大间隔）
InputKit.RegisterCombo(""QuickCombo"", 0.2f,
    new ComboStep(""Attack"", ComboInputType.Tap),
    new ComboStep(""Attack"", ComboInputType.Tap),
    new ComboStep(""Special"", ComboInputType.Tap)
);

// 使用 ComboDefinition 配置
var combo = new ComboDefinition
{
    Id = ""HadoukenCombo"",
    WindowBetweenSteps = 0.3f,
    Steps = new[]
    {
        new ComboStep(""Down"", ComboInputType.Direction),
        new ComboStep(""DownForward"", ComboInputType.Direction),
        new ComboStep(""Forward"", ComboInputType.Direction),
        new ComboStep(""Punch"", ComboInputType.Tap)
    }
};
InputKit.RegisterCombo(combo);",
                        Explanation = "连招由多个 ComboStep 组成，支持 Tap（按下）、Release（释放）、Direction（方向）等输入类型。"
                    },
                    new()
                    {
                        Title = "监听连招事件",
                        Code = @"// 连招成功触发
InputKit.OnComboTriggered += comboId =>
{
    switch (comboId)
    {
        case ""BasicCombo"":
            Player.ExecuteBasicCombo();
            break;
        case ""HadoukenCombo"":
            Player.CastHadouken();
            break;
    }
};

// 连招进度（用于 UI 提示）
InputKit.OnComboProgress += (comboId, current, total) =>
{
    ComboUI.ShowProgress(comboId, current, total);
};

// 连招失败/超时
InputKit.OnComboFailed += comboId =>
{
    ComboUI.HideProgress(comboId);
};",
                        Explanation = "三个事件覆盖连招的完整生命周期：触发、进度、失败。"
                    },
                    new()
                    {
                        Title = "处理输入",
                        Code = @"// 在输入回调中处理连招输入
void OnAttackPressed()
{
    InputKit.ProcessComboTap(""Attack"");
}

void OnAttackReleased()
{
    InputKit.ProcessComboRelease(""Attack"");
}

// 处理方向输入
void OnMoveInput(Vector2 direction)
{
    InputKit.ProcessComboDirection(""Move"", direction);
}

// 在 Update 中更新连招系统（检查超时）
void Update()
{
    InputKit.UpdateCombo();
}",
                        Explanation = "需要手动调用 ProcessComboTap/Release/Direction 传递输入，并在 Update 中调用 UpdateCombo 检查超时。"
                    },
                    new()
                    {
                        Title = "管理连招",
                        Code = @"// 注销单个连招
InputKit.UnregisterCombo(""BasicCombo"");

// 清空所有连招
InputKit.ClearAllCombos();",
                        Explanation = "可以动态注册/注销连招，适用于角色解锁新技能等场景。"
                    }
                }
            };
        }
    }
}
#endif
