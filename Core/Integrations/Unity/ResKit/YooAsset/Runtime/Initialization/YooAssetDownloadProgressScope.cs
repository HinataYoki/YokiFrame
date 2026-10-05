#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 把顺序执行的多个 package 下载折成一个不倒退的总进度。
    /// 每个 package 占相同份额；后续包的字节总量不会重算前面已经完成的份额。
    /// </summary>
    internal sealed class YooAssetDownloadProgressScope
    {
        private readonly YooAssetDownloadProgressHandler mOnProgress;
        private readonly int mPackageCount;
        private readonly float[] mPackageProgress;
        private readonly bool[] mCompleted;
        private int mCurrentIndex = -1;
        private string mCurrentPackageName = string.Empty;
        private int mCompletedPackageCount;
        private int mFinishedCount;
        private int mFinishedCurrentCount;
        private long mFinishedBytes;
        private long mFinishedCurrentBytes;
        private int mActiveTotalCount;
        private int mActiveCurrentCount;
        private long mActiveTotalBytes;
        private long mActiveCurrentBytes;

        /// <summary>创建一次顺序多包下载的总进度会话。</summary>
        /// <param name="packageCount">本次会话包含的 package 数，至少为 1。</param>
        /// <param name="onProgress">总进度回调；为空时所有报告都直接返回。</param>
        public YooAssetDownloadProgressScope(int packageCount, YooAssetDownloadProgressHandler onProgress)
        {
            if (packageCount < 1)
                throw new ArgumentOutOfRangeException(nameof(packageCount));

            mPackageCount = packageCount;
            mOnProgress = onProgress;
            mPackageProgress = new float[packageCount];
            mCompleted = new bool[packageCount];
        }

        /// <summary>开始一个 package 的份额。重复开始同一个下标时不重置已有下载量。</summary>
        /// <param name="packageName">package 名称。</param>
        /// <param name="packageIndex">0 基下标。</param>
        public void BeginPackage(string packageName, int packageIndex)
        {
            EnsureIndex(packageIndex);
            if (mCurrentIndex != packageIndex)
                ClearActive();

            mCurrentIndex = packageIndex;
            mCurrentPackageName = packageName ?? string.Empty;
            Report();
        }

        /// <summary>用当前下载器的快照覆盖该 package 的份额，不与上一次快照相加。</summary>
        /// <param name="progress">当前 package 的下载进度。</param>
        public void ReportPackage(YooAssetPackageDownloadProgress progress)
        {
            if (mCurrentIndex < 0 || mCompleted[mCurrentIndex])
                return;

            mPackageProgress[mCurrentIndex] = Clamp01(progress.Progress);
            mActiveTotalCount = Math.Max(0, progress.TotalDownloadCount);
            mActiveCurrentCount = Math.Max(0, progress.CurrentDownloadCount);
            mActiveTotalBytes = Math.Max(0L, progress.TotalDownloadBytes);
            mActiveCurrentBytes = Math.Max(0L, progress.CurrentDownloadBytes);
            if (!string.IsNullOrEmpty(progress.PackageName))
                mCurrentPackageName = progress.PackageName;
            Report();
        }

        /// <summary>把 package 份额记为完成。没有缺失文件时也要调用，避免总进度停在两个包之间。</summary>
        /// <param name="packageName">package 名称。</param>
        /// <param name="packageIndex">0 基下标。</param>
        public void CompletePackage(string packageName, int packageIndex)
        {
            EnsureIndex(packageIndex);
            if (mCompleted[packageIndex])
                return;

            if (mCurrentIndex != packageIndex)
                ClearActive();

            mFinishedCount += mActiveTotalCount;
            mFinishedCurrentCount += mActiveTotalCount;
            mFinishedBytes += mActiveTotalBytes;
            mFinishedCurrentBytes += mActiveTotalBytes;
            ClearActive();
            mPackageProgress[packageIndex] = 1f;
            mCompleted[packageIndex] = true;
            mCompletedPackageCount++;
            mCurrentIndex = packageIndex;
            mCurrentPackageName = packageName ?? string.Empty;
            Report();
        }

        /// <summary>校验下标，避免把进度写进不存在的份额。</summary>
        private void EnsureIndex(int packageIndex)
        {
            if (packageIndex < 0 || packageIndex >= mPackageCount)
                throw new ArgumentOutOfRangeException(nameof(packageIndex));
        }

        /// <summary>清除当前下载器快照。已完成包的数量已经转入完成基线。</summary>
        private void ClearActive()
        {
            mActiveTotalCount = 0;
            mActiveCurrentCount = 0;
            mActiveTotalBytes = 0L;
            mActiveCurrentBytes = 0L;
        }

        /// <summary>按等权份额计算总进度，并把已完成包与当前下载器的数量相加。</summary>
        private void Report()
        {
            if (mOnProgress == null)
                return;

            float total = 0f;
            for (int index = 0; index < mPackageProgress.Length; index++)
                total += mPackageProgress[index];

            float progress = total / mPackageCount;
            mOnProgress(new YooAssetDownloadProgress(
                mCurrentPackageName,
                Math.Max(0, mCurrentIndex),
                mPackageCount,
                Clamp01(progress),
                mFinishedCount + mActiveTotalCount,
                mFinishedCurrentCount + mActiveCurrentCount,
                mFinishedBytes + mActiveTotalBytes,
                mFinishedCurrentBytes + mActiveCurrentBytes,
                mCompletedPackageCount));
        }

        /// <summary>把进度限制在 0 到 1，避免下载器异常值穿透到 UI。</summary>
        private static float Clamp01(float progress)
        {
            if (progress < 0f)
                return 0f;
            if (progress > 1f)
                return 1f;
            return progress;
        }
    }
}
#endif
