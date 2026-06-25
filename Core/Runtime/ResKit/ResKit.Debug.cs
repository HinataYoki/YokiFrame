using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public static partial class ResKit
    {
        /// <summary>
        /// 填充当前已加载资源的调试信息列表。
        /// </summary>
        /// <param name="result">接收结果的列表；方法会先清空该列表。</param>
        public static void GetLoadedAssets(List<ResDebugInfo> result)
        {
            if (result == null)
                return;

            result.Clear();
            lock (sLock)
            {
                foreach (var kvp in sAssetCache)
                {
                    var handle = kvp.Value as IResHandleDebugView;
                    if (handle == null)
                        continue;

                    result.Add(new ResDebugInfo
                    {
                        Path = handle.Path,
                        TypeName = handle.AssetType != null ? handle.AssetType.Name : "Unknown",
                        RefCount = handle.RefCount,
                        IsDone = handle.IsDone,
                        ProviderName = handle.ProviderName,
                        Source = handle.Source,
                        SourceFile = handle.SourceFile,
                        SourceLine = handle.SourceLine
                    });
                }
            }
        }

        /// <summary>
        /// 填充资源卸载历史列表。
        /// </summary>
        /// <param name="result">接收结果的列表；方法会先清空该列表。</param>
        public static void GetUnloadHistory(List<ResUnloadRecord> result)
        {
            if (result == null)
                return;

            result.Clear();
            lock (sLock)
            {
                var records = sUnloadHistory.ToArray();
                for (var i = records.Length - 1; i >= 0; i--)
                    result.Add(records[i]);
            }
        }

        /// <summary>
        /// 清空资源卸载历史。
        /// </summary>
        public static void ClearUnloadHistory()
        {
            lock (sLock)
                sUnloadHistory.Clear();
        }
    }
}
