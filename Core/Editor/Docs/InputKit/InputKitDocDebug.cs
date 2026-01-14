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
                Description = "输入录制、可视化和模拟，用于调试和自动化测试。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "输入录制",
                        Code = @"var recorder = new InputRecorder();

// 录制
recorder.StartRecording();
recorder.RecordFrame(""Attack"", InputRecordType.ButtonDown);
recorder.RecordFrame(""Move"", InputRecordType.Vector2, moveValue);
recorder.StopRecording();

// 导出/导入
string json = recorder.ExportToJson();
recorder.ImportFromJson(json);

// 回放
recorder.StartPlayback(frame => ProcessFrame(frame));
recorder.UpdatePlayback();  // Update 中调用",
                        Explanation = "录制和回放输入序列，可导出 JSON 用于自动化测试。"
                    },
                    new()
                    {
                        Title = "输入可视化",
                        Code = @"var visualizer = go.AddComponent<InputVisualizer>();
visualizer.ShowOnScreen = true;

// 记录输入
visualizer.LogInput(""Attack"", ""Pressed"", isActive: true);
visualizer.LogInput(""Move"", $""({move.x:F2}, {move.y:F2})"");

visualizer.ClearHistory();",
                        Explanation = "屏幕显示输入历史，方便调试。"
                    },
                    new()
                    {
                        Title = "输入模拟",
                        Code = @"var simulator = new InputSimulator();

// 模拟按钮
simulator.SimulateButtonClick(""Attack"");
simulator.SimulateButtonDown(""Jump"");
simulator.SimulateButtonUp(""Jump"");

// 模拟轴
simulator.SimulateAxis(""Horizontal"", 1f);

// 调度序列（测试连招）
simulator.ScheduleButtonSequence(
    new[] { ""Attack"", ""Attack"", ""Special"" },
    interval: 0.15f);

simulator.Update();  // Update 中调用
simulator.Clear();",
                        Explanation = "用于自动化测试，模拟各种输入序列。"
                    }
                }
            };
        }
    }
}
#endif
