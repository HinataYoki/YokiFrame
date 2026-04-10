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
                Description = "支持玩家自定义按键，自动持久化。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基础重绑定",
                        Code = @"var input = InputKit.Get<GameAppInput>();

// 重绑定（自动保存）
bool success = await InputKit.RebindAsync(
    input.Player.Attack, 
    bindingIndex: 0,
    destroyCancellationToken);

// 获取显示名称
string display = InputKit.GetBindingDisplayString(input.Player.Attack, 0);

// 重置绑定
InputKit.ResetBinding(input.Player.Attack, 0);  // 单个
InputKit.ResetActionBindings(input.Player.Attack); // Action 全部
InputKit.ResetAllBindings();  // 所有",
                        Explanation = "RebindAsync 成功后自动保存，Initialize 时自动加载。"
                    },
                    new()
                    {
                        Title = "绑定索引",
                        Code = @"// 单按键 Action
// bindings[0] = ""<Keyboard>/space""
// bindings[1] = ""<Gamepad>/buttonSouth""

// 复合绑定（WASD）
// bindings[0] = ""2DVector"" (Composite，不可重绑定)
// bindings[1] = ""Up""    → ""<Keyboard>/w""
// bindings[2] = ""Down""  → ""<Keyboard>/s""
// bindings[3] = ""Left""  → ""<Keyboard>/a""
// bindings[4] = ""Right"" → ""<Keyboard>/d""

// 重绑定 W 键
await InputKit.RebindAsync(input.Player.Move, bindingIndex: 1);",
                        Explanation = "复合绑定的每个方向是独立绑定，索引从 1 开始。"
                    },
                    new()
                    {
                        Title = "重绑定 UI",
                        Code = @"public class RebindButton : MonoBehaviour
{
    [SerializeField] private Button mButton;
    [SerializeField] private Text mText;
    
    private InputAction mAction;
    private int mBindingIndex;
    
    public void Setup(InputAction action, int index)
    {
        mAction = action;
        mBindingIndex = index;
        mButton.onClick.AddListener(OnClick);
        UpdateDisplay();
    }
    
    private async void OnClick()
    {
        mText.text = ""按下新按键..."";
        mButton.interactable = false;
        
        bool success = await InputKit.RebindAsync(
            mAction, mBindingIndex, destroyCancellationToken);
        
        mButton.interactable = true;
        UpdateDisplay();
    }
    
    private void UpdateDisplay()
    {
        mText.text = InputKit.GetBindingDisplayString(mAction, mBindingIndex);
    }
}",
                        Explanation = "点击 → 等待输入 → 更新显示。"
                    },
                    new()
                    {
                        Title = "高级配置",
                        Code = @"var options = new RebindOptions
{
    BindingIndex = 0,
    CancelKey = ""<Keyboard>/escape"",
    ExcludedControls = new[] { ""<Mouse>/position"", ""<Mouse>/delta"" },
    BindingGroup = ""Keyboard&Mouse""
};

await InputKit.RebindAsync(action, options, token);

// 绑定冲突事件
InputKit.OnBindingConflict += (action, conflicts) =>
{
    ShowWarning($""{action.name} 与其他按键冲突"");
};

// 绑定变更事件
InputKit.OnBindingChanged += (action, index) =>
{
    RefreshUI();
};",
                        Explanation = "RebindOptions 支持取消键、排除控件、控制方案筛选。"
                    },
                    new()
                    {
                        Title = "持久化",
                        Code = @"// 自动：RebindAsync 成功后自动保存
// 自动：Initialize() 时自动加载

// 手动
InputKit.SaveBindings();
InputKit.LoadBindings();
InputKit.ClearSavedBindings();

// 云存档
string json = InputKit.ExportBindingsJson();
InputKit.ImportBindingsJson(json);

// 自定义存储
InputKit.SetPersistenceKey(""MyGame_Bindings"");
InputKit.SetPersistence(new MyPersistence());",
                        Explanation = "默认 PlayerPrefs，支持导出 JSON 或自定义实现。"
                    }
                }
            };
        }
    }
}
#endif
