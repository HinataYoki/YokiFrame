#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Architecture 模块中的编辑器边界章节。
    /// </summary>
    internal static class ArchitectureDocEditor
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "编辑器边界",
                Description = "Core.Editor 只负责共享编辑器骨架。具体编辑器界面、通道语义以及 Kit 专属监控行为，应当留在各自 Kit 的 Editor 代码中。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "分层规则",
                        Code = @"Core/Runtime
  - 基础运行时契约与低层工具

Core/Editor
  - 页面骨架
  - 文档注册中心
  - 编辑器通信骨架
  - Bridge 基类

Tools/<Kit>/Runtime
  - 可拆卸的 Kit 运行时代码

Tools/<Kit>/Editor
  - 由 Kit 自己维护的页面、窗口、桥接实现、Provider 与文档模块",
                        Explanation = "Core 提供可复用壳层，Kit 自己拥有自己的编辑器语义，并且可以独立移除。"
                    },
                    new()
                    {
                        Title = "编辑器通信骨架",
                        Code = @"EditorDataBridge
EditorEventCenter
DataChannels
EditorChannelRegistry
PlayModeEditorBridgeBase
EasyEventSendHookBridgeBase",
                        Explanation = "这些共享类型定义了编辑器通信基础设施，但不会把具体 Kit 行为强行塞进 Core。"
                    },
                    new()
                    {
                        Title = "当前集成方向",
                        Code = @"运行时发布端
  -> Bridge 或反射安全发布器
  -> DataChannels 共享契约
  -> 工具页面订阅
  -> 文档与 Provider 元数据

代表性已收敛 Kit：
  EventKit
  AudioKit
  UIKit
  ActionKit
  BuffKit
  FsmKit
  PoolKit
  ResKit",
                        Explanation = "当前重构方向是在保持 Runtime / Editor 物理隔离的前提下，为编辑器工具建立统一通信契约。"
                    }
                }
            };
        }
    }
}
#endif
