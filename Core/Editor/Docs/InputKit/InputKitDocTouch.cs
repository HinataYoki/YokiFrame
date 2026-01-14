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
                Description = "虚拟摇杆、虚拟按钮和手势识别，支持移动端。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "虚拟摇杆",
                        Code = @"// 注册
InputKit.RegisterJoystick(""Move"", joystickComponent);

// 读取
Vector2 input = InputKit.GetJoystickInput(""Move"");
bool active = InputKit.IsJoystickActive(""Move"");

// 配置
var joystick = InputKit.GetJoystick(""Move"");
joystick.DeadZone = 0.15f;

// 注销
InputKit.UnregisterJoystick(""Move"");",
                        Explanation = "VirtualJoystick 组件处理触摸，通过 InputKit 统一访问。"
                    },
                    new()
                    {
                        Title = "虚拟按钮",
                        Code = @"// 注册
InputKit.RegisterButton(""Attack"", buttonComponent);

// 查询
bool pressed = InputKit.IsVirtualButtonPressed(""Attack"");
float holdTime = InputKit.GetVirtualButtonHoldDuration(""Attack"");

// 事件
button.OnPressed += () => Attack();
button.OnLongPress += () => ChargeAttack();

// 注销
InputKit.UnregisterButton(""Attack"");",
                        Explanation = "支持点击、长按、按住时长检测。"
                    },
                    new()
                    {
                        Title = "手势识别",
                        Code = @"var recognizer = new GestureRecognizer();
InputKit.SetGestureRecognizer(recognizer);

recognizer.OnSwipe += (dir, velocity) => Player.Dodge(dir);
recognizer.OnPinch += (scale, center) => Camera.Zoom(scale);
recognizer.OnTap += (pos, count) => { if (count == 2) Interact(); };

// Update 中更新
InputKit.GestureRecognizer?.Update();",
                        Explanation = "支持滑动、缩放、旋转、点击手势。"
                    }
                }
            };
        }
    }
}
#endif
