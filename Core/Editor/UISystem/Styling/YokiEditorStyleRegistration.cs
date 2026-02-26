#if UNITY_EDITOR
using YokiFrame.EditorTools;

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
    "Kits/EventKit/EventKit.uss",
    priority: 10
)]

[assembly: YokiEditorStyle(
    "FsmKit",
    "Kits/FsmKit/FsmKit.uss",
    priority: 20
)]

[assembly: YokiEditorStyle(
    "PoolKit",
    "Kits/PoolKit/PoolKit.uss",
    priority: 30
)]

[assembly: YokiEditorStyle(
    "ResKit",
    "Kits/ResKit/ResKit.uss",
    priority: 40
)]

// ═══════════════════════════════════════════════════════════════════
// Tools 层 Kit 样式注册
// ═══════════════════════════════════════════════════════════════════

[assembly: YokiEditorStyle(
    "ActionKit",
    "Kits/ActionKit/ActionKit.uss",
    priority: 100
)]

[assembly: YokiEditorStyle(
    "AudioKit",
    "Kits/AudioKit/AudioKit.uss",
    priority: 110
)]

[assembly: YokiEditorStyle(
    "UIKit",
    "Kits/UIKit/UIKit.uss",
    priority: 120
)]

[assembly: YokiEditorStyle(
    "BuffKit",
    "Kits/BuffKit/BuffKit.uss",
    priority: 130
)]

[assembly: YokiEditorStyle(
    "LocalizationKit",
    "Kits/LocalizationKit/LocalizationKit.uss",
    priority: 140
)]

[assembly: YokiEditorStyle(
    "SaveKit",
    "Kits/SaveKit/SaveKit.uss",
    priority: 150
)]

[assembly: YokiEditorStyle(
    "SceneKit",
    "Kits/SceneKit/SceneKit.uss",
    priority: 160
)]

[assembly: YokiEditorStyle(
    "SpatialKit",
    "Kits/SpatialKit/SpatialKit.uss",
    priority: 170
)]
#endif
