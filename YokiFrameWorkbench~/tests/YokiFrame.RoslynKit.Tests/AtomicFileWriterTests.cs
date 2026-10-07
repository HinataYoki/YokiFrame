using System;
using System.IO;
using System.Text;
using Xunit;

namespace YokiFrame.RoslynKit.Tests;

/// <summary>
/// 原子写在**真实调用点**上的行为：eval 记录与运行记录都必须「覆盖成功、临时文件不残留、UTF-8 无 BOM」。
/// 共享实现 YokiFrameAtomicFileWriter 是 internal，因此从公开存储入口验证。
/// </summary>
public sealed class AtomicFileWriterTests : IDisposable
{
    private readonly string mRoot = Path.Combine(
        Path.GetTempPath(),
        "yokiframe-atomic-writer-tests",
        Guid.NewGuid().ToString("N"));

    /// <summary>删除本测试创建的临时根目录。目录不存在时不做任何事。</summary>
    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(mRoot))
        {
            Directory.Delete(mRoot, recursive: true);
        }
    }

    /// <summary>eval 记录可覆盖保存，且目录中不残留临时文件。不改其他记录。</summary>
    [Fact]
    public void Eval_store_save_overwrites_without_temp_leftovers()
    {
        var store = new RoslynEvalStore(mRoot);
        RoslynEvalRecord record = CreateEvalRecord("eval-1", "first-content");

        store.Save(record);
        record.Note = "second";
        store.Save(record);

        Assert.True(store.TryRead("eval-1", out RoslynEvalRecord reloaded));
        Assert.Equal("second", reloaded.Note);
        AssertNoTempLeftovers(Path.Combine(mRoot, ".yokiframe", "engine", "eval"));
    }

    /// <summary>运行记录可覆盖保存，且目录中不残留临时文件。状态字段保持原写入值。</summary>
    [Fact]
    public void Run_store_save_overwrites_without_temp_leftovers()
    {
        var store = new RoslynRunStore(mRoot);
        var record = new RoslynRunRecord
        {
            RunId = "run-1",
            LegacyEntry = "run.pass",
            State = RunStatus.Queued,
            SubmittedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        store.Save(record);
        record.Note = "updated";
        store.Save(record);

        Assert.True(store.TryReadRun("run-1", out RoslynRunRecord reloaded));
        Assert.Equal("updated", reloaded.Note);
        AssertNoTempLeftovers(Path.Combine(mRoot, ".yokiframe", "engine", "runs"));
    }

    /// <summary>写出的记录是无 BOM 的 UTF-8，中文内容可原样读回。</summary>
    [Fact]
    public void Written_records_are_utf8_without_bom()
    {
        var store = new RoslynEvalStore(mRoot);
        store.Save(CreateEvalRecord("eval-2", "会话"));

        string path = Path.Combine(mRoot, ".yokiframe", "engine", "eval", "eval-2.json");
        byte[] bytes = File.ReadAllBytes(path);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        Assert.Contains("会话", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    /// <summary>构造一条可保存的 eval 记录。不写磁盘。</summary>
    /// <param name="id">记录标识。</param>
    /// <param name="note">备注文本。</param>
    /// <returns>状态为 Ready、令牌固定的记录。</returns>
    private static RoslynEvalRecord CreateEvalRecord(string id, string note)
    {
        return new RoslynEvalRecord
        {
            Id = id,
            Token = "token",
            State = RoslynEvalStates.READY,
            Note = note,
            SubmittedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>断言目录树中没有文件名包含 .tmp 的残留。目录不存在时直接返回。</summary>
    /// <param name="directory">待检查的存储目录。</param>
    private static void AssertNoTempLeftovers(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        string[] files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
        for (var index = 0; index < files.Length; index++)
        {
            Assert.DoesNotContain(".tmp", Path.GetFileName(files[index]), StringComparison.Ordinal);
        }
    }
}
