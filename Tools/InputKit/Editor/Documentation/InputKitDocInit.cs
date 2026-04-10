#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// InputKit 初始化与配置文档
    /// </summary>
    internal static class InputKitDocInit
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "初始化与配置",
                Description = "基于 Unity InputSystem 生成的 C# 类，提供类型安全的输入访问。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "前置准备",
                        Code = @"// 1. Project 右键 → Create → Input Actions
// 2. 双击打开，创建 ActionMap 和 Action
// 3. Inspector 勾选 Generate C# Class → Apply
// 4. Unity 自动生成 GameAppInput.cs",
                        Explanation = "InputKit 依赖 InputSystem 生成的 C# 类。"
                    },
                    new()
                    {
                        Title = "初始化",
                        Code = @"// 注册并初始化
InputKit.Register<GameAppInput>();
InputKit.Initialize();  // 自动加载保存的绑定

// 获取输入实例
var input = InputKit.Get<GameAppInput>();

// 释放
InputKit.Dispose();",
                        Explanation = "Register → Initialize → Get，完成初始化流程。"
                    },
                    new()
                    {
                        Title = "GameManager 集成",
                        Code = @"public class GameManager : MonoBehaviour
{
    void Awake()
    {
        InputKit.Register<GameAppInput>();
        InputKit.Initialize();
        InputKit.OnDeviceChanged += OnDeviceChanged;
    }
    
    void Update()
    {
        // 按需调用（使用对应功能时）
        InputKit.UpdateCombo();    // 连招
        InputKit.UpdateHaptic();   // 震动
        InputKit.CleanupBuffer();  // 缓冲清理
    }
    
    void OnDestroy()
    {
        InputKit.OnDeviceChanged -= OnDeviceChanged;
        InputKit.Dispose();
    }
    
    private static void OnDeviceChanged(InputDeviceType device)
        => UIManager.Instance.RefreshInputPrompts(device);
}",
                        Explanation = "在 GameManager 中统一管理生命周期。"
                    },
                    new()
                    {
                        Title = "PlayerController 示例",
                        Code = @"public class PlayerController : MonoBehaviour
{
    private GameAppInput mInput;
    
    void OnEnable()
    {
        mInput = InputKit.Get<GameAppInput>();
        mInput.Player.Attack.performed += OnAttack;
    }
    
    void OnDisable()
    {
        mInput.Player.Attack.performed -= OnAttack;
    }
    
    void Update()
    {
        var move = mInput.Player.Move.ReadValue<Vector2>();
        bool isRunning = mInput.Player.Run.IsPressed();
        // 应用移动...
    }
    
    private void OnAttack(InputAction.CallbackContext ctx)
    {
        if (InputKit.IsActionBlocked(""Attack"")) return;
        PerformAttack();
    }
}",
                        Explanation = "类型安全访问，编译时检查，无魔法字符串。"
                    }
                }
            };
        }
    }
}
#endif
