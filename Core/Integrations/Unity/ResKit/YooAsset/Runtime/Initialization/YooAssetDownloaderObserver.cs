#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using YooAsset;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 把 YooAsset V2/V3 下载器事件转发到不依赖 YooAsset 类型的回调。
    /// 没有实际下载内容或没有回调时不订阅，避免空下载器产生无意义通知。
    /// </summary>
    internal static class YooAssetDownloaderObserver
    {
        /// <summary>为即将启动的下载器挂上进度、错误和文件开始回调。</summary>
        /// <param name="downloader">已经统计完下载量、尚未开始的下载器。</param>
        /// <param name="onProgress">进度回调，可为空。</param>
        /// <param name="onError">错误回调，可为空。</param>
        /// <param name="onFileBegin">文件开始回调，可为空。</param>
        public static void Attach(
            ResourceDownloaderOperation downloader,
            YooAssetPackageDownloadProgressHandler onProgress,
            YooAssetPackageDownloadErrorHandler onError,
            YooAssetPackageDownloadFileHandler onFileBegin)
        {
            if (downloader == null || downloader.TotalDownloadCount <= 0)
                return;

            AttachProgress(downloader, onProgress);
            AttachError(downloader, onError);
            AttachFileBegin(downloader, onFileBegin);
        }

        /// <summary>转发下载进度。V3 使用事件，V2 使用旧式回调字段。</summary>
        private static void AttachProgress(
            ResourceDownloaderOperation downloader,
            YooAssetPackageDownloadProgressHandler onProgress)
        {
            if (onProgress == null)
                return;

#if YOKIFRAME_YOOASSET_3
            downloader.DownloadProgressChanged += args => onProgress(CreateProgress(
                args.PackageName,
                args.Progress,
                args.TotalDownloadCount,
                args.CurrentDownloadCount,
                args.TotalDownloadBytes,
                args.CurrentDownloadBytes));
#else
            downloader.DownloadUpdateCallback = data => onProgress(CreateProgress(
                data.PackageName,
                data.Progress,
                data.TotalDownloadCount,
                data.CurrentDownloadCount,
                data.TotalDownloadBytes,
                data.CurrentDownloadBytes));
#endif
        }

        /// <summary>转发单个文件下载失败。</summary>
        private static void AttachError(
            ResourceDownloaderOperation downloader,
            YooAssetPackageDownloadErrorHandler onError)
        {
            if (onError == null)
                return;

#if YOKIFRAME_YOOASSET_3
            downloader.DownloadError += args => onError(
                new YooAssetPackageDownloadError(args.PackageName, args.FileName, args.ErrorInfo));
#else
            downloader.DownloadErrorCallback = data => onError(
                new YooAssetPackageDownloadError(data.PackageName, data.FileName, data.ErrorInfo));
#endif
        }

        /// <summary>转发单个文件开始下载。</summary>
        private static void AttachFileBegin(
            ResourceDownloaderOperation downloader,
            YooAssetPackageDownloadFileHandler onFileBegin)
        {
            if (onFileBegin == null)
                return;

#if YOKIFRAME_YOOASSET_3
            downloader.DownloadFileStarted += args => onFileBegin(
                new YooAssetPackageDownloadFile(args.PackageName, args.FileName, args.FileSize));
#else
            downloader.DownloadFileBeginCallback = data => onFileBegin(
                new YooAssetPackageDownloadFile(data.PackageName, data.FileName, data.FileSize));
#endif
        }

        /// <summary>把两代下载器的进度字段收成同一结构。</summary>
        private static YooAssetPackageDownloadProgress CreateProgress(
            string packageName,
            float progress,
            int totalDownloadCount,
            int currentDownloadCount,
            long totalDownloadBytes,
            long currentDownloadBytes)
        {
            return new YooAssetPackageDownloadProgress(
                packageName,
                progress,
                totalDownloadCount,
                currentDownloadCount,
                totalDownloadBytes,
                currentDownloadBytes);
        }
    }
}
#endif
