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
                Title = "输入上下文系统",
                Description = "InputKit 提供基于栈的输入上下文管理，支持 UI 打开时屏蔽游戏输入、对话时限制操作等场景。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "定义上下文",
                        Code = @"// 创建游戏上下文（允许所有输入）
var gameplayContext = new InputContext
{
    Name = ""Gameplay"",
    Priority = 0,
    AllowedActions = null,  // null 表示允许所有
    BlockedActions = null
};

// 创建 UI 上下文（只允许 UI 相关输入）
var uiContext = new InputContext
{
    Name = ""UI"",
    Priority = 10,
    AllowedActions = new HashSet<string> 
    { 
        ""Navigate"", ""Submit"", ""Cancel"", ""Point"", ""Click"" 
    },
    BlockedActions = null
};

// 创建对───────────────┘
//
// 栈顶上下文决定当前的输入规则
// 关闭 UI 时依次 Pop，自动恢复上一层的输入规则

// ============================================
// 与 ActionMap 的区别
// ============================================
// ActionMap：整个 Map 启用/禁用，需手动管理状态
// InputContext：单个 Action 级别控制，栈自动管理嵌套",
                        Explanation = "上下文系统通过栈结构自动管理多层输入状态，比手动切换 ActionMap 更适合复杂的 UI 嵌套场景。"
                    },
                    new()
                    {
                        Title = "创建 InputContext 资产",
                        Code = @"// ============================================
// 方法 1：在编辑器中创建（推荐）
// ============================================
// 1. Project 窗口右键 → Create → YokiFrame → InputKit → Input Context
// 2. 配置以下属性：
//    - Context Name: 唯一标识符（如 ""UI""、""Dialog""）
//    - Priority: 优先级（数值越大优先级越高）
//    - Enabled Action Maps: 在此上下文中启用的 ActionMap
//    - Blocked Actions: 在此上下文中被屏蔽的 Action
//    - Block All Lower Priority: 是否屏蔽所有低优先级输入

// ============================================
// 常用上下文配置示例
// ============================================

// Gameplay 上下文（默认，允许所有）
// - Context Name: ""Gameplay""
// - Priority: 0
// - Enabled Action Maps: [""Player""]
// - Blocked Actions: []

// UI 上下文（屏蔽游戏操作）
// - Context Name: ""UI""
// - Priority: 10
// - Enabled Action Maps: [""UI""]
// - Blocked Actions: [""Move"", ""Attack"", ""Dodge"", ""Jump""]

// Dialog 上下文（只允许对话相关）
// - Context Name: ""Dialog""
// - Priority: 20
// - Enabled Action Maps: [""UI""]
// - Blocked Actions: [""Move"", ""Attack"", ""Dodge"", ""Jump"", ""Interact""]
// - Block All Lower Priority: true

// Cutscene 上下文（只允许跳过）
// - Context Name: ""Cutscene""
// - Priority: 100
// - Enabled Action Maps: []
// - Blocked Actions: [] // 配合 BlockAllLowerPriority 使用
// - Block All Lower Priority: true",
                        Explanation = "InputContext 是 ScriptableObject，可在编辑器中可视化配置，便于策划调整。"
                    },
                    new()
                    {
                        Title = "注册与基础使用",
                        Code = @"public class GameManager : MonoBehaviour
{
    [SerializeField] private InputContext mGameplayContext;
    [SerializeField] private InputContext mUIContext;
    [SerializeField] private InputContext mDialogContext;
    
    void Awake()
    {
        // 注册所有上下文（用于按名称查找）
        InputKit.RegisterContext(mGameplayContext);
        InputKit.RegisterContext(mUIContext);
        InputKit.RegisterContext(mDialogContext);
        
        // 监听上下文变更
        InputKit.OnContextChanged += OnContextChanged;
    }
    
    void OnDestroy()
    {
        InputKit.OnContextChanged -= OnContextChanged;
    }
    
    private void OnContextChanged(InputContext oldCtx, InputContext newCtx)
    {
        string oldName = oldCtx != default ? oldCtx.ContextName : ""None"";
        string newName = newCtx != default ? newCtx.ContextName : ""None"";
        Debug.Log($""上下文切换: {oldName} → {newName}"");
    }
}

// ============================================
// 在 UI 管理器中使用
// ============================================
public class UIManager : MonoBehaviour
{
    public void OpenPanel(string panelName)
    {
        // 压入 UI 上下文
        InputKit.PushContext(""UI"");
        ShowPanel(panelName);
    }
    
    public void ClosePanel()
    {
        // 弹出上下文
        InputKit.PopContext();
        HideCurrentPanel();
    }
}",
                        Explanation = "在 GameManager 中注册所有上下文，在 UI 管理器中通过名称 Push/Pop。"
                    },
                    new()
                    {
                        Title = "典型使用场景",
                        Code = @"// ============================================
// 场景 1：UI 系统（多层嵌套）
// ============================================
public class InventoryUI : MonoBehaviour
{
    public void Open()
    {
        InputKit.PushContext(""UI"");
        gameObject.SetActive(true);
    }
    
    public void Close()
    {
        InputKit.PopContext();
        gameObject.SetActive(false);
    }
    
    // 打开物品详情（嵌套 UI）
    public void ShowItemDetail(Item item)
    {
        // 再次 Push，栈中现在有两个 UI 上下文
        InputKit.PushContext(""UI"");
        mItemDetailPanel.Show(item);
    }
    
    public void HideItemDetail()
    {
        InputKit.PopContext();
        mItemDetailPanel.Hide();
    }
}

// ============================================
// 场景 2：对话系统
// ============================================
public class DialogSystem : MonoBehaviour
{
    public void StartDialog(DialogData dialog)
    {
        // 进入对话时屏蔽战斗输入
        InputKit.PushContext(""Dialog"");
        ShowDialogUI(dialog);
    }
    
    public void EndDialog()
    {
        InputKit.PopContext();
        HideDialogUI();
    }
}

// ============================================
// 场景 3：过场动画
// ============================================
public class CutsceneManager : MonoBehaviour
{
    public async UniTaskVoid PlayCutscene(CutsceneData cutscene)
    {
        // 过场动画只允许跳过
        InputKit.PushContext(""Cutscene"");
        
        await PlayCutsceneAsync(cutscene);
        
        InputKit.PopContext();
    }
}

// ============================================
// 场景 4：教程引导
// ============================================
public class TutorialManager : MonoBehaviour
{
    [SerializeField] private InputContext mTutorialContext;
    
    public void StartTutorialStep(string[] allowedActions)
    {
        // 动态配置允许的操作
        mTutorialContext.BlockedActions = GetBlockedActions(allowedActions);
        InputKit.PushContext(mTutorialContext);
    }
    
    public void EndTutorialStep()
    {
        InputKit.PopContext();
    }
}

// ============================================
// 场景 5：QTE 事件
// ============================================
public class QTEManager : MonoBehaviour
{
    [SerializeField] private InputContext mQTEContext;
    
    public void StartQTE(string requiredAction)
    {
        // QTE 期间只允许特定按键
        InputKit.PushContext(mQTEContext);
    }
    
    public void EndQTE()
    {
        InputKit.PopContext();
    }
}",
                        Explanation = "上下文系统适用于任何需要临时改变输入规则的场景，栈结构自动处理嵌套和恢复。"
                    },
                    new()
                    {
                        Title = "在输入处理中检查上下文",
                        Code = @"public class PlayerController : MonoBehaviour
{
    private GameAppInput mInput;
    
    void OnEnable()
    {
        mInput = InputKit.Get<GameAppInput>();
        mInput.Player.Attack.performed += OnAttackInput;
        mInput.Player.Dodge.performed += OnDodgeInput;
        mInput.Player.Interact.performed += OnInteractInput;
    }
    
    void OnDisable()
    {
        mInput.Player.Attack.performed -= OnAttackInput;
        mInput.Player.Dodge.performed -= OnDodgeInput;
        mInput.Player.Interact.performed -= OnInteractInput;
    }
    
    // ============================================
    // 方法 1：在回调中检查（推荐）
    // ============================================
    private void OnAttackInput(InputAction.CallbackContext ctx)
    {
        // 检查当前上下文是否允许攻击
        if (InputKit.IsActionBlocked(""Attack"")) return;
        
        PerformAttack();
    }
    
    private void OnDodgeInput(InputAction.CallbackContext ctx)
    {
        if (InputKit.IsActionBlocked(""Dodge"")) return;
        
        PerformDodge();
    }
    
    private void OnInteractInput(InputAction.CallbackContext ctx)
    {
        // 类型安全版本
        if (InputKit.IsActionBlocked(mInput.Player.Interact)) return;
        
        TryInteract();
    }
    
    // ============================================
    // 方法 2：结合输入缓冲
    // ============================================
    void Update()
    {
        // 检查缓冲的攻击输入
        if (!InputKit.IsActionBlocked(""Attack"") && 
            InputKit.ConsumeBufferedInput(mInput.Player.Attack))
        {
            PerformAttack();
        }
    }
}",
                        Explanation = "在输入回调的开头检查 IsActionBlocked，被屏蔽时直接返回。"
                    },
                    new()
                    {
                        Title = "高级栈操作",
                        Code = @"// ============================================
// 弹出到指定上下文
// ============================================
// 场景：从深层 UI 直接返回游戏
// 栈状态：Gameplay → Inventory → ItemDetail → ConfirmDialog

// 一次性弹出到 Gameplay（清空所有 UI 上下文）
InputKit.PopToContext(""Gameplay"");
// 栈状态：Gameplay

// ============================================
// 清空上下文栈
// ============================================
// 场景：切换场景时重置所有状态
InputKit.ClearContextStack();

// ============================================
// 查询上下文状态
// ============================================
// 获取当前上下文
var current = InputKit.CurrentContext;
if (current != default)
{
    Debug.Log($""当前上下文: {current.ContextName}"");
}

// 获取栈深度
int depth = InputKit.ContextDepth;
Debug.Log($""上下文栈深度: {depth}"");

// 检查是否包含指定上下文
if (InputKit.HasContext(""UI""))
{
    // 当前有 UI 打开
    mGameplayHUD.Hide();
}
else
{
    mGameplayHUD.Show();
}

// ============================================
// 监听上下文变更
// ============================================
InputKit.OnContextChanged += (oldCtx, newCtx) =>
{
    // 根据上下文更新游戏状态
    if (newCtx != default && newCtx.ContextName == ""UI"")
    {
        // 进入 UI 模式
        Time.timeScale = 0f; // 暂停游戏
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
    else if (oldCtx != default && oldCtx.ContextName == ""UI"" && 
             (newCtx == default || newCtx.ContextName == ""Gameplay""))
    {
        // 退出 UI 模式
        Time.timeScale = 1f;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
};",
                        Explanation = "PopToContext 可一次性弹出多个上下文，适合快速返回特定状态。OnContextChanged 事件可用于同步其他系统状态。"
                    },
                    new()
                    {
                        Title = "与 ActionMap 配合使用",
                        Code = @"// ============================================
// 上下文自动切换 ActionMap
// ============================================
// InputContext 的 EnabledActionMaps 属性会在 Push 时自动启用对应的 ActionMap

// 示例配置：
// UI 上下文：EnabledActionMaps = [""UI""]
// 当 Push ""UI"" 上下文时，自动调用 InputKit.EnableActionMaps(""UI"")

// ============================================
// 手动配合 ActionMap
// ============================================
InputKit.OnContextChanged += (oldCtx, newCtx) =>
{
    if (newCtx == default)
    {
        // 栈空，恢复默认
        InputKit.EnableActionMaps(""Player"", ""Camera"");
        return;
    }
    
    // 根据上下文切换 ActionMap
    switch (newCtx.ContextName)
    {
        case ""Gameplay"":
            InputKit.EnableActionMaps(""Player"", ""Camera"");
            break;
        case ""UI"":
            InputKit.SwitchActionMap(""UI"");
            break;
        case ""Dialog"":
            InputKit.EnableActionMaps(""UI"", ""Dialog"");
            break;
        case ""Cutscene"":
            InputKit.DisableAllActionMaps();
            InputKit.EnableActionMaps(""Cutscene"");
            break;
    }
};

// ============================================
// 何时用 ActionMap vs 上下文
// ============================================
// 用 ActionMap：
// - 完全不同的输入模式（如驾驶 vs 步行）
// - 不需要嵌套管理

// 用 InputContext：
// - 需要屏蔽部分 Action（如 UI 打开时屏蔽攻击但保留移动）
// - 多层嵌套场景（多个 UI 面板）
// - 需要自动恢复上一状态

// 两者结合：
// - 上下文管理状态切换逻辑
// - ActionMap 提供实际的输入绑定",
                        Explanation = "上下文系统可以自动切换 ActionMap，也可以通过事件手动控制，两者配合使用效果最佳。"
                    },
                    new()
                    {
                        Title = "完整集成示例",
                        Code = @"public class GameInputManager : MonoBehaviour
{
    [Header(""上下文配置"")]
    [SerializeField] private InputContext mGameplayContext;
    [SerializeField] private InputContext mUIContext;
    [SerializeField] private InputContext mDialogContext;
    [SerializeField] private InputContext mCutsceneContext;
    
    void Awake()
    {
        // 初始化 InputKit
        InputKit.Register<GameAppInput>();
        InputKit.Initialize();
        
        // 注册所有上下文
        InputKit.RegisterContext(mGameplayContext);
        InputKit.RegisterContext(mUIContext);
        InputKit.RegisterContext(mDialogContext);
        InputKit.RegisterContext(mCutsceneContext);
        
        // 监听上下文变更
        InputKit.OnContextChanged += OnContextChanged;
        
        // 默认进入 Gameplay 上下文
        InputKit.PushContext(""Gameplay"");
    }
    
    void OnDestroy()
    {
        InputKit.OnContextChanged -= OnContextChanged;
        InputKit.Dispose();
    }
    
    private void OnContextChanged(InputContext oldCtx, InputContext newCtx)
    {
        // 更新光标状态
        bool isUI = newCtx != default && 
                    (newCtx.ContextName == ""UI"" || newCtx.ContextName == ""Dialog"");
        
        Cursor.visible = isUI;
        Cursor.lockState = isUI ? CursorLockMode.None : CursorLockMode.Locked;
        
        // 更新 HUD 显示
        GameHUD.Instance.SetVisible(!isUI);
    }
    
    // ============================================
    // 公共 API
    // ============================================
    public static void EnterUI() => InputKit.PushContext(""UI"");
    public static void ExitUI() => InputKit.PopContext();
    
    public static void EnterDialog() => InputKit.PushContext(""Dialog"");
    public static void ExitDialog() => InputKit.PopContext();
    
    public static void EnterCutscene() => InputKit.PushContext(""Cutscene"");
    public static void ExitCutscene() => InputKit.PopContext();
    
    public static void ReturnToGameplay() => InputKit.PopToContext(""Gameplay"");
}",
                        Explanation = "在专门的 InputManager 中统一管理上下文，提供简洁的公共 API 供其他系统调用。"
                    }
                }
            };
        }
    }
}
#endif
