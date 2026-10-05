#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 单个 YooAsset package 的下载进度。
    /// 字段与 YooAsset 下载器一致，不合成多个 package 的总进度。
    /// </summary>
    public readonly struct YooAssetPackageDownloadProgress
    {
        /// <summary>创建单个 package 的下载进度。</summary>
        /// <param name="packageName">package 名称。</param>
        /// <param name="progress">当前下载器的 0 到 1 进度。</param>
        /// <param name="totalDownloadCount">需要下载的文件数。</param>
        /// <param name="currentDownloadCount">已经完成的文件数。</param>
        /// <param name="totalDownloadBytes">需要下载的字节数。</param>
        /// <param name="currentDownloadBytes">已经完成的字节数。</param>
        public YooAssetPackageDownloadProgress(
            string packageName,
            float progress,
            int totalDownloadCount,
            int currentDownloadCount,
            long totalDownloadBytes,
            long currentDownloadBytes)
        {
            PackageName = packageName ?? string.Empty;
            Progress = progress;
            TotalDownloadCount = totalDownloadCount;
            CurrentDownloadCount = currentDownloadCount;
            TotalDownloadBytes = totalDownloadBytes;
            CurrentDownloadBytes = currentDownloadBytes;
        }

        /// <summary>获取 package 名称。</summary>
        public string PackageName { get; }

        /// <summary>获取当前下载器的 0 到 1 进度。</summary>
        public float Progress { get; }

        /// <summary>获取需要下载的文件数。</summary>
        public int TotalDownloadCount { get; }

        /// <summary>获取已经完成的文件数。</summary>
        public int CurrentDownloadCount { get; }

        /// <summary>获取需要下载的字节数。</summary>
        public long TotalDownloadBytes { get; }

        /// <summary>获取已经完成的字节数。</summary>
        public long CurrentDownloadBytes { get; }
    }

    /// <summary>单个 package 下载失败时的文件错误。</summary>
    public readonly struct YooAssetPackageDownloadError
    {
        /// <summary>创建下载错误。</summary>
        /// <param name="packageName">package 名称。</param>
        /// <param name="fileName">失败文件名。</param>
        /// <param name="error">YooAsset 返回的错误信息。</param>
        public YooAssetPackageDownloadError(string packageName, string fileName, string error)
        {
            PackageName = packageName ?? string.Empty;
            FileName = fileName ?? string.Empty;
            Error = error ?? string.Empty;
        }

        /// <summary>获取 package 名称。</summary>
        public string PackageName { get; }

        /// <summary>获取失败文件名。</summary>
        public string FileName { get; }

        /// <summary>获取错误信息。</summary>
        public string Error { get; }
    }

    /// <summary>单个 package 开始下载某个文件时的通知。</summary>
    public readonly struct YooAssetPackageDownloadFile
    {
        /// <summary>创建文件开始通知。</summary>
        /// <param name="packageName">package 名称。</param>
        /// <param name="fileName">文件名。</param>
        /// <param name="fileSize">文件字节数。</param>
        public YooAssetPackageDownloadFile(string packageName, string fileName, long fileSize)
        {
            PackageName = packageName ?? string.Empty;
            FileName = fileName ?? string.Empty;
            FileSize = fileSize;
        }

        /// <summary>获取 package 名称。</summary>
        public string PackageName { get; }

        /// <summary>获取文件名。</summary>
        public string FileName { get; }

        /// <summary>获取文件字节数。</summary>
        public long FileSize { get; }
    }

    /// <summary>接收单个 package 的下载进度。多包由调用方自行累计。</summary>
    public delegate void YooAssetPackageDownloadProgressHandler(YooAssetPackageDownloadProgress progress);

    /// <summary>接收单个 package 的下载文件错误。</summary>
    public delegate void YooAssetPackageDownloadErrorHandler(YooAssetPackageDownloadError error);

    /// <summary>接收单个 package 开始下载某个文件的通知。</summary>
    public delegate void YooAssetPackageDownloadFileHandler(YooAssetPackageDownloadFile file);
}
#endif
