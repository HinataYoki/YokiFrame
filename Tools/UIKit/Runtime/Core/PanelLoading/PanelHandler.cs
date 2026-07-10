#if !GODOT
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    internal enum PanelRootCloseState
    {
        None,
        Pending,
        Finalizing,
        DestroyedFinalizing,
        Finalized
    }

    public class PanelHandler : IPoolable
    {
        /// <summary>
        /// UI类
        /// </summary>
        public Type Type;
        /// <summary>
        /// UI层级
        /// </summary>
        public UILevel Level;
        /// <summary>
        /// 预制体
        /// </summary>
        public GameObject Prefab;
        /// <summary>
        /// 界面引用
        /// </summary>
        public IPanel Panel;
        /// <summary>
        /// UI数据
        /// </summary>
        public IUIData Data;
        /// <summary>
        /// 加载该UI的加载器
        /// </summary>
        public IPanelLoader Loader;
        /// <summary>
        /// 在栈上的位置
        /// </summary>
        public LinkedListNode<IPanel> OnStack;
        /// <summary>
        /// 热度
        /// </summary>
        public int Hot = 0;
        
        /// <summary>
        /// 所在栈名称
        /// </summary>
        public string StackName = "main";
        
        /// <summary>
        /// 子层级（用于同层级内的排序）
        /// </summary>
        public int SubLevel = 0;
        
        /// <summary>
        /// 是否为模态面板
        /// </summary>
        public bool IsModal = false;
        
        /// <summary>
        /// 打开时间戳
        /// </summary>
        public long OpenTimestamp = 0;
        
        /// <summary>
        /// 缓存模式
        /// </summary>
        public PanelCacheMode CacheMode = PanelCacheMode.Hot;

        /// <summary>
        /// 面板标签，用于批量关闭
        /// </summary>
        public string Tag = null;

        /// <summary>
        /// 当前打开轮次的 UIRoot 关闭阶段。
        /// </summary>
        internal PanelRootCloseState RootCloseState;

        /// <summary>
        /// 区分同一 Handler 上先后发生的根关闭轮次。
        /// </summary>
        internal int RootCloseVersion;

        public bool IsRecycled { get; set; }

        public static PanelHandler Allocate() => SafePoolKit<PanelHandler>.Instance.Allocate();

        public void Recycle() => SafePoolKit<PanelHandler>.Instance.Recycle(this);

        /// <summary>
        /// 解除面板所有权并重置池化字段。
        /// </summary>
        void IPoolable.OnRecycled()
        {
            var panel = Panel;
            if (panel != default && ReferenceEquals(panel.Handler, this))
            {
                panel.Handler = null;
            }

            Type = null;
            Level = default;
            OnStack = null;
            Prefab = null;
            Panel = null;
            Data = null;
            Hot = 0;
            if (Loader != default) Loader.UnLoadAndRecycle();
            Loader = null;
            StackName = "main";
            SubLevel = 0;
            IsModal = false;
            OpenTimestamp = 0;
            CacheMode = PanelCacheMode.Hot;
            Tag = null;
            RootCloseState = PanelRootCloseState.None;
            RootCloseVersion = 0;
        }
    }
}
#endif
