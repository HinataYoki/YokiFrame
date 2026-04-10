#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UIKit 自定义面板文档
    /// </summary>
    internal static class UIKitDocCustomPanel
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "自定义面板",
                Description = "创建面板需继承 UIPanel，实现生命周期方法。推荐使用编辑器工具创建。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基础面板",
                        Code = @"public class MainMenuPanel : UIPanel
{
    [SerializeField] private Button mBtnStart;
    [SerializeField] private Button mBtnSettings;
    [SerializeField] private Text mTxtVersion;

    protected override void OnInit(IUIData data = null)
    {
        // 初始化，只调用一次
        mBtnStart.onClick.AddListener(OnStartClick);
        mBtnSettings.onClick.AddListener(OnSettingsClick);
    }

    protected override void OnOpen(IUIData data = null)
    {
        // 每次打开时调用
        mTxtVersion.text = Application.version;
    }

    protected override void OnClose()
    {
        // 关闭时清理
        mBtnStart.onClick.RemoveAllListeners();
        mBtnSettings.onClick.RemoveAllListeners();
    }

    private void OnStartClick() => CloseSelf();
    private void OnSettingsClick() => UIKit.PushOpenPanel<SettingsPanel>();
}",
                        Explanation = "UIPanel 继承 MonoBehaviour，但业务逻辑应与 Unity 生命周期解耦。"
                    },
                    new()
                    {
                        Title = "数据传递",
                        Code = @"// 定义数据类
public class GameOverData : IUIData
{
    public int Score;
    public int HighScore;
    public bool IsNewRecord;
}

// 面板中使用
public class GameOverPanel : UIPanel
{
    [SerializeField] private Text mTxtScore;

    protected override void OnOpen(IUIData data = null)
    {
        if (data is GameOverData d)
        {
            mTxtScore.text = $""得分: {d.Score}"";
        }
    }
}

// 打开并传递数据
var data = new GameOverData { Score = 1000 };
UIKit.OpenPanel<GameOverPanel>(UILevel.Pop, data);",
                        Explanation = "通过 IUIData 传递数据，保持面板与数据源解耦。"
                    },
                    new()
                    {
                        Title = "设置默认焦点",
                        Code = @"public class SettingsPanel : UIPanel
{
    [SerializeField] private Button mBtnGraphics;

    protected override void Awake()
    {
        base.Awake();
        // 设置默认焦点（手柄模式自动聚焦）
        SetDefaultSelectable(mBtnGraphics);
    }
}

// 或在 OnDidShow 中动态设置
protected override void OnDidShow()
{
    if (UIKit.GetInputMode() == UIInputMode.Navigation)
    {
        UIKit.SetFocus(mBtnGraphics);
    }
}",
                        Explanation = "默认焦点改善手柄/键盘导航体验。"
                    },
                    new()
                    {
                        Title = "生命周期钩子",
                        Code = @"public class AnimatedPanel : UIPanel
{
    protected override void OnWillShow()
    {
        // 显示动画开始前
        AudioKit.Play(""UI/Appear"");
    }

    protected override void OnDidShow()
    {
        // 显示动画完成后
    }

    protected override void OnFocus()
    {
        // 成为栈顶面板时
        base.OnFocus();
    }

    protected override void OnBlur()
    {
        // 失去栈顶位置时
        base.OnBlur();
    }

    protected override void OnResume()
    {
        // 从栈中恢复时（Pop 后）
        base.OnResume();
    }
}",
                        Explanation = "生命周期钩子按顺序调用，异常不会中断后续钩子。"
                    },
                    new()
                    {
                        Title = "UIElement 和 UIComponent",
                        Code = @"// UIElement - 轻量级 UI 元素（列表项等）
public class ItemSlot : UIElement
{
    [SerializeField] private Image mIcon;
    [SerializeField] private Text mCount;

    public void SetData(ItemData data)
    {
        mIcon.sprite = data.Icon;
        mCount.text = data.Count.ToString();
    }
}

// UIComponent - 带生命周期的复杂组件
public class PlayerCard : UIComponent
{
    protected override void OnInit() { }
    protected override void OnShow() { }
    protected override void OnHide() { }
}",
                        Explanation = "UIElement 用于面板内复用，UIComponent 用于跨面板复用。"
                    }
                }
            };
        }
    }
}
#endif
