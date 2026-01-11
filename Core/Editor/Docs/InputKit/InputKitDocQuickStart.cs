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
                Description = "InputKit 是基于 Unity InputSystem 的输入管理框架，提供类型安全的 API、运行时重绑定、输入缓冲、连招系统等功能。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "完整使用流程",
                        Code = @"// ============================================
// 第一步：创建 InputActionAsset
// ============================================
// 1. 在 Project 窗口右键 → Create → Input Actions
// 2. 双击打开编辑器，创建 ActionMap 和 Action
// 3. 勾选 ""Generate C# Class""，点击 Apply
// 4. Unity 会生成一个 C# 类（如 GameAppInput.cs）

// ============================================
// 第二步：在 GameManager 中初始化
// ============================================
public class GameManager : MonoBehaviour
{
    void Awake()
    {
        // 1. 注册 InputSystem 生成的 C# 类
        InputKit.Register<GameAppInput>();
        
        // 2. 初始化（自动加载保存的按键绑定）
        InputKit.Initialize();
        
        // 3. 监听设备切换（可选）
        InputKit.OnDeviceChanged += OnDeviceChanged;
    }
    
    void Update()
    {
        // 4. 更新子系统（连招、震动、缓冲清理）
        InputKit.UpdateCombo();
        InputKit.UpdateHaptic();
        InputKit.CleanupBuffer();
    }
    
    void OnDestroy()
    {
        // 5. 释放资源
        InputKit.OnDeviceChanged -= OnDeviceChanged;
        InputKit.Dispose();
    }
    
    private void OnDeviceChanged(InputDeviceType device)
    {
        Debug.Log($""设备切换: {device}"");
    }
}

// ============================================
// 第三步：在游戏逻辑中使用
// ============================================
public class PlayerController : MonoBehaviour
{
    private GameAppInput mInput;
    
    void OnEnable()
    {
        // 获取类型安全的输入实例
        mInput = InputKit.Get<GameAppInput>();
        
        // 订阅事件
        mInput.Player.Attack.performed += OnAttack;
    }
    
    void OnDisable()
    {
        mInput.Player.Attack.performed -= OnAttack;
    }
    
    void Update()
    {
        // 读取移动输入
        var move = mInput.Player.Move.ReadValue<Vector2>();
        transform.Translate(move * Time.deltaTime * 5f);
    }
    
    private void OnAttack(InputAction.CallbackContext ctx)
    {
        Debug.Log(""攻击!"");
    }
}",
                        Explanation = "三步完成：注册 → 初始化 → 使用。InputKit 封装了 InputSystem 的复杂性，提供简洁的静态 API。"
                    },
                    new()
                    {
                        Title = "核心 API 速查",
                        Code = @"// ============ 生命周期 ============
InputKit.Register<GameAppInput>();     // 注册输入类
InputKit.Initialize();                  // 初始化
InputKit.Dispose();                     // 释放资源

// ============ 获取输入 ============
var input = InputKit.Get<GameAppInput>();
var move = input.Player.Move.ReadValue<Vector2>();
bool isPressed = input.Player.Attack.IsPressed();

// ============ 设备检测 ============
InputDeviceType device = InputKit.CurrentDeviceType;
bool isGamepad = InputKit.IsUsingGamepad;
bool isKeyboard = InputKit.IsUsingKeyboardMouse;

// ============ ActionMap 管理 ============
InputKit.SwitchActionMap(""UI"");        // 切换到 UI
InputKit.EnableAllActionMaps();         // 启用所有

// ============ 重绑定 ============
await InputKit.RebindAsync(input.Player.Attack);
InputKit.ResetAllBindings();            // 重置所有绑定
string display = InputKit.GetBindingDisplayString(input.Player.Attack);

// ============ 输入缓冲 ============
InputKit.SetBufferWindow(150f);         // 设置缓冲窗口
bool has = InputKit.HasBufferedInput(input.Player.Attack);
bool consumed = InputKit.ConsumeBufferedInput(input.Player.Attack);

// ============ 连招系统 ============
InputKit.RegisterCombo(""Combo1"", 
    ComboStep.Tap(""Attack""), 
    ComboStep.Tap(""Attack""));
InputKit.ProcessComboTap(""Attack"");   // 处理输入
InputKit.UpdateCombo();                 // 每帧更新

// ============ 震动反馈 ============
InputKit.PlayHaptic(HapticPreset.Medium);
InputKit.UpdateHaptic();                // 每帧更新

// ============ 上下文系统 ============
InputKit.PushContext(""UI"");           // 压入上下文
InputKit.PopContext();                  // 弹出上下文
bool blocked = InputKit.IsActionBlocked(""Attack"");",
                        Explanation = "所有 API 都是静态方法，通过 InputKit.Xxx() 调用。"
                    },
                    new()
                    {
                        Title = "常见问题",
                        Code = @"// Q1: 为什么要调用 UpdateCombo/UpdateHaptic？
// A: 这些子系统需要每帧检查超时和更新状态。
//    如果不使用连招或震动功能，可以不调用。

// Q2: 为什么 BufferInput 是 internal？
// A: 输入缓冲应该在 Action 回调中自动记录，
//    而不是手动调用。可以通过扩展方法暴露：
public static class InputKitExtensions
{
    public static void RecordToBuffer(this InputAction action)
    {
        // 需要修改 InputKit.BufferInput 为 public
    }
}

// Q3: 如何在不同场景间保持输入状态？
// A: InputKit 是静态类，状态自动跨场景保持。
//    只需在游戏启动时初始化一次。

// Q4: 如何支持多玩家？
// A: 当前设计为单玩家。多玩家需要为每个玩家
//    创建独立的输入实例，不使用静态 API。

// Q5: 重绑定后如何更新 UI？
// A: 监听 OnBindingChanged 事件：
InputKit.OnBindingChanged += (action, index) =>
{
    RefreshBindingUI(action);
};",
                        Explanation = "常见问题解答，帮助理解 InputKit 的设计决策。"
                    }
                }
            };
        }
    }
}
#endif
