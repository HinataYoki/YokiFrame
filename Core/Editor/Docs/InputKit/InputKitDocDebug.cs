#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// InputKit 调试工具文档
    /// </summary>
    internal static class InputKitDocDebug
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "调试工具",
                Description = "InputKit 提供输入录制、可视化和模拟工具，用于调试和自动化测试。",
                CodeExamples = new List<CodeExample>
                {
                    CreateRecorderExample(),
                    CreateVisualizerExample(),
                    CreateSimulatorExample()
                }
            };
        }

        private static CodeExample CreateRecorderExample()
        {
            return new CodeExample
            {
                Title = "输入录制器",
                Code = @"// 创建录制器
var recorder = new InputRecorder();

// 开始录制
recorder.StartRecording();

// 在输入回调中记录
void OnAttackPressed()
{
    recorder.RecordFrame(""Attack"", InputRecordType.ButtonDown);
}

void OnMoveInput(Vector2 value)
{
    recorder.RecordFrame(""Move"", InputRecordType.Vector2, value);
}

// 停止录制
recorder.StopRecording();
Debug.Log($""录制了 {recorder.FrameCount} 帧，时长 {recorder.Duration}s"");

// 导出为 JSON
string json = recorder.ExportToJson();
File.WriteAllText(""recording.json"", json);

// 从 JSON 导入
recorder.ImportFromJson(File.ReadAllText(""recording.json""));

// 回放录制
recorder.StartPlayback(frame =>
{
    switch (frame.Type)
    {
        case InputRecordType.ButtonDown:
            InputKit.ProcessComboTap(frame.ActionName);
            break;
        case InputRecordType.Vector2:
            Player.Move(frame.Value);
            break;
    }
});

// 在 Update 中更新回放
void Update()
{
    recorder.UpdatePlayback();
}",
                Explanation = "InputRecorder 支持录制和回放输入序列，可导出为 JSON 用于自动化测试。"
            };
        }

        private static CodeExample CreateVisualizerExample()
        {
            return new CodeExample
            {
                Title = "输入可视化器",
                Code = @"// 添加 InputVisualizer 组件到场景
// 或通过代码创建
var go = new GameObject(""InputVisualizer"");
var visualizer = go.AddComponent<InputVisualizer>();

// 配置显示
visualizer.ShowOnScreen = true;

// 在输入回调中记录
void OnAnyInput(string action, string value)
{
    visualizer.LogInput(action, value, isActive: true);
}

// 示例：记录各种输入
void Update()
{
    var move = InputKit.GetAxis2D(""Move"");
    if (move.sqrMagnitude > 0.01f)
    {
        visualizer.LogInput(""Move"", $""({move.x:F2}, {move.y:F2})"");
    }
    
    if (InputKit.GetButtonDown(""Attack""))
    {
        visualizer.LogInput(""Attack"", ""Pressed"", isActive: true);
    }
}

// 清空历史
visualizer.ClearHistory();",
                Explanation = "InputVisualizer 在屏幕上显示输入历史，方便调试输入问题。"
            };
        }

        private static CodeExample CreateSimulatorExample()
        {
            return new CodeExample
            {
                Title = "输入模拟器",
                Code = @"// 创建模拟器
var simulator = new InputSimulator();

// 模拟按钮按下/抬起
simulator.SimulateButtonDown(""Attack"");
simulator.SimulateButtonUp(""Attack"");

// 模拟点击（按下后自动抬起）
simulator.SimulateButtonClick(""Jump"");

// 模拟轴输入
simulator.SimulateAxis(""Horizontal"", 1f);
simulator.SimulateAxis(""Vertical"", 0.5f);

// 模拟方向输入（带持续时间）
simulator.SimulateDirection(new Vector2(1, 0), duration: 0.5f);

// 调度延迟输入
simulator.ScheduleInput(0.5f, () => simulator.SimulateButtonClick(""Attack""));

// 调度按钮序列（用于测试连招）
simulator.ScheduleButtonSequence(
    new[] { ""Attack"", ""Attack"", ""Special"" },
    interval: 0.15f
);

// 查询模拟状态
if (simulator.IsButtonSimulated(""Attack""))
{
    // 按钮正在被模拟按下
}

float axis = simulator.GetSimulatedAxis(""Horizontal"");

// 在 Update 中更新模拟器
void Update()
{
    simulator.Update();
}

// 清空所有模拟状态
simulator.Clear();",
                Explanation = "InputSimulator 用于自动化测试，可模拟各种输入序列。"
            };
        }
    }
}
#endif
