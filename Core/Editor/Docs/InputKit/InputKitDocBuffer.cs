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
                Title = "输入缓冲系统",
                Description = "动作游戏核心功能，允许玩家提前输入，提升操作手感和连招流畅度。类型安全 API，零 GC 分配。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基础用法（类型安全）",
                        Code = @"private GameAppInput mInput;

void Start()
{
    mInput = InputKit.Get<GameAppInput>();
    
    // 设置缓冲窗口（毫秒）
    // 推荐值：100-200 毫秒
    InputKit.SetBufferWindow(150f);
}

void Update()
{
    // 检查是否有缓冲的攻击输入（类型安全）
    if (mCanAttack && InputKit.HasBufferedInput(mInput.Player.Attack))
    {
        // 消费缓冲输入（从缓冲区移除）
        InputKit.ConsumeBufferedInput(mInput.Player.Attack);
        PerformAttack();
    }
    
    // 查看但不消费（用于预览）
    if (InputKit.PeekBufferedInput(mInput.Player.Dodge, out float timestamp))
    {
        float age = Time.time - timestamp;
        Debug.Log($""缓冲的闪避输入，已存在 {age:F2} 秒"");
    }
}

// 状态切换时清空缓冲区
void OnStateExit()
{
    InputKit.ClearBuffer();
}",
                        Explanation = "直接传入 InputAction 引用，编译时类型检查，无魔法字符串。"
                    },
                    new()
                    {
                        Title = "动作游戏连击示例",
                        Code = @"public class PlayerCombat : MonoBehaviour
{
    private GameAppInput mInput;
    private bool mCanAttack = true;
    private bool mIsAttacking;
    private int mComboCount;
    
    void Start()
    {
        mInput = InputKit.Get<GameAppInput>();
        
        // 设置较长的缓冲窗口以支持连击
        InputKit.SetBufferWindow(200f);
    }
    
    void Update()
    {
        // 可以攻击时检查缓冲
        if (mCanAttack && InputKit.HasBufferedInput(mInput.Player.Attack))
        {
            InputKit.ConsumeBufferedInput(mInput.Player.Attack);
            StartAttack();
        }
        
        // 闪避可以打断攻击
        if (InputKit.HasBufferedInput(mInput.Player.Dodge))
        {
            InputKit.ConsumeBufferedInput(mInput.Player.Dodge);
            CancelAttackAndDodge();
        }
    }
    
    void StartAttack()
    {
        mCanAttack = false;
        mIsAttacking = true;
        mComboCount++;
        mAnimator.Play($""Attack_{mComboCount}"");
    }
    
    // 动画事件：攻击可取消点
    void OnAttackCanCancel() => mCanAttack = true;
    
    // 动画事件：攻击结束
    void OnAttackEnd()
    {
        mCanAttack = true;
        mIsAttacking = false;
        
        // 如果没有后续输入，重置连击
        if (!InputKit.HasBufferedInput(mInput.Player.Attack))
        {
            mComboCount = 0;
        }
    }
    
    void CancelAttackAndDodge()
    {
        mIsAttacking = false;
        mCanAttack = false;
        mComboCount = 0;
    }
}",
                        Explanation = "输入缓冲让玩家可以在攻击动画结束前提前输入下一次攻击，实现流畅连击。"
                    },
                    new()
                    {
                        Title = "缓冲配置",
                        Code = @"// 获取当前缓冲窗口（毫秒）
float window = InputKit.BufferWindow;

// 动态调整缓冲窗口
void SetDifficulty(Difficulty diff)
{
    float bufferWindow = diff switch
    {
        Difficulty.Easy => 250f,    // 更宽松
        Difficulty.Normal => 150f,  // 标准
        Difficulty.Hard => 80f,     // 更严格
        _ => 150f
    };
    InputKit.SetBufferWindow(bufferWindow);
}

// 清空指定 Action 的缓冲
InputKit.ClearBuffer(mInput.Player.Attack);

// 清空所有缓冲
InputKit.ClearBuffer();

// 可选：在 Update 中清理过期项（优化内存）
InputKit.CleanupBuffer();",
                        Explanation = "缓冲窗口可以根据游戏难度或玩家偏好动态调整。"
                    }
                }
            };
        }
    }
}
#endif
