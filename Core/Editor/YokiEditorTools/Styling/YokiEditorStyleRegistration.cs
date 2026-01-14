#if UNITY_EDITOR
using YokiFrame.EditorTools;

// ═══════════════════════════════════════════════════════════════════
// Kit 样式注册
// 
// 使用 [YokiEditorStyle] 特性显式声明 Kit 的样式表路径。
// AI 可通过搜索 [YokiEditorStyle( 找到所有 Kit 样式入口。
// ═══════════════════════════════════════════════════════════════════

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
#endif
