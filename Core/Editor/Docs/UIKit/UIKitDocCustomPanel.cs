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
                Description = "创建自定义面板需继承 UIPanel 并实现生命周期方法。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "完整面板示例",
                        Code = @"public class MainMenuPanel : UIPanel
{
    // UI 组件引用（使用 Bind 系统自动生成）
    private Button mBtnStart;
    private Button mBtnSettings;
    private Text mTxtVersion;

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

    protected override void OnWillShow()
    {
        // 显示动画开始前
        AudioKit.Play(""UI/MenuAppear"");
    }

    protected override void OnDidShow()
    {
        // 显示动画完成后
        mBtnStart.Select(); // 设置默认焦点
    }

    protected override void OnFocus()
    {
        // 获得焦点时
        base.OnFocus();
        InputSystem.EnableUIInput();
    }

    protected override void OnBlur()
    {
        // 失去焦点时
        base.OnBlur();
        InputSystem.DisableUIInput();
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
                        Explanation = "UIPanel 继承自 MonoBehaviour，但业务逻辑应尽量与 Unity 生命周期解耦。"
                    },
                    new()
                    {
                        Title = "面板数据传递",
                        Code = @"// 定义数据类
public class GameOverData : IUIData
{
    public int Score;
    public int HighScore;
    public bool IsNewRecord;
}

// 面板中使用数据
public class GameOverPanel : UIPanel
{
    private Text mTxtScore;
    private Text mTxtHighScore;
    private GameObject mNewRecordEffect;

    protected override void OnOpen(IUIData data = null)
    {
        if (data is GameOverData gameOverData)
        {
            mTxtScore.text = $""得分: {gameOverData.Score}"";
            mTxtHighScore.text = $""最高分: {gameOverData.HighScore}"";
            mNewRecordEffect.SetActive(gameOverData.IsNewRecord);
        }
    }
}

// 打开面板并传递数据
var data = new GameOverData 
{ 
    Score = 1000, 
    HighScore = 1500, 
    IsNewRecord = false 
};
UIKit.OpenPanel<GameOverPanel>(UILevel.Pop, data);",
                        Explanation = "通过 IUIData 接口传递数据，保持面板与数据源解耦。"
                    },
                    new()
                    {
                        Title = "配置面板动画",
                        Code = @"public class AnimatedPanel : UIPanel
{
    protected override void Awake()
    {
        base.Awake();
        
        // 代码配置动画
        SetShowAnimation(UIAnimationFactory.CreateParallel(
            UIAnimationFactory.CreateFadeIn(0.3f),
            UIAnimationFactory.CreateScaleIn(0.3f)
        ));
        
        SetHideAnimation(UIAnimationFactory.CreateParallel(
            UIAnimationFactory.CreateFadeOut(0.2f),
            UIAnimationFactory.CreateScaleOut(0.2f)
        ));
    }

    // 或者在 Inspector 中配置：
    // [SerializeField] UIAnimationConfig mShowAnimationConfig;
    // [SerializeField] UIAnimationConfig mHideAnimationConfig;
}",
                        Explanation = "动画可以通过代码或 Inspector 配置。"
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
        // 设置默认焦点元素（手柄模式下自动聚焦）
        SetDefaultSelectable(mBtnGraphics);
    }
}

// 或者动态设置
protected override void OnDidShow()
{
    if (UIKit.IsNavigationMode)
    {
        UIKit.SetFocus(mBtnGraphics);
    }
}",
                        Explanation = "设置默认焦点可以改善手柄/键盘导航体验。"
                    },
                    new()
                    {
                        Title = "UIElement 和 UIComponent",
                        Code = @"// UIElement - 轻量级 UI 元素基类
// 适合：列表项、小型可复用组件
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

// UIComponent - 带生命周期的 UI 组件
// 适合：需要初始化/清理的复杂组件
public class PlayerCard : UIComponent
{
    protected override void OnInit()
    {
        // 初始化
    }

    protected override void OnShow()
    {
        // 显示时
    }

    protected override void OnHide()
    {
        // 隐藏时
    }
}",
                        Explanation = "UIElement 和 UIComponent 适合创建可复用的 UI 组件。"
                    }
                }
            };
        }
    }
}
#endif
