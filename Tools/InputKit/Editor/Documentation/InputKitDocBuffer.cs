#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// InputKit 输入缓冲文档
    /// </summary>
    internal static class InputKitDocBuffer
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "输入缓冲",
                Description = "允许玩家提前输入，提升操作手感和连招流畅度。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基础用法",
                        Code = @"var input = InputKit.Get<GameAppInput>();

// 设置缓冲窗口（毫秒，推荐 100-200）
InputKit.SetBufferWindow(150f);

void Update()
{
    // 检查并消费缓冲输入
    if (mCanAttack && InputKit.ConsumeBufferedInput(input.Player.Attack))
    {
        PerformAttack();
    }
}

// 清空缓冲
InputKit.ClearBuffer();
InputKit.ClearBuffer(input.Player.Attack);  // 指定 Action",
                        Explanation = "ConsumeBufferedInput 检查并移除缓冲，返回是否存在。"
                    },
                    new()
                    {
                        Title = "连击示例",
                        Code = @"public class PlayerCombat : MonoBehaviour
{
    private GameAppInput mInput;
    private bool mCanAttack = true;
    private int mComboCount;
    
    void Start()
    {
        mInput = InputKit.Get<GameAppInput>();
        InputKit.SetBufferWindow(200f);
    }
    
    void Update()
    {
        if (mCanAttack && InputKit.ConsumeBufferedInput(mInput.Player.Attack))
        {
            mCanAttack = false;
            mComboCount++;
            mAnimator.Play($""Attack_{mComboCount}"");
        }
    }
    
    // 动画事件
    void OnAttackCanCancel() => mCanAttack = true;
    void OnAttackEnd()
    {
        mCanAttack = true;
        if (!InputKit.HasBufferedInput(mInput.Player.Attack))
            mComboCount = 0;
    }
}",
                        Explanation = "缓冲让玩家可在动画结束前输入下一次攻击。"
                    }
                }
            };
        }
    }
}
#endif
