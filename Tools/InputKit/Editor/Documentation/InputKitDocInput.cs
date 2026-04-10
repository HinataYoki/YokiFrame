#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// InputKit 输入读取文档
    /// </summary>
    internal static class InputKitDocInput
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "输入读取",
                Description = "类型安全的输入 API，编译时检查，零魔法字符串。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "输入读取",
                        Code = @"var input = InputKit.Get<GameAppInput>();

// 值读取
var move = input.Player.Move.ReadValue<Vector2>();

// 按钮状态
bool pressed = input.Player.Attack.IsPressed();

// 事件订阅
input.Player.Attack.performed += ctx => Attack();",
                        Explanation = "通过生成的类直接访问 Action。"
                    },
                    new()
                    {
                        Title = "设备检测",
                        Code = @"// 当前设备
InputDeviceType device = InputKit.CurrentDeviceType;
bool isGamepad = InputKit.IsUsingGamepad;
bool isKeyboard = InputKit.IsUsingKeyboardMouse;

// 设备切换事件
InputKit.OnDeviceChanged += static device =>
{
    UIManager.Instance.RefreshPrompts(device);
};",
                        Explanation = "设备切换时自动触发事件。"
                    },
                    new()
                    {
                        Title = "ActionMap 管理",
                        Code = @"InputKit.SwitchActionMap(""UI"");           // 切换（禁用其他）
InputKit.EnableActionMaps(""Player"", ""Camera""); // 同时启用多个
InputKit.DisableAllActionMaps();
InputKit.EnableAllActionMaps();",
                        Explanation = "用于切换游戏状态（战斗/UI/对话）。"
                    },
                    new()
                    {
                        Title = "绑定显示",
                        Code = @"// 获取显示名称
string display = InputKit.GetBindingDisplayString(input.Player.Attack);
// ""Space"" 或 ""Button South""

// 按控制方案获取
string kb = InputKit.GetBindingDisplayString(action, ""Keyboard&Mouse"");
string gp = InputKit.GetBindingDisplayString(action, ""Gamepad"");",
                        Explanation = "返回本地化按键名称，用于 UI 显示。"
                    }
                }
            };
        }
    }
}
#endif
