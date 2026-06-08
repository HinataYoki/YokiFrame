#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// NodeKit 节点编辑器使用文档
    /// </summary>
    internal static class NodeKitDocCreateGraph
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "节点图编辑器",
                Description = "NodeKit 提供完整的节点图编辑体验，支持拖拽、搜索、复制粘贴和撤销重做。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "编辑器操作",
                        Code = @"// 打开编辑器
// 方式 1: 双击 NodeGraph 资产
// 方式 2: 菜单 YokiFrame > NodeKit > Node Graph Editor
// 方式 3: 代码打开
NodeGraphWindow.ShowGraph(myGraph);

// 快捷键
// Ctrl+S    保存
// Ctrl+C/V  复制/粘贴节点
// Ctrl+D    复制选中节点
// Delete    删除选中节点/连线
// F2        重命名选中节点
// F         聚焦全部/选中节点
// Ctrl+A    全选/取消全选",
                        Explanation = "编辑器支持标准键盘快捷键，与 Unity 其他编辑器操作习惯一致。"
                    },
                    new()
                    {
                        Title = "节点操作",
                        Code = @"// 添加节点
// 方式 1: 右键空白区域 > Create > 选择节点类型
// 方式 2: 从端口拖出连线到空白区域 > 搜索窗口

// 连接端口
// 从输出端口拖拽到输入端口（类型兼容时高亮）

// 右键节点 > 上下文菜单
// - Rename: 重命名节点
// - Copy/Duplicate: 复制/克隆节点
// - Move To Front: 置顶节点
// - Set As Start Node: 设为起始节点
// - Delete: 删除节点

// 右键连线 > 上下文菜单
// - Add Reroute: 添加重路由点
// - Delete: 删除连线",
                        Explanation = "所有操作支持 Undo/Redo。类型不兼容的端口自动灰显，防止非法连接。"
                    },
                    new()
                    {
                        Title = "网格与吸附",
                        Code = @"// 网格吸附（默认 20px）
// Edit > Preferences > NodeKit > Grid Snap & Snap Size

// 缩放范围
// 滚轮缩放，默认 0.25x ~ 5x

// 自动保存
// 默认开启，可在 Preferences 中关闭",
                        Explanation = "节点移动时自动吸附到网格，按住 Alt 可临时禁用吸附。"
                    }
                }
            };
        }
    }
}
#endif
