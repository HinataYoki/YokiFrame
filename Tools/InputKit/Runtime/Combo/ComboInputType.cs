namespace YokiFrame
{
    /// <summary>
    /// 连招输入类型
    /// </summary>
    public enum ComboInputType
    {
        /// <summary>短按（按下即触发）</summary>
        Tap,
        
        /// <summary>长按（按住指定时长）</summary>
        Hold,
        
        /// <summary>释放（松开时触发）</summary>
        Release,
        
        /// <summary>方向输入</summary>
        Direction
    }
}
