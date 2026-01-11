#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// InputKit 运行时重绑定文档
    /// </summary>
    internal static class InputKitDocRebind
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "运行时重绑定",
                Description = "支持玩家自定义按键绑定，类型安全 API，自动持久化。包含完整 UI 实现示例、复合绑定处理、多控制方案支持。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "核心概念：绑定索引（BindingIndex）",
                        Code = @"// ============================================
// 什么是 BindingIndex？
// ============================================
// 每个 InputAction 可以有多个绑定（Binding）
// BindingIndex 是绑定在 action.bindings 数组中的位置

// 示例：Attack Action 的绑定结构
// bindings[0] = ""<Keyboard>/space""     ← 键盘绑定
// bindings[1] = ""<Gamepad>/buttonSouth"" ← 手柄绑定

// 重绑定键盘按键
await InputKit.RebindAsync(input.Player.Attack, bindingIndex: 0);

// 重绑定手柄按键
await InputKit.RebindAsync(input.Player.Attack, bindingIndex: 1);

// ============================================
// 复合绑定（Composite）的索引结构
// ============================================
// Move Action 使用 WASD 复合绑定时：
// bindings[0] = ""2DVector"" (Composite)  ← 复合绑定本身，不可重绑定
// bindings[1] = ""Up""    → ""<Keyboard>/w""
// bindings[2] = ""Down""  → ""<Keyboard>/s""
// bindings[3] = ""Left""  → ""<Keyboard>/a""
// bindings[4] = ""Right"" → ""<Keyboard>/d""
// bindings[5] = ""2DVector"" (Composite)  ← 手柄复合绑定
// bindings[6] = ""Up""    → ""<Gamepad>/leftStick/up""
// ...

// 重绑定 W 键（向上移动）
await InputKit.RebindAsync(input.Player.Move, bindingIndex: 1);

// 重绑定 S 键（向下移动）
await InputKit.RebindAsync(input.Player.Move, bindingIndex: 2);",
                        Explanation = "BindingIndex 是绑定在数组中的位置。复合绑定（如 WASD）的每个方向是独立的绑定，索引从 1 开始（0 是复合绑定本身）。"
                    },
                    new()
                    {
                        Title = "查找正确的 BindingIndex",
                        Code = @"var input = InputKit.Get<GameAppInput>();
var action = input.Player.Move;

// 方法 1：遍历打印所有绑定
for (int i = 0; i < action.bindings.Count; i++)
{
    var binding = action.bindings[i];
    Debug.Log($""[{i}] {binding.name}: {binding.effectivePath}"");
}

// 方法 2：按控制方案查找
int GetBindingIndex(InputAction action, string controlScheme, string compositePart = null)
{
    for (int i = 0; i < action.bindings.Count; i++)
    {
        var binding = action.bindings[i];
        
        // 检查控制方案
        if (!string.IsNullOrEmpty(controlScheme) && 
            !binding.groups.Contains(controlScheme))
            continue;
        
        // 检查复合绑定部分
        if (!string.IsNullOrEmpty(compositePart) && 
            binding.name != compositePart)
            continue;
        
        // 跳过复合绑定本身
        if (binding.isComposite) continue;
        
        return i;
    }
    return -1;
}

// 查找键盘方案的 ""Up"" 绑定
int upIndex = GetBindingIndex(action, ""Keyboard&Mouse"", ""Up"");

// 方法 3：使用 InputBinding.MaskByGroup
var mask = InputBinding.MaskByGroup(""Keyboard&Mouse"");
int index = action.GetBindingIndex(mask);",
                        Explanation = "复合绑定的 binding.name 是方向名（Up/Down/Left/Right），binding.groups 包含控制方案名。"
                    },
                    new()
                    {
                        Title = "基础重绑定 UI（UGUI）",
                        Code = @"public class RebindButton : MonoBehaviour
{
    [SerializeField] private Button mButton;
    [SerializeField] private Text mBindingText;
    [SerializeField] private GameObject mWaitingOverlay;
    
    private GameAppInput mInput;
    private InputAction mTargetAction;
    private int mBindingIndex;
    
    public void Setup(InputAction action, int bindingIndex)
    {
        mInput = InputKit.Get<GameAppInput>();
        mTargetAction = action;
        mBindingIndex = bindingIndex;
        
        mButton.onClick.AddListener(OnButtonClicked);
        UpdateDisplay();
    }
    
    private async void OnButtonClicked()
    {
        // 显示等待提示
        mWaitingOverlay.SetActive(true);
        mBindingText.text = ""按下新按键..."";
        mButton.interactable = false;
        
        // 执行重绑定
        bool success = await InputKit.RebindAsync(
            mTargetAction,
            mBindingIndex,
            destroyCancellationToken);
        
        // 恢复 UI
        mWaitingOverlay.SetActive(false);
        mButton.interactable = true;
        
        if (success)
        {
            UpdateDisplay();
        }
        else
        {
            mBindingText.text = ""已取消"";
            await UniTask.Delay(500, cancellationToken: destroyCancellationToken);
            UpdateDisplay();
        }
    }
    
    private void UpdateDisplay()
    {
        mBindingText.text = InputKit.GetBindingDisplayString(
            mTargetAction, 
            mBindingIndex);
    }
    
    void OnDestroy()
    {
        mButton.onClick.RemoveListener(OnButtonClicked);
    }
}",
                        Explanation = "基础 UI 组件：点击按钮 → 显示等待 → 执行重绑定 → 更新显示。使用 destroyCancellationToken 确保对象销毁时取消操作。"
                    },
                    new()
                    {
                        Title = "完整按键设置面板",
                        Code = @"public class KeyBindingPanel : MonoBehaviour
{
    [SerializeField] private Transform mBindingContainer;
    [SerializeField] private RebindButton mBindingPrefab;
    [SerializeField] private Button mResetAllButton;
    [SerializeField] private Toggle mKeyboardToggle;
    [SerializeField] private Toggle mGamepadToggle;
    
    private GameAppInput mInput;
    private string mCurrentScheme = ""Keyboard&Mouse"";
    private readonly List<RebindButton> mBindingButtons = new();
    
    void Start()
    {
        mInput = InputKit.Get<GameAppInput>();
        
        // 监听绑定变更
        InputKit.OnBindingChanged += OnBindingChanged;
        
        // 设置控制方案切换
        mKeyboardToggle.onValueChanged.AddListener(on => 
        {
            if (on) SwitchScheme(""Keyboard&Mouse"");
        });
        mGamepadToggle.onValueChanged.AddListener(on => 
        {
            if (on) SwitchScheme(""Gamepad"");
        });
        
        // 重置按钮
        mResetAllButton.onClick.AddListener(OnResetAllClicked);
        
        // 初始化绑定列表
        RefreshBindingList();
    }
    
    private void RefreshBindingList()
    {
        // 清理旧按钮
        for (int i = 0; i < mBindingButtons.Count; i++)
        {
            Destroy(mBindingButtons[i].gameObject);
        }
        mBindingButtons.Clear();
        
        // 创建绑定按钮
        CreateBindingButton(""攻击"", mInput.Player.Attack, mCurrentScheme);
        CreateBindingButton(""闪避"", mInput.Player.Dodge, mCurrentScheme);
        CreateBindingButton(""交互"", mInput.Player.Interact, mCurrentScheme);
        
        // 复合绑定（移动）
        CreateCompositeBindingButtons(""移动"", mInput.Player.Move, mCurrentScheme);
    }
    
    private void CreateBindingButton(string label, InputAction action, string scheme)
    {
        int index = FindBindingIndex(action, scheme);
        if (index < 0) return;
        
        var button = Instantiate(mBindingPrefab, mBindingContainer);
        button.Setup(action, index);
        mBindingButtons.Add(button);
    }
    
    private void CreateCompositeBindingButtons(string label, InputAction action, string scheme)
    {
        string[] parts = { ""Up"", ""Down"", ""Left"", ""Right"" };
        string[] labels = { ""上"", ""下"", ""左"", ""右"" };
        
        for (int i = 0; i < parts.Length; i++)
        {
            int index = FindCompositePartIndex(action, scheme, parts[i]);
            if (index < 0) continue;
            
            var button = Instantiate(mBindingPrefab, mBindingContainer);
            button.Setup(action, index);
            mBindingButtons.Add(button);
        }
    }
    
    private int FindBindingIndex(InputAction action, string scheme)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            if (binding.isComposite || binding.isPartOfComposite) continue;
            if (binding.groups.Contains(scheme)) return i;
        }
        return -1;
    }
    
    private int FindCompositePartIndex(InputAction action, string scheme, string part)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            if (!binding.isPartOfComposite) continue;
            if (binding.name != part) continue;
            if (binding.groups.Contains(scheme)) return i;
        }
        return -1;
    }
    
    private void SwitchScheme(string scheme)
    {
        mCurrentScheme = scheme;
        RefreshBindingList();
    }
    
    private void OnResetAllClicked()
    {
        InputKit.ResetAllBindings();
    }
    
    private void OnBindingChanged(InputAction action, int index)
    {
        RefreshBindingList();
    }
    
    void OnDestroy()
    {
        InputKit.OnBindingChanged -= OnBindingChanged;
    }
}",
                        Explanation = "完整面板：支持键盘/手柄切换、复合绑定（WASD）、重置所有。通过 OnBindingChanged 事件自动刷新 UI。"
                    },
                    new()
                    {
                        Title = "高级重绑定配置",
                        Code = @"// 使用 RebindOptions 进行高级配置
var options = new RebindOptions
{
    BindingIndex = 0,
    
    // 取消键（按 Escape 取消重绑定）
    CancelKey = ""<Keyboard>/escape"",
    
    // 排除的控件（防止误触）
    ExcludedControls = new[] 
    { 
        ""<Mouse>/position"",   // 鼠标位置
        ""<Mouse>/delta"",      // 鼠标移动
        ""<Pointer>/position"", // 触摸位置
        ""<Pointer>/delta""     // 触摸移动
    },
    
    // 等待延迟（防止按键抖动）
    WaitDelay = 0.1f,
    
    // 控制方案筛选（仅接受该方案的输入）
    BindingGroup = ""Keyboard&Mouse""
};

var success = await InputKit.RebindAsync(
    mInput.Player.Attack,
    options,
    destroyCancellationToken);

// ============================================
// 绑定冲突处理
// ============================================
InputKit.OnBindingConflict += (action, conflicts) =>
{
    // 显示冲突警告
    var sb = ZString.CreateStringBuilder();
    sb.Append(action.name);
    sb.Append("" 与以下按键冲突:\n"");
    
    for (int i = 0; i < conflicts.Count; i++)
    {
        sb.Append(""  - "");
        sb.Append(conflicts[i].name);
        sb.Append(""\n"");
    }
    
    ShowWarningDialog(sb.ToString());
};

// ============================================
// 超时处理
// ============================================
using var cts = CancellationTokenSource.CreateLinkedTokenSource(
    destroyCancellationToken);
cts.CancelAfter(TimeSpan.FromSeconds(10)); // 10 秒超时

var success = await InputKit.RebindAsync(
    mInput.Player.Attack,
    options,
    cts.Token);

if (!success)
{
    // 可能是取消或超时
    ShowMessage(""重绑定已取消或超时"");
}",
                        Explanation = "RebindOptions 提供取消键、排除控件、控制方案筛选等配置。使用 CancellationTokenSource 实现超时。"
                    },
                    new()
                    {
                        Title = "绑定持久化",
                        Code = @"// ============================================
// 自动持久化
// ============================================
// InputKit 在以下时机自动保存绑定：
// - RebindAsync 成功后
// - ResetBinding / ResetActionBindings / ResetAllBindings 后

// Initialize() 时自动加载保存的绑定
InputKit.Initialize();

// ============================================
// 手动持久化
// ============================================
// 保存当前所有绑定
InputKit.SaveBindings();

// 加载保存的绑定
InputKit.LoadBindings();

// 检查是否有保存的绑定
if (InputKit.HasSavedBindings)
{
    mResetButton.SetActive(true);
}

// 清除保存的绑定
InputKit.ClearSavedBindings();

// ============================================
// 云存档支持
// ============================================
// 导出为 JSON 字符串
string json = InputKit.ExportBindingsJson();

// 保存到云端
await CloudSave.SetStringAsync(""input_bindings"", json);

// 从云端恢复
string savedJson = await CloudSave.GetStringAsync(""input_bindings"");
if (!string.IsNullOrEmpty(savedJson))
{
    InputKit.ImportBindingsJson(savedJson);
}

// ============================================
// 自定义持久化
// ============================================
// 设置自定义存储键名
InputKit.SetPersistenceKey(""MyGame_InputBindings_v2"");

// 设置自定义持久化实现
InputKit.SetPersistence(new MyCustomPersistence());

public class MyCustomPersistence : IInputPersistence
{
    public void Save(string key, string json) { /* ... */ }
    public string Load(string key) { /* ... */ }
    public void Delete(string key) { /* ... */ }
    public bool Exists(string key) { /* ... */ }
}",
                        Explanation = "默认使用 PlayerPrefs 持久化。支持导出 JSON 用于云存档，或实现 IInputPersistence 自定义存储。"
                    },
                    new()
                    {
                        Title = "重置绑定",
                        Code = @"var input = InputKit.Get<GameAppInput>();

// ============================================
// 重置单个绑定
// ============================================
// 重置 Attack 的第一个绑定到默认值
InputKit.ResetBinding(input.Player.Attack, bindingIndex: 0);

// ============================================
// 重置 Action 的所有绑定
// ============================================
// 重置 Attack 的所有绑定（键盘 + 手柄）
InputKit.ResetActionBindings(input.Player.Attack);

// ============================================
// 重置所有绑定
// ============================================
InputKit.ResetAllBindings();

// ============================================
// 监听绑定变更
// ============================================
InputKit.OnBindingChanged += (action, bindingIndex) =>
{
    if (action == default)
    {
        // action 为 null 表示全部重置
        RefreshAllBindingUI();
    }
    else if (bindingIndex < 0)
    {
        // bindingIndex < 0 表示 Action 的所有绑定重置
        RefreshActionBindingUI(action);
    }
    else
    {
        // 单个绑定变更
        RefreshSingleBindingUI(action, bindingIndex);
    }
};",
                        Explanation = "重置后自动保存并触发 OnBindingChanged 事件。通过 action 和 bindingIndex 参数判断重置范围。"
                    },
                    new()
                    {
                        Title = "获取绑定显示名称",
                        Code = @"var input = InputKit.Get<GameAppInput>();

// ============================================
// 基础用法
// ============================================
// 获取第一个绑定的显示名称
string display = InputKit.GetBindingDisplayString(input.Player.Attack);
// 输出: ""Space"" 或 ""A"" (手柄)

// 获取指定索引的绑定
string display = InputKit.GetBindingDisplayString(input.Player.Attack, 0);

// ============================================
// 按控制方案获取
// ============================================
// 获取键盘方案的显示名称
string kbDisplay = InputKit.GetBindingDisplayString(
    input.Player.Attack, 
    ""Keyboard&Mouse"");
// 输出: ""Space""

// 获取手柄方案的显示名称
string gpDisplay = InputKit.GetBindingDisplayString(
    input.Player.Attack, 
    ""Gamepad"");
// 输出: ""Button South"" 或 ""A""

// ============================================
// 复合绑定显示
// ============================================
// 获取移动的完整显示（WASD）
string moveDisplay = InputKit.GetBindingDisplayString(input.Player.Move);
// 输出: ""W/A/S/D"" 或 ""Left Stick""

// 获取复合绑定的单个部分
string upDisplay = InputKit.GetBindingDisplayString(
    input.Player.Move, 
    bindingIndex: 1); // Up 部分
// 输出: ""W""

// ============================================
// UI 显示示例
// ============================================
void UpdatePromptUI()
{
    string scheme = InputKit.IsUsingGamepad ? ""Gamepad"" : ""Keyboard&Mouse"";
    
    mAttackPrompt.text = InputKit.GetBindingDisplayString(
        mInput.Player.Attack, scheme);
    mDodgePrompt.text = InputKit.GetBindingDisplayString(
        mInput.Player.Dodge, scheme);
}

// 设备切换时更新
InputKit.OnDeviceChanged += _ => UpdatePromptUI();",
                        Explanation = "GetBindingDisplayString 返回本地化的按键名称。可按控制方案筛选，适合动态 UI 提示。"
                    }
                }
            };
        }
    }
}
#endif
