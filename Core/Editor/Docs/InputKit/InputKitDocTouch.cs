#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// InputKit 触屏/虚拟控件文档
    /// </summary>
    internal static class InputKitDocTouch
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "触屏/虚拟控件",
                Description = "InputKit 提供虚拟摇杆、虚拟按钮和手势识别，支持移动端触屏操作。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "虚拟摇杆",
                        Code = @"// 在 UI 上创建虚拟摇杆组件
public class MoveJoystick : MonoBehaviour
{
    [SerializeField] private VirtualJoystick mJoystick;
    
    void Start()
    {
        // 注册到 InputKit
        InputKit.RegisterJoystick(""Move"", mJoystick);
    }
    
    void OnDestroy()
    {
        InputKit.UnregisterJoystick(""Move"");
    }
}

// 读取摇杆输入
Vector2 moveInput = InputKit.GetJoystickInput(""Move"");
if (moveInput.sqrMagnitude > 0.01f)
{
    Player.Move(moveInput);
}

// 检查摇杆是否激活（正在触摸）
if (InputKit.IsJoystickActive(""Move""))
{
    // 显示移动方向指示器
}

// 获取摇杆组件进行高级配置
var joystick = InputKit.GetJoystick(""Move"");
if (joystick != default)
{
    joystick.DeadZone = 0.15f;
    joystick.MaxDistance = 100f;
}",
                        Explanation = "VirtualJoystick 组件处理触摸输入，通过 InputKit 统一管理和访问。"
                    },
                    new()
                    {
                        Title = "虚拟按钮",
                        Code = @"// 注册虚拟按钮
public class AttackButton : MonoBehaviour
{
    [SerializeField] private VirtualButton mButton;
    
    void Start()
    {
        InputKit.RegisterButton(""Attack"", mButton);
        
        // 监听按钮事件
        mButton.OnPressed += () => Player.Attack();
        mButton.OnReleased += () => Player.StopAttack();
        mButton.OnLongPress += () => Player.ChargeAttack();
    }
    
    void OnDestroy()
    {
        InputKit.UnregisterButton(""Attack"");
    }
}

// 查询按钮状态
if (InputKit.IsVirtualButtonPressed(""Attack""))
{
    // 按钮正在按下
}

// 获取按住时长（用于蓄力攻击）
float holdTime = InputKit.GetVirtualButtonHoldDuration(""Attack"");
if (holdTime > 1f)
{
    Player.ReleaseChargedAttack();
}

// 获取按钮组件
var button = InputKit.GetVirtualButton(""Dodge"");
if (button != default)
{
    button.LongPressThreshold = 0.5f;
}",
                        Explanation = "VirtualButton 支持点击、长按、按住时长检测等功能。"
                    },
                    new()
                    {
                        Title = "手势识别",
                        Code = @"// 创建手势识别器
var recognizer = new GestureRecognizer();
InputKit.SetGestureRecognizer(recognizer);

// 监听手势事件
recognizer.OnSwipe += (direction, velocity) =>
{
    switch (direction)
    {
        case SwipeDirection.Up:
            Player.Jump();
            break;
        case SwipeDirection.Down:
            Player.Crouch();
            break;
        case SwipeDirection.Left:
        case SwipeDirection.Right:
            Player.Dodge(direction);
            break;
    }
};

recognizer.OnPinch += (scale, center) =>
{
    Camera.main.orthographicSize /= scale;
};

recognizer.OnRotate += (angle, center) =>
{
    // 旋转操作
};

recognizer.OnTap += (position, tapCount) =>
{
    if (tapCount == 2)
    {
        // 双击
        Player.Interact();
    }
};

// 在 Update 中更新手势识别
void Update()
{
    InputKit.GestureRecognizer?.Update();
}",
                        Explanation = "GestureRecognizer 支持滑动、缩放、旋转、点击等常见手势。"
                    },
                    new()
                    {
                        Title = "统一输入处理",
                        Code = @"// 统一处理物理输入和虚拟输入
Vector2 GetMoveInput()
{
    // 优先使用虚拟摇杆（移动端）
    if (InputKit.IsJoystickActive(""Move""))
    {
        return InputKit.GetJoystickInput(""Move"");
    }
    
    // 回退到物理输入（PC/主机）
    return InputKit.GetAxis2D(""Move"");
}

bool GetAttackInput()
{
    // 虚拟按钮或物理按钮
    return InputKit.IsVirtualButtonPressed(""Attack"") 
        || InputKit.GetButton(""Attack"");
}",
                        Explanation = "可以同时支持触屏和物理输入，根据平台或用户偏好切换。"
                    }
                }
            };
        }
    }
}
#endif
