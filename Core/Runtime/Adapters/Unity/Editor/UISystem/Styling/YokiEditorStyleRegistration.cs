#if !GODOT
#if UNITY_EDITOR
using YokiFrame.Unity;

// ═══════════════════════════════════════════════════════════════════
// Kit 样式注册
// 
// 使用 [YokiEditorStyle] 特性显式声明 Kit 的样式表路径。
// AI 可通过搜索 [YokiEditorStyle( 找到所有 Kit 样式入口。
// ═══════════════════════════════════════════════════════════════════

// Core 层通用组件样式
[assembly: YokiEditorStyle(
    "VectorInputs",
    "Core/YokiVectorInputs.uss",
    priority: 5
)]

[assembly: YokiEditorStyle(
    "EventKit",
    "Runtime/Core/EventKit/Editor/Styling/EventKit.uss",
    priority: 10
)]

[assembly: YokiEditorStyle(
    "FsmKit",
    "Runtime/Core/FsmKit/Editor/Styling/FsmKit.uss",
    priority: 20
)]

[assembly: YokiEditorStyle(
    "PoolKit",
    "Runtime/Core/PoolKit/Editor/Styling/PoolKit.uss",
    priority: 30
)]

[assembly: YokiEditorStyle(
    "ResKit",
    "Runtime/Core/ResKit/Editor/Styling/ResKit.uss",
    priority: 40
)]

// ═══════════════════════════════════════════════════════════════════
// Tools 层 Kit 样式注册
// ═══════════════════════════════════════════════════════════════════

[assembly: YokiEditorStyle(
    "ActionKit",
    "Runtime/Tool/ActionKit/Editor/Styling/ActionKit.uss",
    priority: 100
)]

[assembly: YokiEditorStyle(
    "AudioKit",
    "Runtime/Tool/AudioKit/Editor/Styling/AudioKit.uss",
    priority: 110
)]

[assembly: YokiEditorStyle(
    "UIKit",
    "Runtime/Tool/UIKit/Editor/Styling/UIKit.uss",
    priority: 120
)]

[assembly: YokiEditorStyle(
    "LocalizationKit",
    "Runtime/Tool/LocalizationKit/Editor/Styling/LocalizationKit.uss",
    priority: 140
)]

[assembly: YokiEditorStyle(
    "SaveKit",
    "Runtime/Tool/SaveKit/Editor/Styling/SaveKit.uss",
    priority: 150
)]

[assembly: YokiEditorStyle(
    "SceneKit",
    "Runtime/Tool/SceneKit/Editor/Styling/SceneKit.uss",
    priority: 160
)]

[assembly: YokiEditorStyle(
    "SpatialKit",
    "Runtime/Tool/SpatialKit/Editor/Styling/SpatialKit.uss",
    priority: 170
)]
#endif
#endif
