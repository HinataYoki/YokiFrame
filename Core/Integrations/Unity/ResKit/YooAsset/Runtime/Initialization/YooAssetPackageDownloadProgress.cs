#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 单个 YooAsset package 的下载进度。
    /// 字段与 YooAsset 下载器一致；多包总进度由 <see cref="YooAssetDownloadProgress"/> 单独提供。
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

    /// <summary>接收单个 package 的下载进度。</summary>
    public delegate void YooAssetPackageDownloadProgressHandler(YooAssetPackageDownloadProgress progress);

    /// <summary>
    /// 一次多包下载会话的总进度。
    /// 进度按 package 数量等权累计，不使用尚未开始的后续包字节重算，因此不会因后一个包更大而倒退。
    /// 文件数和字节数只累计已经创建下载器的 package，不是全部包的最终总量。
    /// </summary>
    public readonly struct YooAssetDownloadProgress
    {
        /// <summary>创建多包总进度。</summary>
        /// <param name="currentPackageName">当前正在下载或刚刚完成的 package。</param>
        /// <param name="packageIndex">当前 package 的 0 基下标。</param>
        /// <param name="packageCount">本次会话的 package 数。</param>
        /// <param name="progress">按 package 等权累计的 0 到 1 总进度。</param>
        /// <param name="totalDownloadCount">已创建下载器的文件数合计。</param>
        /// <param name="currentDownloadCount">已完成文件数合计。</param>
        /// <param name="totalDownloadBytes">已创建下载器的字节数合计。</param>
        /// <param name="currentDownloadBytes">已完成字节数合计。</param>
        /// <param name="completedPackageCount">已经完成的 package 数。</param>
        public YooAssetDownloadProgress(
            string currentPackageName,
            int packageIndex,
            int packageCount,
            float progress,
            int totalDownloadCount,
            int currentDownloadCount,
            long totalDownloadBytes,
            long currentDownloadBytes,
            int completedPackageCount)
        {
            CurrentPackageName = currentPackageName ?? string.Empty;
            PackageIndex = packageIndex;
            PackageCount = packageCount;
            Progress = progress;
            TotalDownloadCount = totalDownloadCount;
            CurrentDownloadCount = currentDownloadCount;
            TotalDownloadBytes = totalDownloadBytes;
            CurrentDownloadBytes = currentDownloadBytes;
            CompletedPackageCount = completedPackageCount;
        }

        /// <summary>获取当前 package 名称。</summary>
        public string CurrentPackageName { get; }

        /// <summary>获取当前 package 的 0 基下标。</summary>
        public int PackageIndex { get; }

        /// <summary>获取本次会话的 package 数。</summary>
        public int PackageCount { get; }

        /// <summary>获取按 package 等权累计的 0 到 1 总进度。</summary>
        public float Progress { get; }

        /// <summary>获取已创建下载器的文件数合计。</summary>
        public int TotalDownloadCount { get; }

        /// <summary>获取已完成文件数合计。</summary>
        public int CurrentDownloadCount { get; }

        /// <summary>获取已创建下载器的字节数合计。</summary>
        public long TotalDownloadBytes { get; }

        /// <summary>获取已完成字节数合计。</summary>
        public long CurrentDownloadBytes { get; }

        /// <summary>获取已经完成的 package 数。</summary>
        public int CompletedPackageCount { get; }
    }

    /// <summary>接收一次多包下载会话的总进度。</summary>
    public delegate void YooAssetDownloadProgressHandler(YooAssetDownloadProgress progress);

    /// <summary>接收单个 package 的下载文件错误。</summary>
    public delegate void YooAssetPackageDownloadErrorHandler(YooAssetPackageDownloadError error);

    /// <summary>接收单个 package 开始下载某个文件的通知。</summary>
    public delegate void YooAssetPackageDownloadFileHandler(YooAssetPackageDownloadFile file);
}
#endif
