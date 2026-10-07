#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.IO;

namespace YokiFrame
{
    public sealed partial class RoslynRunStore
    {
        /// <summary>
        /// 独占创建认领文件；已存在且租约过期时接管。无权限时不接管。
        /// </summary>
        /// <param name="claimPath">认领文件路径。</param>
        /// <param name="ownerSessionId">认领方会话。</param>
        /// <param name="generation">认领方代次。</param>
        /// <param name="lease">租约时长。</param>
        /// <returns>取得认领时返回 true。</returns>
        private static bool TryAcquireClaim(
            string claimPath, string ownerSessionId, long generation, TimeSpan lease)
        {
            DateTime now = DateTime.UtcNow;
            string payload = BuildClaimJson(ownerSessionId, generation, now.Add(lease));
            if (TryCreateClaim(claimPath, payload, out bool denied)) return true;
            if (denied || !IsClaimExpired(claimPath, now)) return false;
            return TryReplaceClaim(claimPath, payload);
        }

        /// <summary>独占创建认领文件。无权限直接拒绝；文件已存在则交给租约判断。</summary>
        /// <param name="claimPath">认领文件路径。</param>
        /// <param name="payload">认领 JSON。</param>
        /// <param name="denied">无权限或无法判断存在性时返回 true。</param>
        /// <returns>创建成功时返回 true。</returns>
        private static bool TryCreateClaim(string claimPath, string payload, out bool denied)
        {
            denied = false;
            try
            {
                using (var stream = new FileStream(claimPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, sUtf8NoBom))
                {
                    writer.Write(payload);
                }

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                denied = true;
                return false;
            }
            catch (IOException)
            {
                denied = !File.Exists(claimPath);
                return false;
            }
        }

        /// <summary>覆盖已过期认领。写入失败时不声称取得租约。</summary>
        /// <param name="claimPath">认领文件路径。</param>
        /// <param name="payload">认领 JSON。</param>
        /// <returns>覆盖成功时返回 true。</returns>
        private static bool TryReplaceClaim(string claimPath, string payload)
        {
            try
            {
                File.WriteAllText(claimPath, payload, sUtf8NoBom);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>返回运行认领文件路径。后缀不是 json，避免被目录扫描当成记录。</summary>
        /// <param name="runId">运行标识。</param>
        /// <returns>认领文件路径。</returns>
        private string ClaimPath(string runId)
        {
            return Path.Combine(mRunsRoot, runId + CLAIM_SUFFIX);
        }

        /// <summary>构造认领 JSON。owner 必须转义，否则解析失败会被当成租约过期。</summary>
        /// <param name="ownerSessionId">认领方会话。</param>
        /// <param name="generation">认领方代次。</param>
        /// <param name="expiresAtUtc">租约到期时间。</param>
        /// <returns>认领 JSON。</returns>
        private static string BuildClaimJson(string ownerSessionId, long generation, DateTime expiresAtUtc)
        {
            return "{\"ownerSessionId\":\"" + Escape(ownerSessionId)
                + "\",\"generation\":" + generation.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ",\"expiresAtUtc\":\"" + expiresAtUtc.ToString("o") + "\"}";
        }

        /// <summary>转义会影响 JSON 解析的引号和反斜杠。</summary>
        /// <param name="text">原文。</param>
        /// <returns>转义后的文本。</returns>
        private static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.IndexOf('"') < 0 && text.IndexOf('\\') < 0
                ? text
                : text.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        /// <summary>判断认领是否缺失、损坏或已过期。读取失败按过期处理，避免永久占租。</summary>
        /// <param name="claimPath">认领文件路径。</param>
        /// <param name="now">当前 UTC 时间。</param>
        /// <returns>可以接管时返回 true。</returns>
        private static bool IsClaimExpired(string claimPath, DateTime now)
        {
            try
            {
                if (!File.Exists(claimPath)) return true;
                string text = File.ReadAllText(claimPath, sUtf8NoBom);
                int start = text.IndexOf("\"expiresAtUtc\":\"", StringComparison.Ordinal);
                if (start < 0) return true;
                start += "\"expiresAtUtc\":\"".Length;
                int end = text.IndexOf('"', start);
                if (end <= start) return true;
                return !DateTime.TryParse(
                    text.Substring(start, end - start),
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out DateTime expiresAtUtc)
                    || expiresAtUtc <= now;
            }
            catch (IOException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }
    }
}
#endif
