using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    public class PanelHandler : IPoolable
    {
        /// <summary>
        /// UI类
        /// </summary>
        public Type Type;
        /// <summary>
        /// UI层级
        /// </summary>
        public UILevel Level = UILevel.Common;
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

        public bool IsRecycled { get; set; }

        public static PanelHandler Allocate() => SafePoolKit<PanelHandler>.Instance.Allocate();

        public void Recycle() => SafePoolKit<PanelHandler>.Instance.Recycle(this);

        void IPoolable.OnRecycled()
        {
            Type = null;
            Level = UILevel.Common;
            OnStack = null;
            Prefab = null;
            Panel = null;
            Data = null;
            Loader.UnLoadAndRecycle();
            Loader = null;
        }
    }
}