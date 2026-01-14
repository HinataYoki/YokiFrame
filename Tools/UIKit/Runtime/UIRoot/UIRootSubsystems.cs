using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// UIRoot 子系统配置
    /// </summary>
    [Serializable]
    public class UIRootConfig
    {
        [Header("缓存配置")]
        [Tooltip("缓存容量")]
        public int CacheCapacity = 10;

        [Tooltip("创建界面时赋予的热度值")]
        public int OpenHot = 3;

        [Tooltip("获取界面时赋予的热度值")]
        public int GetHot = 2;

        [Tooltip("每次行为造成的衰减热度值")]
        public int Weaken = 1;

        [Header("焦点配置")]
        [Tooltip("是否启用焦点系统")]
        public bool EnableFocusSystem = false;

        [Tooltip("是否启用手柄支持")]
        public bool EnableGamepad = false;

        [Tooltip("手柄配置资源")]
        public GamepadConfig GamepadConfig;

        [Header("对话框配置")]
        [Tooltip("对话框显示层级")]
        public UILevel DialogLevel = UILevel.Pop;
    }

    /// <summary>
    /// 面板缓存数据
    /// </summary>
    internal class PanelCacheData
    {
        public PanelHandler Handler;
        public long AccessTimestamp;
        public bool IsPreloaded;
    }

    /// <summary>
    /// 面板缓存模式
    /// </summary>
    public enum PanelCacheMode
    {
        /// <summary>热度模式 - 根据热度值决定是否销毁</summary>
        Hot,
        /// <summary>常驻模式 - 不会被自动销毁</summary>
        Persistent,
        /// <summary>临时模式 - 关闭后立即销毁</summary>
        Temporary
    }

    /// <summary>
    /// UI 输入模式
    /// </summary>
    public enum UIInputMode
    {
        /// <summary>指针模式（鼠标/触摸）</summary>
        Pointer,
        /// <summary>导航模式（键盘/手柄）</summary>
        Navigation
    }

    /// <summary>
    /// 对话框队列项
    /// </summary>
    internal class DialogQueueItem
    {
        public Type PanelType;
        public DialogConfig Config;
        public Action<DialogResultData> OnResult;
        public UILevel Level;
    }
}
