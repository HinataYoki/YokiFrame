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
                Description = "类型安全的输入 API，基于 InputSystem 生成的 C# 类，编译时类型检查，零魔法字符串。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "类型安全输入读取",
                        Code = @"public class PlayerController : MonoBehaviour
{
    private GameAppInput mInput;
    
    void OnEnable()
    {
        // 获取类型安全的输入实例
        mInput = InputKit.Get<GameAppInput>();
        
        // 订阅事件（类型安全）
        mInput.Player.Attack.performed += OnAttack;
        mInput.Player.Dodge.performed += OnDodge;
    }
    
    void OnDisable()
    {
        mInput.Player.Attack.performed -= OnAttack;
        mInput.Player.Dodge.performed -= OnDodge;
    }
    
    void Update()
    {
        // 类型安全的值读取
        var move = mInput.Player.Move.ReadValue<Vector2>();
        transform.Translate(move * Time.deltaTime * 5f);
        
        // 按钮状态检测
        if (mInput.Player.Sprint.IsPressed())
        {
            // 冲刺中
        }
    }
    
    private void OnAttack(InputAction.CallbackContext ctx) => ExecuteAttack();
    private void OnDodge(InputAction.CallbackContext ctx) => ExecuteDodge();
}",
                        Explanation = "通过 InputSystem 生成的类直接访问 Action，编译时类型检查，无运行时字符串查找。"
                    },
                    new()
                    {
                        Title = "设备检测",
                        Code = @"// 当前设备类型
InputDeviceType device = InputKit.CurrentDeviceType;

// 便捷属性检测
if (InputKit.IsUsingKeyboardMouse)
{
    ShowPrompt(""按 E 交互"");
}
else if (InputKit.IsUsingGamepad)
{
    ShowPrompt(""按 A 交互"");
}
else if (InputKit.IsUsingTouch)
{
    ShowTouchButton();
}

// 检测手柄连接状态
if (InputKit.IsGamepadConnected)
{
    EnableGamepadUI();
}

// 监听设备切换事件
InputKit.OnDeviceChanged += static deviceType =>
{
    UIManager.Instance.RefreshInputPrompts(deviceType);
};",
                        Explanation = "设备切换时自动触发事件，用于动态更新 UI 提示图标。"
                    },
                    new()
                    {
                        Title = "ActionMap 管理",
                        Code = @"// 切换 ActionMap（禁用其他，启用指定）
InputKit.SwitchActionMap(""UI"");

// 同时启用多个 ActionMap
InputKit.EnableActionMaps(""Player"", ""Camera"");

// 禁用所有 ActionMap
InputKit.DisableAllActionMaps();

// 启用所有 ActionMap
InputKit.EnableAllActionMaps();

// 获取当前启用的 ActionMap
var enabledMaps = InputKit.GetEnabledActionMaps();

// 备用字符串 API（不推荐，仅用于动态场景）
var action = InputKit.FindAction(""Player/Attack"");
var map = InputKit.FindActionMap(""Player"");",
                        Explanation = "ActionMap 管理用于切换游戏状态（如战斗/UI/对话）。"
                    },
                    new()
                    {
                        Title = "绑定显示名称",
                        Code = @"var input = InputKit.Get<GameAppInput>();

// 获取绑定显示名称（类型安全）
string display = InputKit.GetBindingDisplayString(input.Player.Attack);
// 输出: ""Space"" 或 ""Button South""

// 获取指定控制方案的显示名称
string kbDisplay = InputKit.GetBindingDisplayString(
    input.Player.Attack, 
    ""Keyboard&Mouse"");

string gpDisplay = InputKit.GetBindingDisplayString(
    input.Player.Attack, 
    ""Gamepad"");

// 用于 UI 显示
mAttackPrompt.text = $""攻击: {display}"";",
                        Explanation = "GetBindingDisplayString 返回本地化的按键名称，适合 UI 显示。"
                    }
                }
            };
        }
    }
}
#endif
