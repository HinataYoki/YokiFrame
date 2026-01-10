#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 响应式 ViewModel
    /// 管理资源列表、选中资源、卸载历史的响应式数据
    /// </summary>
    public sealed class ResKitViewModel : IDisposable
    {
        #region 响应式属性

        /// <summary>
        /// 已加载资源列表
        /// </summary>
        public ReactiveCollection<ResDebugger.ResInfo> LoadedAssets { get; } = new(64);

        /// <summary>
        /// 当前选中的资源
        /// </summary>
        public ReactiveProperty<ResDebugger.ResInfo?> SelectedAsset { get; } = new();

        /// <summary>
        /// 卸载历史记录
        /// </summary>
        public ReactiveCollection<ResDebugger.UnloadRecord> UnloadHistory { get; } = new(64);

        #endregion

        #region 统计数据

        /// <summary>
        /// 已加载资源数量
        /// </summary>
        public ReactiveProperty<int> LoadedCount { get; } = new(0);

        /// <summary>
        /// 总引用计数
        /// </summary>
        public ReactiveProperty<int> TotalRefCount { get; } = new(0);

        /// <summary>
        /// 搜索过滤器
        /// </summary>
        public ReactiveProperty<string> SearchFilter { get; } = new(string.Empty);

        #endregion

        #region 内部字段

        private readonly List<ResDebugger.ResInfo> mTempAssets = new(64);
        private readonly List<ResDebugger.UnloadRecord> mTempHistory = new(64);
        private bool mIsDisposed;

        #endregion

        #region 数据刷新

        /// <summary>
        /// 刷新资源列表数据
        /// </summary>
        public void RefreshAssets()
        {
            if (mIsDisposed) return;

            mTempAssets.Clear();
            mTempAssets.AddRange(ResDebugger.GetLoadedAssets());

            // 检查是否有变化
            bool hasChanges = mTempAssets.Count != LoadedAssets.Count;
            if (!hasChanges)
            {
                for (int i = 0; i < mTempAssets.Count; i++)
                {
                    if (i >= LoadedAssets.Count ||
                        mTempAssets[i].Path != LoadedAssets[i].Path ||
                        mTempAssets[i].RefCount != LoadedAssets[i].RefCount)
                    {
                        hasChanges = true;
                        break;
                    }
                }
            }

            if (hasChanges)
            {
                LoadedAssets.ReplaceAll(mTempAssets);
            }

            // 更新统计数据
            LoadedCount.Value = ResDebugger.GetLoadedCount();
            TotalRefCount.Value = ResDebugger.GetTotalRefCount();

            // 如果选中的资源已不存在，清除选择
            if (SelectedAsset.Value.HasValue)
            {
                var selected = SelectedAsset.Value.Value;
                bool found = false;
                for (int i = 0; i < mTempAssets.Count; i++)
                {
                    if (mTempAssets[i].Path == selected.Path && mTempAssets[i].TypeName == selected.TypeName)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    SelectedAsset.Value = null;
                }
            }
        }

        /// <summary>
        /// 刷新卸载历史
        /// </summary>
        public void RefreshHistory()
        {
            if (mIsDisposed) return;

            var history = ResDebugger.GetUnloadHistory();
            
            // 检查是否有变化
            if (history.Count != UnloadHistory.Count)
            {
                mTempHistory.Clear();
                mTempHistory.AddRange(history);
                UnloadHistory.ReplaceAll(mTempHistory);
            }
        }

        #endregion

        #region 操作方法

        /// <summary>
        /// 选择资源
        /// </summary>
        public void SelectAsset(ResDebugger.ResInfo asset)
        {
            if (mIsDisposed) return;
            SelectedAsset.Value = asset;
        }

        /// <summary>
        /// 清除选择
        /// </summary>
        public void ClearSelection()
        {
            if (mIsDisposed) return;
            SelectedAsset.Value = null;
        }

        /// <summary>
        /// 设置搜索过滤器
        /// </summary>
        public void SetSearchFilter(string filter)
        {
            if (mIsDisposed) return;
            SearchFilter.Value = filter?.ToLowerInvariant() ?? string.Empty;
        }

        /// <summary>
        /// 清空卸载历史
        /// </summary>
        public void ClearHistory()
        {
            if (mIsDisposed) return;
            ResDebugger.ClearUnloadHistory();
            UnloadHistory.Clear();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (mIsDisposed) return;
            mIsDisposed = true;

            LoadedAssets.Dispose();
            SelectedAsset.Dispose();
            UnloadHistory.Dispose();
            LoadedCount.Dispose();
            TotalRefCount.Dispose();
            SearchFilter.Dispose();

            mTempAssets.Clear();
            mTempHistory.Clear();
        }

        #endregion
    }
}
#endif
