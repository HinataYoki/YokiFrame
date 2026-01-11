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
                Description = "InputKit 提供类型安全的输入管理，基于 Unity InputSystem 生成的 C# 类实现零魔法字符串访问。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "前置准备：创建 InputActionAsset",
                        Code = @"// ============================================
// 步骤 1：创建 InputActionAsset
// ============================================
// 1. Project 窗口右键 → Create → Input Actions
// 2. 命名为 GameAppInput（或其他名称）
// 3. 双击打开 Input Actions 编辑器

// ============================================
// 步骤 2：配置 ActionMap 和 Action
// ============================================
// 1. 点击 + 创建 ActionMap（如 ""Player""）
// 2. 在 ActionMap 中添加 Action：
//    - Move (Value, Vector2) - 移动
//    - Attack (Button) - 攻击
//    - Dodge (Button) - 闪避
// 3. 为每个 Action 添加绑定（Binding）

// ============================================
// 步骤 3：生成 C# 类
// ============================================
// 1. 选中 InputActionAsset
// 2. 在 Inspector 中勾选 ""Generate C# Class""
// 3. 设置 Class Name（如 GameAppInput）
// 4. 点击 Apply
// 5. Unity 自动生成 GameAppInput.cs

// 生成的类实现了 IInputActionCollection2 接口
// InputKit 通过此接口管理输入",
                        Explanation = "InputKit 依赖 InputSystem 生成的 C# 类，必须先完成此步骤。"
                    },
                    new()
                    {
                        Title = "基础初始化",
                        Code = @"// 注册 InputSystem 生成的输入类
InputKit.Register<GameAppInput>();

// 初始化（自动加载持久化绑定并启用输入）
InputKit.Initialize();

// 获取输入类实例（类型安全）
var input = InputKit.Get<GameAppInput>();

// 检查初始化状态
if (InputKit.IsInitialized)
{
    Debug.Log($""已注册输入类，当前设备: {InputKit.CurrentDeviceType}"");
}

// 释放资源（场景切换或退出时）
InputKit.Dispose();",
                        Explanation = "Register<T>() 接受 InputSystem 生成的 C# 类，Get<T>() 返回类型安全的实例。"
                    },
                    new()
                    {
                        Title = "类型安全输入访问",
                        Code = @"public class PlayerController : MonoBehaviour
{
    private GameAppInput mInput;
    
    void OnEnable()
    {
        // 获取类型安全的输入实例
        mInput = InputKit.Get<GameAppInput>();
        
        // 直接访问 Action（无魔法字符串）
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
    }
    
    private void OnAttack(InputAction.CallbackContext ctx) => ExecuteAttack();
    private void OnDodge(InputAction.CallbackContext ctx) => ExecuteDodge();
}",
                        Explanation = "通过 InputSystem 生成的类直接访问 Action，编译时类型检查，无运行时字符串查找。"
                    },
                    new()
                    {
                        Title = "GameManager 集成示例",
                        Code = @"public class GameManager : MonoBehaviour
{
    void Awake()
    {
        // 注册输入类
        InputKit.Register<GameAppInput>();
        
        // 可选：设置自定义持久化
        InputKit.SetPersistence(new PlayerPrefsPersistence());
        InputKit.SetPersistenceKey(""MyGame_InputBindings"");
        
        // 初始化
        InputKit.Initialize();
        
        // 监听设备切换
        InputKit.OnDeviceChanged += OnDeviceChanged;
    }
    
    void Update()
    {
        // 更新子系统（必须每帧调用）
        InputKit.UpdateCombo();    // 连招超时检测
        InputKit.UpdateHaptic();   // 震动曲线更新
        InputKit.CleanupBuffer();  // 清理过期缓冲
    }
    
    void OnDestroy()
    {
        InputKit.OnDeviceChanged -= OnDeviceChanged;
        InputKit.Dispose();
    }
    
    private static void OnDeviceChanged(InputDeviceType device)
    {
        // 更新 UI 提示图标
        UIManager.Instance.RefreshInputPrompts(device);
    }
}",
                        Explanation = "建议在 GameManager 或专门的 InputManager 中统一管理 InputKit 生命周期。Update 中的三个调用是可选的，只有使用对应功能时才需要。"
                    },
                    new()
                    {
                        Title = "完整 PlayerController 示例",
                        Code = @"public class PlayerController : MonoBehaviour
{
    private GameAppInput mInput;
    private Rigidbody2D mRb;
    private float mMoveSpeed = 5f;
    
    void OnEnable()
    {
        // 获取类型安全的输入实例
        mInput = InputKit.Get<GameAppInput>();
        mRb = GetComponent<Rigidbody2D>();
        
        // 订阅按钮事件
        mInput.Player.Attack.performed += OnAttack;
        mInput.Player.Dodge.performed += OnDodge;
        mInput.Player.Interact.performed += OnInteract;
    }
    
    void OnDisable()
    {
        // 取消订阅（防止内存泄漏）
        mInput.Player.Attack.performed -= OnAttack;
        mInput.Player.Dodge.performed -= OnDodge;
        mInput.Player.Interact.performed -= OnInteract;
    }
    
    void Update()
    {
        // 读取移动输入（Vector2）
        var move = mInput.Player.Move.ReadValue<Vector2>();
        
        // 检测按住状态
        bool isRunning = mInput.Player.Run.IsPressed();
        float speed = isRunning ? mMoveSpeed * 2f : mMoveSpeed;
        
        // 应用移动
        mRb.linearVelocity = move * speed;
    }
    
    private void OnAttack(InputAction.CallbackContext ctx)
    {
        // 处理连招输入
        InputKit.ProcessComboTap(""Attack"");
        
        // 执行攻击逻辑
        PerformAttack();
    }
    
    private void OnDodge(InputAction.CallbackContext ctx)
    {
        InputKit.ProcessComboTap(""Dodge"");
        PerformDodge();
    }
    
    private void OnInteract(InputAction.CallbackContext ctx)
    {
        // 检查上下文是否允许交互
        if (InputKit.IsActionBlocked(""Interact"")) return;
        
        TryInteract();
    }
    
    private void PerformAttack() { /* ... */ }
    private void PerformDodge() { /* ... */ }
    private void TryInteract() { /* ... */ }
}",
                        Explanation = "展示了完整的输入处理流程：获取实例、订阅事件、读取值、处理回调。"
                    }
                }
            };
        }
    }
}
#endif
