#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// InputKit 快速入门文档
    /// </summary>
    internal static class InputKitDocQuickStart
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "快速入门",
                Description = "基于 Unity InputSystem 的输入管理框架，提供类型安全 API、重绑定、输入缓冲、连招系统。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "三步上手",
                        Code = @"// 1. 创建 InputActionAsset 并勾选 Generate C# Class
// 2. 初始化
InputKit.Register<GameAppInput>();
InputKit.Initialize();

// 3. 使用
var input = InputKit.Get<GameAppInput>();
var move = input.Player.Move.ReadValue<Vector2>();
input.Player.Attack.performed += ctx => Debug.Log(""攻击"");",
                        Explanation = "Register → Initialize → Get，三步完成初始化。"
                    },
                    new()
                    {
                        Title = "核心 API",
                        Code = @"// 生命周期
InputKit.Register<T>();    // 注册
InputKit.Initialize();     // 初始化（自动加载绑定）
InputKit.Dispose();        // 释放

// 输入读取
var input = InputKit.Get<GameAppInput>();
input.Player.Move.ReadValue<Vector2>();
input.Player.Attack.IsPressed();

// 设备检测
InputKit.CurrentDeviceType;
InputKit.IsUsingGamepad;
InputKit.OnDeviceChanged += device => { };

// ActionMap
InputKit.SwitchActionMap(""UI"");
InputKit.EnableActionMaps(""Player"", ""Camera"");

// 重绑定
await InputKit.RebindAsync(action, bindingIndex);
InputKit.GetBindingDisplayString(action);
InputKit.ResetAllBindings();

// 输入缓冲
InputKit.SetBufferWindow(150f);
InputKit.HasBufferedInput(action);
InputKit.ConsumeBufferedInput(action);

// 连招
InputKit.RegisterCombo(""Combo"", steps);
InputKit.ProcessComboTap(""Attack"");
InputKit.UpdateCombo();  // Update 中调用

// 震动
InputKit.PlayHaptic(HapticPreset.Medium);
InputKit.UpdateHaptic(); // Update 中调用

// 上下文
InputKit.PushContext(""UI"");
InputKit.PopContext();
InputKit.IsActionBlocked(""Attack"");",
                        Explanation = "所有 API 通过 InputKit.Xxx() 静态调用。"
                    }
                }
            };
        }
    }
}
#endif
